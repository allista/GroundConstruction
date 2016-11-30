//   GroundWorkshop.cs
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
using Experience;
using Experience.Effects;

namespace GroundConstruction
{
	public class GroundWorkshop : PartModule
	{
		static Globals GLB { get { return Globals.Instance; } }

		public class KitInfo : ConfigNodeObject
		{
			[Persistent] public Guid id = Guid.Empty;
			[Persistent] public string KitName = string.Empty;

			public ModuleConstructionKit Module { get; private set; }
			public VesselKit Kit { get { return ModuleValid? Module.kit: null; } }

			public bool Valid { get { return id != Guid.Empty; } }
			public bool ModuleValid { get { return Module != null && Module.Valid; } }

			public KitInfo() {}
			public KitInfo(ModuleConstructionKit kit_module)
			{
				id = kit_module.vessel.id;
				Module = kit_module;
				KitName = kit_module.KitName;
			}


			public bool Recheck()
			{
				if(ModuleValid) return true;
				FindKit();
				return ModuleValid;
			}

			public static ModuleConstructionKit GetKitFromVessel(Vessel vsl)
			{
				var kit_part = vsl.Parts.Find(p => p.Modules.Contains<ModuleConstructionKit>());
				return kit_part == null? null : kit_part.Modules.GetModule<ModuleConstructionKit>();
			}

			public ModuleConstructionKit FindKit()
			{
				var kit_vsl = FlightGlobals.FindVessel(id);
				Module = kit_vsl == null? null : GetKitFromVessel(kit_vsl);
				return Module;
			}

			public override string ToString()
			{
				var s = string.Format("\"{0}\"", KitName);
				if(ModuleValid) s += string.Format(" needs {0:F1} SKH, {1}", Module.kit.WorkLeft/3600, Module.KitStatus);
				return s;
			}
		}

		[KSPField(isPersistant = true)] public bool Working;
		[KSPField(isPersistant = true)] public double LastUpdateTime = -1;
		[KSPField(isPersistant = true)] public PersistentQueue<KitInfo> Queue = new PersistentQueue<KitInfo>();
		[KSPField(isPersistant = true)] public KitInfo KitUnderConstruction = new KitInfo();
		float workers = 0;
		float distance_mod = 0;
		double eta = -1;
		string ETA = "Stalled...";

		bool get_next_kit()
		{
			LastUpdateTime = -1;
			while(Queue.Count > 0 && !KitUnderConstruction.ModuleValid)
			{
				KitUnderConstruction = Queue.Dequeue();
				KitUnderConstruction.FindKit();
			}
			if(KitUnderConstruction.ModuleValid) return true;
			KitUnderConstruction = new KitInfo();
			return false;
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			LockName = "GroundWorkshop"+GetInstanceID();
			if(FlightGlobals.ready && KitUnderConstruction.Valid)
			{
				KitUnderConstruction.FindKit();
				if(!KitUnderConstruction.ModuleValid) 
				{
					KitUnderConstruction = new KitInfo();
					LastUpdateTime = -1;
				}
			}
			update_workers();
		}

		void update_queue()
		{
			if(Queue.Count == 0) return;
			Queue = new PersistentQueue<KitInfo>(Queue.Where(kit => kit.Recheck()));
		}

		List<KitInfo> nearby_unbuilt_kits = new List<KitInfo>();
		List<KitInfo> nearby_built_kits = new List<KitInfo>();
		void update_nearby_kits()
		{
			if(!FlightGlobals.ready) return;
			var queued = new HashSet<Guid>(Queue.Select(k => k.id));
			nearby_unbuilt_kits.Clear();
			nearby_built_kits.Clear();
			foreach(var vsl in FlightGlobals.Vessels)
			{
				var vsl_kit = KitInfo.GetKitFromVessel(vsl);
				if(vsl_kit != null && vsl_kit.Valid && vsl_kit.Deployed &&
				   vsl_kit != KitUnderConstruction.Module && !queued.Contains(vsl.id) &&
				   (part.transform.position-vsl_kit.transform.position).magnitude < GLB.MaxDistanceToWorkshop)
				{
					if(vsl_kit.Completeness < 1)
						nearby_unbuilt_kits.Add(new KitInfo(vsl_kit));
					else nearby_built_kits.Add(new KitInfo(vsl_kit));
				}
			}
		}

		void update_workers()
		{
			workers = 0;
			foreach(var kerbal in part.protoModuleCrew)
			{
				var worker = 0;
				var trait = kerbal.experienceTrait;
				foreach(var effect in trait.Effects)
				{
					if(effect is DrillSkill ||
					   effect is ConverterSkill ||
					   effect is RepairSkill)
					{ worker = 1; break; }
				}
				worker *= trait.CrewMemberExperienceLevel();
				workers += worker;
			}
			this.Log("workers: {}", workers);//debug
		}

		bool can_construct()
		{
			if(!vessel.Landed)
			{
				Utils.Message("Cannot construct unless landed.");
				return false;
			}
			if(vessel.srfSpeed > GLB.DeployMaxSpeed)
			{
				Utils.Message("Cannot construct while mooving.");
				return false;
			}
			if(vessel.angularVelocity.sqrMagnitude > GLB.DeployMaxAV)
			{
				Utils.Message("Cannot construct while rotating.");
				return false;
			}
			return true;
		}

		void Update()
		{
			if(Working) 
			{
				update_workers();
				if(workers > 0 && distance_mod > 0 && KitUnderConstruction.Kit != null)
				{
					eta = KitUnderConstruction.Kit.WorkLeft;
					if(KitUnderConstruction.Kit.PartUnderConstruction != null)
						eta += KitUnderConstruction.Kit.PartUnderConstruction.WorkLeft;
					eta /= workers*distance_mod;
					ETA = "Time left: "+KSPUtil.PrintTimeCompact(eta, false);
				}
				else 
				{
					eta = -1;
					ETA = "Stalled...";
				}
				Working = can_construct();
			}
			if(show_window)
			{
				update_queue();
				update_nearby_kits();
			}
		}

		float dist2target(KitInfo kit)
		{ return (kit.Module.part.transform.position-part.transform.position).magnitude; }

		void FixedUpdate() //TODO: implement huge catch-ups that allow multiple kits assembly at once
		{
			if(!HighLogic.LoadedSceneIsFlight || !Working || workers.Equals(0)) return;
			var deltaTime = GetDeltaTime();
			this.Log("deltaTime: {}, fixedDeltaTime {}", deltaTime, TimeWarp.fixedDeltaTime);//debug
			if(deltaTime < 0) return;
			//check current kit
			if(!KitUnderConstruction.ModuleValid) 
			{ Working = get_next_kit(); return; }
			//calculate distance modifier
			var dist = dist2target(KitUnderConstruction);
			this.Log("dist: {}", dist);//debug
			if(dist > GLB.MaxDistanceToWorkshop)
			{ 
				Utils.Message("{0} is too far away. Switching to the next kit in line.", KitUnderConstruction.KitName);
				Working = get_next_kit(); 
				return; 
			}
			distance_mod = Mathf.Lerp(1, GLB.MaxDistanceEfficiency, 
			                          Mathf.Max((dist-GLB.MinDistanceToWorkshop)/GLB.MaxDistanceToWorkshop, 0));
			//try to get the structure resource
			double work = workers*distance_mod*deltaTime;
			double required_ec;
			var required_res = KitUnderConstruction.Module.RequiredMass(ref work, out required_ec)/GLB.StructureResource.density;
			var have_res = part.RequestResource(GLB.StructureResourceID, required_res);
			this.Log("dist.mod: {}, work left {}, work {}, n.res {}, n.EC {}", distance_mod, KitUnderConstruction.Kit.WorkLeft, work, required_res, required_ec);//debug
			if(have_res.Equals(0)) return;
			//try to get EC
			var have_ec = part.RequestResource(Utils.ElectricChargeID, required_ec);
			if(have_ec/required_ec < GLB.WorkshopShutdownThreshold) 
			{ 
				Utils.Message("Not enough energy. Construction of {0} was put on hold.", KitUnderConstruction.KitName);
				Working = false; 
				return; 
			}
			//do the work, if any
			have_res *= have_ec/required_ec;
			work *= have_res/required_res;
			this.Log("work {}, res {}/{}, EC {}/{}", work, have_res, required_res, have_ec, required_ec);//debug
			if(work > 0) 
			{
				KitUnderConstruction.Module.DoSomeWork(work);
				this.Log("{}.completeness {}", KitUnderConstruction.KitName, KitUnderConstruction.Module.Completeness);//debug
				if(KitUnderConstruction.Module.Completeness >= 0)
					Working = get_next_kit();
			}
			//return unused structure resource
			if(have_res < required_res)
				part.RequestResource(GLB.StructureResourceID, have_res-required_res);
		}

		double GetDeltaTime()
		{
			if(Time.timeSinceLevelLoad < 1 || !FlightGlobals.ready) return -1;
			if(LastUpdateTime < 0)
			{
				LastUpdateTime = Planetarium.GetUniversalTime();
				return TimeWarp.fixedDeltaTime;
			}
			var time = Planetarium.GetUniversalTime();
			var dT = time - LastUpdateTime;
			LastUpdateTime = time;
			return dT;
		}

		#region Resource Transfer
		bool transfer_resources;
		readonly ResourceManifestList transfer_list = new ResourceManifestList();
		VesselResources host_resources, kit_resources;
		KitInfo target_kit;

		void setup_resource_transfer(KitInfo target)
		{
			if(!target.Recheck()) return;
			if(dist2target(target) > GLB.MaxDistanceToWorkshop)
			{
				Utils.Message("{0} is too far away. Needs to be closer that {1}m", 
				              target.KitName, GLB.MaxDistanceToWorkshop);
				return;
			}
			target_kit = target;
			host_resources = new VesselResources(vessel);
			kit_resources = target_kit.Module.GetConstructResources();
			transfer_list.NewTransfer(host_resources, kit_resources);
			if(transfer_list.Count > 0) return;
			host_resources = null;
			kit_resources = null;
			target_kit = null;
			transfer_resources = false;
		}
		#endregion

		#region GUI
		readonly ResourceTransferWindow resources_window = new ResourceTransferWindow();

		bool show_window;
		const float width = 400;
		const float height = 50;

		[KSPEvent(guiName = "Construction Window", guiActive = true, active = true)]
		public void ToggleConstructionWindow()
		{ show_window = !show_window; }

		void info_pane()
		{
			if(KitUnderConstruction.ModuleValid) 
			{
				GUILayout.BeginVertical(Styles.white);
				GUILayout.Label("Constructing: "+KitUnderConstruction.KitName, Styles.green, GUILayout.ExpandWidth(true));
				GUILayout.Label(KitUnderConstruction.Module.KitStatus, Styles.fracStyle(KitUnderConstruction.Module.Completeness), GUILayout.ExpandWidth(true));
				GUILayout.Label(KitUnderConstruction.Module.PartStatus, Styles.fracStyle(KitUnderConstruction.Module.PartCompleteness), GUILayout.ExpandWidth(true));
				if(Working)
				{
					GUILayout.Label(string.Format("Efficiency (due to distance): {0:P1}", distance_mod), Styles.fracStyle(distance_mod), GUILayout.ExpandWidth(true));
					GUILayout.Label(ETA, Styles.boxed_label, GUILayout.ExpandWidth(true));
                }
				if(GUILayout.Button("Moove back to the Queue", 
				                    Styles.danger_button, GUILayout.ExpandWidth(true)))
				{
					Queue.Enqueue(KitUnderConstruction);
					KitUnderConstruction = new KitInfo();
					LastUpdateTime = -1;
					Working &= Queue.Count > 1;
				}
				GUILayout.EndVertical();
			}
			else GUILayout.Label("Nothing is under construction now.", Styles.boxed_label, GUILayout.ExpandWidth(true));
		}

		Vector2 queue_scroll = Vector2.zero;
		void queue_pane()
		{
			if(Queue.Count > 0)
			{
				GUILayout.Label("Construction Queue:", GUILayout.ExpandWidth(true));
				GUILayout.BeginVertical(Styles.white);
				queue_scroll = GUILayout.BeginScrollView(queue_scroll, GUILayout.Height(height), GUILayout.Width(width));
				KitInfo del = null;
				KitInfo up = null;
				foreach(var info in Queue) 
				{
					GUILayout.BeginHorizontal();
					GUILayout.Label(info.ToString(), Styles.boxed_label, GUILayout.ExpandWidth(true));
					if(GUILayout.Button(new GUIContent("^", "Move up"), 
					                    Styles.normal_button, GUILayout.Width(25)))
						up = info;
					if(GUILayout.Button(new GUIContent("X", "Remove from Queue"), 
					                    Styles.danger_button, GUILayout.Width(25))) 
						del = info;
					GUILayout.EndHorizontal();
				}
				if(del != null) Queue.Remove(del);
				else if(up != null) Queue.MoveUp(up);
				GUILayout.EndScrollView();
				GUILayout.EndVertical();
			}
			if(Utils.ButtonSwitch("Process the Queue", ref Working, "", GUILayout.ExpandWidth(true)))
			{ Working &= can_construct(); }
		}

		Vector2 built_scroll = Vector2.zero;
		void built_kits_pane()
		{
			if(nearby_built_kits.Count == 0) return;
			GUILayout.Label("Complete DIY kits nearby:", GUILayout.ExpandWidth(true));
			GUILayout.BeginVertical(Styles.white);
			built_scroll = GUILayout.BeginScrollView(built_scroll, GUILayout.Height(height), GUILayout.Width(width));
			KitInfo selected = null;
			foreach(var info in nearby_built_kits)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label(info.ToString(), Styles.boxed_label, GUILayout.ExpandWidth(true));
				if(GUILayout.Button(new GUIContent("Resources", "Transfer resources between the workshop and the assembled vessel"), Styles.active_button))
					selected = info;
				GUILayout.EndHorizontal();
			}
			if(selected != null) 
				setup_resource_transfer(selected);
			GUILayout.EndScrollView();
			GUILayout.EndVertical();
		}

		Vector2 unbuilt_scroll = Vector2.zero;
		void nearby_kits_pane()
		{
			if(nearby_unbuilt_kits.Count == 0) return;
			GUILayout.Label("Deployed DIY kits nearby:", GUILayout.ExpandWidth(true));
			GUILayout.BeginVertical(Styles.white);
			unbuilt_scroll = GUILayout.BeginScrollView(unbuilt_scroll, GUILayout.Height(height), GUILayout.Width(width));
			KitInfo selected = null;
			foreach(var info in nearby_unbuilt_kits)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label(info.ToString(), Styles.boxed_label, GUILayout.ExpandWidth(true));
				if(GUILayout.Button(new GUIContent("Add", "Add this kit to construction queue"), Styles.active_button))
					selected = info;
				GUILayout.EndHorizontal();
			}
			if(selected != null) 
				Queue.Enqueue(selected);
			GUILayout.EndScrollView();
			GUILayout.EndVertical();
		}

		Rect WindowPos = new Rect(width, height*4, Screen.width/4, Screen.height/4);
		void main_window(int WindowID)
		{
			GUILayout.BeginVertical();
			nearby_kits_pane();
			queue_pane();
			info_pane();
			built_kits_pane();
			if(GUILayout.Button("Close", Styles.close_button, GUILayout.ExpandWidth(true)))
				show_window = false;
			GUILayout.EndVertical();
			GUIWindowBase.TooltipsAndDragWindow(WindowPos);
		}

		string LockName = "";
		void OnGUI()
		{
			if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
			if(show_window && GUIWindowBase.HUD_enabled && vessel.isActiveVessel)
			{
				Styles.Init();
				if(transfer_resources)
				{
					if(transfer_list.Count > 0)
					{
						resources_window.Draw(string.Format("Resources: Workshop <> {0}", target_kit.KitName), transfer_list);
						if(resources_window.transferNow) 
						{
							if(target_kit.Recheck())
							{
								double dM, dC;
								transfer_list.TransferResources(host_resources, kit_resources, out dM, out dC);
								target_kit.Kit.Mass += (float)dM;
								target_kit.Kit.Cost += (float)dC;
							}
							resources_window.UnlockControls();
							resources_window.transferNow = false;
							transfer_resources = false;
						}
					}
					else transfer_resources = false;
				}
				else
				{
					Utils.LockIfMouseOver(LockName, WindowPos);
					WindowPos = GUILayout.Window(GetInstanceID(), 
					                             WindowPos, main_window, part.partInfo.title,
					                             GUILayout.Width(width),
					                             GUILayout.Height(height)).clampToScreen();
				}
			}
			else
			{
				Utils.LockIfMouseOver(LockName, WindowPos, false);
				resources_window.UnlockControls();
			}
		}
		#endregion
	}
}

