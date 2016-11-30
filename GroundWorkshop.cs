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
			[Persistent] public Guid id;
			[Persistent] public string KitName;
			[Persistent] public float Completeness;

			public KitInfo(ModuleConstructionKit k)
			{
				id = k.vessel.id;
				KitName = k.KitName;
				Completeness = k.Completness;
			}

			public static ModuleConstructionKit GetKitFromVessel(Vessel vsl)
			{
				var kit_part = vsl.Parts.Find(p => p.Modules.Contains<ModuleConstructionKit>());
				return kit_part == null? null : kit_part.Modules.GetModule<ModuleConstructionKit>();
			}

			public ModuleConstructionKit FindKit()
			{
				var kit_vsl = FlightGlobals.FindVessel(id);
				return kit_vsl == null? null : GetKitFromVessel(kit_vsl);
			}
		}

		public class ConstructionQueue : Queue<Guid>, IConfigNode
		{
			public void Save(ConfigNode node)
			{
				foreach(var item in this)
					node.AddValue("Item", item);
			}

			public void Load(ConfigNode node)
			{
				Clear();
				var values = node.GetValues();
				for(int i = 0, len = values.Length; i < len; i++)
					Enqueue(new Guid(values[i]));
			}
		}

//		public class CompletedList : List<Guid>, IConfigNode
//		{
//			public void Save(ConfigNode node)
//			{ ForEach(item => node.AddValue("Item", item)); }
//
//			public void Load(ConfigNode node)
//			{
//				Clear();
//				var values = node.GetValues();
//				for(int i = 0, len = values.Length; i < len; i++)
//					Add(new Guid(values[i]));
//			}
//		}

		[KSPField(isPersistant = true)] public bool Working;
		[KSPField(isPersistant = true)] public double LastUpdateTime = -1;
		[KSPField(isPersistant = true)] public ConstructionQueue Queue = new ConstructionQueue();
//		[KSPField(isPersistant = true)] public CompletedList Completed = new CompletedList();
		[KSPField(isPersistant = true)] public Guid KitUnderConstruction = Guid.Empty;
		ModuleConstructionKit kit;
		float workers = 0;

		static ModuleConstructionKit get_kit_from_vessel(Vessel vsl)
		{
			var kit_part = vsl.Parts.Find(p => p.Modules.Contains<ModuleConstructionKit>());
			return kit_part == null? null : kit_part.Modules.GetModule<ModuleConstructionKit>();
		}

		static ModuleConstructionKit find_kit_by_vesselID(Guid vid)
		{
			var kit_vsl = FlightGlobals.FindVessel(vid);
			return kit_vsl == null? null : get_kit_from_vessel(kit_vsl);
		}

		bool get_next_kit()
		{
			while(Queue.Count > 0 && (kit == null || !kit.Valid))
				kit = find_kit_by_vesselID(Queue.Dequeue());
			var success = kit != null && kit.Valid;
			KitUnderConstruction = success ? kit.vessel.id : Guid.Empty;
			LastUpdateTime = -1;
			return success;
		}

		struct TargetKit
		{
			public Guid id;
			public string KitName;
			public float Completeness;
			public ModuleConstructionKit Kit;
			public TargetKit(ModuleConstructionKit k)
			{
				Kit = k;
				id = k.vessel.id;
				KitName = k.KitName;
				Completeness = k.Completness;
			}
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			LockName = "GroundWorkshop"+GetInstanceID();
			if(FlightGlobals.ready && KitUnderConstruction != Guid.Empty)
			{
				kit = find_kit_by_vesselID(KitUnderConstruction);
				if(kit == null) 
				{
					KitUnderConstruction = Guid.Empty;
					LastUpdateTime = -1;
				}
			}
			update_workers();
		}

		List<TargetKit> nearby_unbuilt_kits = new List<TargetKit>();
		List<TargetKit> nearby_built_kits = new List<TargetKit>();
		void update_nearby_kits()
		{
			if(!FlightGlobals.ready) return;
			nearby_unbuilt_kits.Clear();
			nearby_built_kits.Clear();
			foreach(var vsl in FlightGlobals.Vessels)
			{
				var vsl_kit = get_kit_from_vessel(vsl);
				if(vsl_kit != null && vsl_kit.Valid && vsl_kit.Deployed &&
				   vsl_kit != kit && !Queue.Contains(vsl.id) &&
				   (part.transform.position-vsl_kit.transform.position).magnitude < GLB.MaxDistanceToWorkshop)
				{
					if(vsl_kit.Completness < 1)
						nearby_unbuilt_kits.Add(new TargetKit(vsl_kit));
					else nearby_built_kits.Add(new TargetKit(vsl_kit));
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

		void Update()
		{
			if(Working) update_workers();
			if(show_window)
			{
				update_nearby_kits();
			}
		}

		void FixedUpdate()
		{
			if(!HighLogic.LoadedSceneIsFlight || !Working ||
			   kit == null || workers.Equals(0)) return;
			if(LastUpdateTime < 0) {}
			var deltaTime = GetDeltaTime();
			this.Log("deltaTime: {}, fixedDeltaTime {}", deltaTime, TimeWarp.fixedDeltaTime);//debug
			if(deltaTime < 0) return;
			//check current kit
			if(!kit.Valid || kit.vessel.mainBody != vessel.mainBody) 
			{ Working = get_next_kit(); return; }
			//calculate distance modifier
			var dist = (kit.transform.position-part.transform.position).magnitude;
			this.Log("dist: {}", dist);//debug
			if(dist > GLB.MaxDistanceToWorkshop)
			{ Working = get_next_kit(); return; }
			var dist_mod = Mathf.Lerp(1, GLB.MaxDistanceEfficiency, 
			                          Mathf.Max((dist-GLB.MinDistanceToWorkshop)/GLB.MaxDistanceToWorkshop, 0));
			//try to get the structure resource
			double work = workers*dist_mod*deltaTime;
			double required_ec;
			var required_mass = kit.RequiredMass(ref work, out required_ec);
			var have_mass = part.RequestResource(GLB.StructureResourceID, required_mass);
			this.Log("dist.mod: {}, work {}, n.mass {}, n.EC {}", dist_mod, work, required_mass, required_ec);//debug
			if(have_mass.Equals(0)) return;
			//try to get EC
			var have_ec = part.RequestResource(Utils.ElectricChargeID, required_ec);
			if(have_ec/required_ec < GLB.WorkshopShutdownThreshold) 
			{ 
				Utils.Message("Not enough energy. Construction of {} was paused.", kit.KitName);
				Working = false; 
				return; 
			}
			//do the work, if any
			have_mass *= have_ec/required_ec;
			work *= have_mass/required_mass;
			this.Log("work {}, mass {}/{}, EC {}/{}", work, have_mass, required_mass, have_ec, required_ec);//debug
			if(work > 0) 
			{
				kit.DoSomeWork(work);
				this.Log("{}.completeness {}", kit.KitName, kit.Completness);//debug
				if(kit.Completness >= 0)
					Working = get_next_kit();
			}
			//return unused structure resource
			if(have_mass < required_mass)
				part.RequestResource(GLB.StructureResourceID, have_mass-required_mass);
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

		#region GUI
		ResourceTransferWindow resources_window = new ResourceTransferWindow();

		bool show_window;
		[KSPEvent(guiName = "Construction Window", guiActive = true, active = true)]
		public void ToggleConstructionWindow()
		{ show_window = !show_window; }

		void info_pane()
		{

		}

		Vector2 queue_scroll;
		void queue_pane()
		{

		}

		bool transfer_resources;
		ResourceManifestList transfer_list = new ResourceManifestList();
		VesselResources host_resources, kit_resources;
		ModuleConstructionKit target_kit;

		void setup_resource_transfer(TargetKit target)
		{
			
		}

		Vector2 complete_scroll;
		void complete_kits_pane()
		{

		}

		Vector2 nearby_scroll;
		void nearby_kits_pane()
		{

		}

		const float width = 300;
		const float height = 200;
		Rect WindowPos = new Rect(width, height, Screen.width/4, Screen.height/4);
		void main_window(int WindowID)
		{
			GUILayout.BeginVertical();
			nearby_kits_pane();
			queue_pane();
			info_pane();
			complete_kits_pane();
			if(GUILayout.Button("Close", Styles.close_button, GUILayout.ExpandWidth(true)))
				show_window = false;
			GUILayout.EndVertical();
			GUIWindowBase.TooltipsAndDragWindow(WindowPos);
		}

		string LockName = "";
		void OnGUI()
		{
			if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
			if(show_window)
			{
				Styles.Init();
				if(transfer_resources && transfer_list.Count > 0)
				{
					resources_window.Draw(string.Format("Resources: Workshop <> {0}", kit.KitName), transfer_list);
					if(resources_window.transferNow) 
					{
						double dM, dC;
						transfer_list.TransferResources(host_resources, kit_resources, out dM, out dC);
						target_kit.kit.Mass += (float)dM;
						target_kit.kit.Cost += (float)dC;
						resources_window.transferNow = false;
						resources_window.UnlockControls();
						transfer_resources = false;
					}
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

