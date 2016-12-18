//   GroundConstructionScenario.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
	[KSPScenario(ScenarioCreationOptions.AddToAllGames, new []
	{
		GameScenes.SPACECENTER,
		GameScenes.FLIGHT,
		GameScenes.TRACKSTATION,
	})]
	public class GroundConstructionScenario : ScenarioModule
	{
		static Globals GLB { get { return Globals.Instance; } }

		public class WorkshopInfo : VesselInfo
		{
			public enum Status { IDLE, ACTIVE, COMPLETE };

			[Persistent] public uint   id;
			[Persistent] public string CB;
			[Persistent] public string Name;
			[Persistent] public string KitName;
			[Persistent] public Status State;
			[Persistent] public double EndUT;
			[Persistent] public string ETA;

			public WorkshopInfo() {}
			public WorkshopInfo(GroundWorkshop workshop) 
			{
				vesselID = workshop.vessel.id;
				id = workshop.part.flightID;
				CB = workshop.vessel.mainBody.bodyName;
				Name = workshop.vessel.vesselName;
				State = Status.IDLE;
				EndUT = -1;
				if(workshop.KitUnderConstruction.Valid) 
				{
					State = Status.ACTIVE;
					KitName = workshop.KitUnderConstruction.KitName;
					if(workshop.ETA > 0 && workshop.LastUpdateTime > 0) 
						EndUT = workshop.LastUpdateTime+workshop.ETA;
					if(EndUT < Planetarium.GetUniversalTime())
						State = Status.COMPLETE;
				}
			}

			public bool Recheck()
			{ return FlightGlobals.FindVessel(vesselID) != null; }

			public bool SwitchTo()
			{
				var vsl = FlightGlobals.FindVessel(vesselID);
				if(vsl == null) 
				{
					Utils.Message("{0} was not found in the game", Name);
					return false;
				}
				if(FlightGlobals.ready) FlightGlobals.SetActiveVessel(vsl);
				else
				{
					GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
					FlightDriver.StartAndFocusVessel("persistent", FlightGlobals.Vessels.IndexOf(vsl));
				}
				return true;
			}

			public override string ToString()
			{ 
				if(State == Status.COMPLETE)
					return string.Format("[{0}] \"{1}\" assembled \"{2}\".", CB, Name, KitName);
				if(State == Status.ACTIVE)
					return string.Format("[{0}] \"{1}\" is building \"{2}\". {3}", CB, Name, KitName, ETA);
				return string.Format("[{0}] \"{1}\" is idle.", CB, Name);
			}

			public void Draw()
			{
				var style = Styles.white;
				GUIContent status = null;
				if(State == Status.COMPLETE)
				{
					style = Styles.green;
					status = new GUIContent(KitName, "Complete");
				}
				else if(State == Status.ACTIVE)
				{
					style = EndUT > 0? Styles.yellow : Styles.red;
					status = new GUIContent(KitName, "Under construction. "+ETA);
				}
				GUILayout.BeginHorizontal();
				GUILayout.Label(CB, Styles.yellow, GUILayout.Width(60));
				GUILayout.Label(Name, Styles.white, GUILayout.ExpandWidth(true));
				if(status != null) GUILayout.Label(status, style, GUILayout.ExpandWidth(false));
				GUILayout.EndHorizontal();
			}
		}

		static SortedDictionary<uint,WorkshopInfo> Workshops = new SortedDictionary<uint,WorkshopInfo>();
		double now = -1;

		public static void CheckinWorkshop(GroundWorkshop workshop)
		{
			if(workshop.part == null || workshop.vessel == null) return;
			Workshops[workshop.part.flightID] = new WorkshopInfo(workshop);
//			Utils.Log("Workshop registered: {} [{}], {}, ETA: {}", 
//			          workshop.vessel.vesselName, workshop.vessel.id, 
//			          workshop.KitUnderConstruction.KitName, workshop.ETA); //debug
		}

		public static bool DeregisterWorkshop(GroundWorkshop workshop)
		{ return Workshops.Remove(workshop.part.flightID); }

		bool reckeck_workshops()
		{
			if(FlightGlobals.Vessels != null && FlightGlobals.Vessels.Count > 0)
			{
				var _workshops = new SortedDictionary<uint, WorkshopInfo>();
				foreach(var workshop in Workshops)
				{
					if(workshop.Value.Recheck())
						_workshops.Add(workshop.Key, workshop.Value);
				}
				Workshops = _workshops;
				return true;
			}
			return false;
		}

		IEnumerator<YieldInstruction> slow_update()
		{
			while(true)
			{
				reckeck_workshops();
				var finished = false;
				now = Planetarium.GetUniversalTime();
				foreach(var workshop in Workshops.Values)
				{
					if(workshop.State != WorkshopInfo.Status.ACTIVE) continue;
					if(workshop.EndUT > 0 && 
					   workshop.EndUT < now)
					{
						Utils.Message(10, "Engineers at '{0}' should have assembled the '{1}' by now.",
						              workshop.Name, workshop.KitName);
						workshop.State = WorkshopInfo.Status.COMPLETE;
						workshop.EndUT = -1;
						finished = true;
					}
					else
					{
						workshop.ETA = workshop.EndUT > 0?
							"Time left: "+KSPUtil.PrintTimeCompact(workshop.EndUT-now, false) : "Stalled...";
					}
				}
				if(finished) TimeWarp.SetRate(0, !HighLogic.LoadedSceneIsFlight);
				yield return new WaitForSeconds(1);
			}
		}

		public override void OnAwake()
		{
			base.OnAwake();
			StartCoroutine(slow_update());
		}

		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			var workshops = new PersistentList<WorkshopInfo>(Workshops.Values);
			workshops.Sort((a,b) => a.id.CompareTo(b.id));
			workshops.Save(node.AddNode("Workshops"));
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			Workshops.Clear();
			var wnode = node.GetNode("Workshops");
			if(wnode != null)
			{
				var workshops = new PersistentList<WorkshopInfo>();
				workshops.Load(wnode);
				workshops.ForEach(w => Workshops.Add(w.id, w));
			}
		}

		#region GUI
		const float width = 500;
		const float height = 120;

		static bool show_window;
		public static void ShowWindow(bool show) { show_window = show; }
		public static void ToggleWindow() { show_window = !show_window; }

		Vector2 workshops_scroll = Vector2.zero;
		Rect WindowPos = new Rect(Screen.width-width-100, 0, Screen.width/4, Screen.height/4);
		void main_window(int WindowID)
		{
			GUILayout.BeginVertical(Styles.white);
			if(Workshops.Count > 0)
			{
				workshops_scroll = GUILayout.BeginScrollView(workshops_scroll, GUILayout.Height(height), GUILayout.Width(width));
				WorkshopInfo switchto = null;
//				WorkshopInfo warpto = null; //TODO: implement warp to
				foreach(var item in Workshops) 
				{
					var info = item.Value;
					GUILayout.BeginHorizontal();
					info.Draw();
//					if(info.EndUT < now)
//					{
//						if(GUILayout.Button(new GUIContent("Warp", "Warp to the end of construction"), 
//						                    Styles.active_button, GUILayout.ExpandWidth(false)))
//							warpto = info;
//					}
					if(GUILayout.Button(new GUIContent("Focus", "Switch to this workshop"), 
					                    Styles.enabled_button, GUILayout.ExpandWidth(false)))
						switchto = info;
					GUILayout.EndHorizontal();
				}
//				if(warpto != null) pass
				if(switchto != null) 
				{
					if(!switchto.SwitchTo()) 
						Workshops.Remove(switchto.id);
				}
				GUILayout.EndScrollView();
				if(GUILayout.Button("Close", Styles.close_button, GUILayout.ExpandWidth(true)))
					show_window = false;
			}
			else GUILayout.Label("No Ground Workshops", Styles.white, GUILayout.ExpandWidth(true));
			GUILayout.EndVertical();
			GUIWindowBase.TooltipsAndDragWindow(WindowPos);
		}

		const string LockName = "GroundConstructionScenario";
		void OnGUI()
		{
			if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
			if(show_window && GUIWindowBase.HUD_enabled)
			{
				Styles.Init();

					Utils.LockIfMouseOver(LockName, WindowPos);
					WindowPos = GUILayout.Window(GetInstanceID(), 
				                                 WindowPos, main_window, "Ground Workshops",
					                             GUILayout.Width(width),
					                             GUILayout.Height(height)).clampToScreen();
			}
			else Utils.LockIfMouseOver(LockName, WindowPos, false);
		}
		#endregion
	}
}

