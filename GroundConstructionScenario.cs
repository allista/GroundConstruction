//   GroundConstructionScenario.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
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
			[Persistent] public uint   wid;
			[Persistent] public string Name;
			[Persistent] public string KitName;
			[Persistent] public bool   Complete;
			[Persistent] public double EndUT;
			[Persistent] public string ETA;

			public WorkshopInfo() {}
			public WorkshopInfo(GroundWorkshop workshop) 
			{
				vesselID = workshop.vessel.id;
				wid = workshop.part.flightID;
				Name = workshop.vessel.vesselName;
				KitName = workshop.KitUnderConstruction.KitName;
				EndUT = workshop.ETA > 0? Planetarium.GetUniversalTime()+workshop.ETA : -1;
			}

			public void SwitchTo()
			{
				Utils.Log("Switching To: {}, vid {}", this, vesselID);//debug
				var vsl = FlightGlobals.FindVessel(vesselID);
				if(vsl == null) 
				{
					Utils.Message("{0} was not found in the game", Name);
					return;
				}
				if(FlightGlobals.ready) FlightGlobals.SetActiveVessel(vsl);
				else
				{
					GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
					FlightDriver.StartAndFocusVessel("persistent", FlightGlobals.Vessels.IndexOf(vsl));
				}
//				{
//					var game = HighLogic.CurrentGame.Updated();
//					if(game.flightState != null)
//					{
//						Utils.Log("FlightState.protoVessels: {}", game.flightState.protoVessels);//debug
//						var idx = game.flightState.protoVessels.FindIndex(pv => pv.vesselID == vid);
//						if(idx >= 0)
//						{
//							GamePersistence.SaveGame(game, "persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
//							FlightDriver.StartAndFocusVessel("persistent", idx);
//							return;
//						}
//					}
//				}
			}

			public override string ToString()
			{ 
				return Complete? 
					string.Format("\"{0}\" assembled \"{1}\".", Name, KitName) :
					string.Format("\"{0}\" is building \"{1}\". {2}", Name, KitName, ETA);
			}
		}

		static Dictionary<uint,WorkshopInfo> Workshops = new Dictionary<uint,WorkshopInfo>();

		public static bool RegisterWorkshop(GroundWorkshop workshop)
		{
			if(workshop.KitUnderConstruction.Valid && workshop.Working)
			{
				Workshops[workshop.part.flightID] = new WorkshopInfo(workshop);
				Utils.Log("Workshop registered: {} [{}], {}, ETA: {}", 
				          workshop.vessel.vesselName, workshop.vessel.id, 
				          workshop.KitUnderConstruction.KitName, workshop.ETA); //debug
				return true;
			}
			return false;
		}

		public static bool DeregisterWorkshop(GroundWorkshop workshop)
		{ return Workshops.Remove(workshop.part.flightID); }

		IEnumerator<YieldInstruction> slow_update()
		{
			while(true)
			{
				var now = Planetarium.GetUniversalTime();
				var finished = false;
				foreach(var workshop in Workshops.Values)
				{
					if(workshop.Complete) continue;
					if(workshop.EndUT > 0 && 
					   workshop.EndUT < now)
					{
						Utils.Message(10, "Engineers at '{0}' should have assembled the '{1}' by now.",
						              workshop.Name, workshop.KitName);
						workshop.Complete = true;
						finished = true;
					}
					else
					{
						workshop.ETA = workshop.EndUT > 0?
							"Time left: "+KSPUtil.PrintTimeCompact(workshop.EndUT-now, false) : "Stalled...";
					}
				}
				if(finished) TimeWarp.SetRate(0, false);
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
			workshops.Sort((a,b) => a.wid.CompareTo(b.wid));
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
				workshops.ForEach(w => Workshops.Add(w.wid, w));
			}
		}

		#region GUI
		const float width = 500;
		const float height = 50;

		static bool show_window = true;
		public static void ShowWindow(bool show) { show_window = show; }
		public static void ToggleWindow() { show_window = !show_window; }

		Vector2 workshops_scroll = Vector2.zero;
		Rect WindowPos = new Rect(Screen.width-width-100, 0, Screen.width/4, Screen.height/4);
		void main_window(int WindowID)
		{
			GUILayout.BeginVertical(Styles.white);
			workshops_scroll = GUILayout.BeginScrollView(workshops_scroll, GUILayout.Height(height), GUILayout.Width(width));
			WorkshopInfo switchto = null;
			foreach(var item in Workshops) 
			{
				var info = item.Value;
				GUILayout.BeginHorizontal();
				var style = info.Complete? Styles.green : (info.EndUT > 0? Styles.yellow : Styles.red);
				GUILayout.Label(info.ToString(), style, GUILayout.ExpandWidth(true));
				if(GUILayout.Button(new GUIContent("Focus", "Switch to this workshop"), 
				                    Styles.active_button, GUILayout.ExpandWidth(false)))
					switchto = info;
				GUILayout.EndHorizontal();
			}
			if(switchto != null) switchto.SwitchTo();
			GUILayout.EndScrollView();
			if(GUILayout.Button("Close", Styles.close_button, GUILayout.ExpandWidth(true)))
				show_window = false;
			GUILayout.EndVertical();
			GUIWindowBase.TooltipsAndDragWindow(WindowPos);
		}

		const string LockName = "GroundConstructionScenario";
		void OnGUI()
		{
			if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
			if(show_window && GUIWindowBase.HUD_enabled && Workshops.Count > 0)
			{
				Styles.Init();

					Utils.LockIfMouseOver(LockName, WindowPos);
					WindowPos = GUILayout.Window(GetInstanceID(), 
				                                 WindowPos, main_window, "Active Ground Workshops",
					                             GUILayout.Width(width),
					                             GUILayout.Height(height)).clampToScreen();
			}
			else Utils.LockIfMouseOver(LockName, WindowPos, false);
		}
		#endregion
	}
}

