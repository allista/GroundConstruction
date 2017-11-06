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

namespace GroundConstruction
{
    public class GroundWorkshop : WorkshopBase
	{
		static Globals GLB { get { return Globals.Instance; } }

		public class KitInfo : VesselInfo
		{
			[Persistent] public string KitName = string.Empty;

			public ModuleConstructionKit Module { get; private set; }
			public VesselKit Kit { get { return ModuleValid? Module.kit: null; } }

			public bool ModuleValid { get { return Module != null && Module.Valid; } }

			public KitInfo() {}
			public KitInfo(ModuleConstructionKit kit_module)
			{
				vesselID = kit_module.vessel.id;
				Module = kit_module;
				KitName = kit_module.KitName;
			}

            protected KitInfo(KitInfo other) : base(other)
            {
                Module = other.Module;
                KitName = other.KitName;
            }

            public new KitInfo Clone()
            { 
                return new KitInfo(this); 
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
                var kit_vsl = GetVessel();
				Module = kit_vsl == null? null : GetKitFromVessel(kit_vsl);
//				Utils.Log("FindKit: vsl {}, module {}, valid {}", kit_vsl, Module, ModuleValid);//debug
				return Module;
			}

			public string StructureStatus
			{
				get
				{
					return Module.kit.WorkLeft > 0? 
						string.Format("Reqires: {0:F1} {1}", 
						              Module.kit.StructureLeft, GLB.StructureResourceName) : "";
				}
			}

			public override string ToString()
			{
				var s = string.Format("\"{0}\"", KitName);
				if(ModuleValid)
				{
					s += Module.kit.WorkLeft > 0? 
						string.Format(" needs: {0:F1} {1}, {2:F1} SKH. {3}", 
						              Module.kit.StructureLeft, GLB.StructureResourceName,
						              Module.kit.WorkLeft/3600, 
						              Module.KitStatus) : 
						" Complete.";
				}
				return s;
			}
		}

		[KSPField] public bool AutoEfficiency;
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Workshop Efficiency", guiFormat = "P1")] 
		public float Efficiency = 1;
        float distance_mod = -1;

		[KSPField(isPersistant = true)] public PersistentQueue<KitInfo> Queue = new PersistentQueue<KitInfo>();
		[KSPField(isPersistant = true)] public KitInfo KitUnderConstruction = new KitInfo();

        double loadedUT = -1;

        public float EffectiveWorkforce { get { return workforce * distance_mod; } }

        public WorkshopManager Manager;

		public override string GetInfo()
		{ 
			if(AutoEfficiency) compute_part_efficiency();
            if(isEnabled && Efficiency > 0)
            {
                update_max_workforce();
                return string.Format("Efficiency: {0:P}\n" +
                                     "Max Workforce: {1:F1} SK", 
                                     Efficiency, max_workforce);
            }
            return "";
		}

		public override void OnAwake()
		{
			base.OnAwake();
			resources_window = gameObject.AddComponent<ResourceTransferWindow>();
			crew_window = gameObject.AddComponent<CrewTransferWindow>();
            GameEvents.onVesselCrewWasModified.Add(update_and_checkin);
            GameEvents.onVesselGoOnRails.Add(onVesselPacked);
            GameEvents.onVesselGoOffRails.Add(onVesselUpacked);
		}

		void OnDestroy()
		{
			Utils.LockIfMouseOver(LockName, WindowPos, false);
			Destroy(resources_window);
			Destroy(crew_window);
            GameEvents.onVesselCrewWasModified.Remove(update_and_checkin);
            GameEvents.onVesselGoOnRails.Remove(onVesselPacked);
            GameEvents.onVesselGoOffRails.Remove(onVesselUpacked);
		}

        void onVesselPacked(Vessel vsl)
        {
            if(vsl != vessel) return;
            loadedUT = -1;
        }

        void onVesselUpacked(Vessel vsl)
        {
            if(vsl != vessel) return;
            loadedUT = Planetarium.GetUniversalTime();
        }

        void update_and_checkin(Vessel vsl)
        {
            if(vsl != null && vsl == vessel &&
               part.started && isEnabled && Efficiency > 0)
            {
                if(Working && KitUnderConstruction.Recheck()) 
                    update_ETA();
                else 
                    update_workforce();
                checkin();
            }
        }

        void checkin()
        {
            if(Manager != null)
                Manager.CheckinWorkshop(this);
        }

		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			node.AddValue("WindowPos", new Vector4(WindowPos.x, WindowPos.y, WindowPos.width, WindowPos.height));
            node.AddValue("Workforce_Display", Workforce_Display);
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			var wpos = node.GetValue("WindowPos");
			if(wpos != null)
			{
				var vpos = ConfigNode.ParseVector4(wpos);
				WindowPos = new Rect(vpos.x, vpos.y, vpos.z, vpos.w);
			}
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
            if(!isEnabled) { enabled = false; return; }
			LockName = "GroundWorkshop"+GetInstanceID();
			if(AutoEfficiency) compute_part_efficiency();
			if(Efficiency.Equals(0)) this.EnableModule(false);
			else if(HighLogic.LoadedSceneIsFlight)
			{
                loadedUT = -1;
				update_workforce();
                update_max_workforce();
			}
		}

		void compute_part_efficiency()
		{
			Efficiency = 0;
			if(part.CrewCapacity == 0) return;
			var usefull_volume = (Metric.Volume(part)-part.mass)*GLB.PartVolumeFactor;
			if(usefull_volume <= 0) return;
			Efficiency = Mathf.Lerp(0, GLB.MaxGenericEfficiency, 
			                        Mathf.Min(usefull_volume/part.CrewCapacity/GLB.VolumePerKerbal, 1));
			if(Efficiency < GLB.MinGenericEfficiency) Efficiency = 0;
		}

		void update_queue()
		{
			if(Queue.Count == 0) return;
            Queue = new PersistentQueue<KitInfo>(Queue.Where(kit => kit.Recheck() && kit.Module.Completeness < 1));
		}

		List<KitInfo> nearby_unbuilt_kits = new List<KitInfo>();
		List<KitInfo> nearby_built_kits = new List<KitInfo>();
		void update_nearby_kits()
		{
			if(!FlightGlobals.ready) return;
			var queued = new HashSet<Guid>(Queue.Select(k => k.vesselID));
			nearby_unbuilt_kits.Clear();
			nearby_built_kits.Clear();
			foreach(var vsl in FlightGlobals.Vessels)
			{
				if(!vsl.loaded) continue;
				var vsl_kit = KitInfo.GetKitFromVessel(vsl);
				if(vsl_kit != null && vsl_kit.Valid &&
				   vsl_kit != KitUnderConstruction.Module && !queued.Contains(vsl.id) &&
				   (vessel.vesselTransform.position-vsl.vesselTransform.position).magnitude < GLB.MaxDistanceToWorkshop)
				{
					if(vsl_kit.Completeness < 1)
						nearby_unbuilt_kits.Add(new KitInfo(vsl_kit));
					else nearby_built_kits.Add(new KitInfo(vsl_kit));
				}
			}
		}

        protected override void update_max_workforce()
        {
            base.update_max_workforce();
            max_workforce *= Efficiency;
        }

		void update_workforce()
		{
            base.update_workforce<ConstructionSkill>();
			workforce *= Efficiency;
		}

		bool can_construct()
		{
            if(loadedUT < 0 || Planetarium.GetUniversalTime()-loadedUT < 3)
                return true;
			if(!vessel.Landed)
			{
				Utils.Message("Cannot construct unless landed.");
				return false;
			}
			if(workforce.Equals(0))
			{
				Utils.Message("No engineers in the workshop.");
				return false;
			}
			if(vessel.horizontalSrfSpeed > GLB.DeployMaxSpeed)
			{
				Utils.Message("Cannot construct while mooving.");
				return false;
			}
			return true;
		}

		float dist2target(KitInfo kit)
		{ return (kit.Module.vessel.transform.position-vessel.transform.position).magnitude; }

		void update_distance_mod()
		{
			var dist = dist2target(KitUnderConstruction);
			if(dist > GLB.MaxDistanceToWorkshop) distance_mod = 0;
			else distance_mod = Mathf.Lerp(1, GLB.MaxDistanceEfficiency, 
			                               Mathf.Max((dist-GLB.MinDistanceToWorkshop)/GLB.MaxDistanceToWorkshop, 0));
		}

		void update_ETA()
		{
			update_workforce();
			update_distance_mod();
            var lastEndUT = EndUT;
			if(workforce > 0 && distance_mod > 0)
			{
				if(LastUpdateTime < 0) 
					LastUpdateTime = Planetarium.GetUniversalTime();
                KitUnderConstruction.Module.CheckinWorker(this);
                var ETA = KitUnderConstruction.Module.GetETA();
				if(ETA > 0)
                {
                    var time = Planetarium.GetUniversalTime();
                    EndUT = time+ETA;
                    ETA_Display = "Time left: "+KSPUtil.PrintTimeCompact(ETA, false);
                }
            }
            else 
                EndUT = -1;
			if(EndUT < 0) 
                ETA_Display = "Stalled...";
            if(Math.Abs(EndUT-lastEndUT) > 1)
                checkin();
		}

		void Update()
		{
            if(!HighLogic.LoadedSceneIsFlight) return;
            //highlight kit under the mouse
            disable_highlights();
            if(highlight_kit != null)
            {
                if(highlight_kit.Module != null)
                {
                    highlight_kit.Module.part.HighlightAlways(Color.yellow);
                    highlighted_kits.Add(highlight_kit);
                }
            }
            highlight_kit = null;
            //check the kit under construction
            if(!FlightDriver.Pause && FlightGlobals.ready && Time.timeSinceLevelLoad > 1)
            {
                if(KitUnderConstruction.Valid)
                {
                    if(KitUnderConstruction.Recheck())
                    {
                        //update ETA if working
            			if(Working)
            			{
            				if(can_construct()) update_ETA();
            				else stop();
            			}
                        else if(KitUnderConstruction.Module.Completeness >= 1)
                            stop(true);
                    }
                    else 
                        stop(true);
                }
            }
            //if UI is opened, update info about nearby kits
			if(show_window)
			{
				update_queue();
				update_nearby_kits();
			}
		}

        protected override void start()
		{
            base.start();
            if(KitUnderConstruction.Recheck()) 
                update_ETA();
            checkin();
		}

        protected override void stop(bool reset = false)
		{
            base.stop(reset);
			distance_mod = -1;
            if(KitUnderConstruction.Recheck())
                KitUnderConstruction.Module.CheckoutWorker(this);
            if(reset)
                KitUnderConstruction = new KitInfo();
            checkin();
		}

		bool start_next_kit()
		{
			KitUnderConstruction = new KitInfo();
			if(Queue.Count > 0)
			{
				while(Queue.Count > 0 && !KitUnderConstruction.ModuleValid)
				{
					KitUnderConstruction = Queue.Dequeue();
					KitUnderConstruction.FindKit();
				}
				if(KitUnderConstruction.ModuleValid) 
				{
					start();
					return true;
				}
			}
            stop(true);
			return false;
		}

        public void ConstructThisKit(KitInfo kit)
        {
            if(KitUnderConstruction.vesselID == kit.vesselID)
                return;
            if(KitUnderConstruction.Valid)
                Queue.Enqueue(KitUnderConstruction);
            KitUnderConstruction = kit.Clone();
            if(KitUnderConstruction.Recheck()) 
                start();
            else 
                stop(true);
        }

		double do_some_work(double available_work)
		{
			if(distance_mod < 0) update_distance_mod();
			if(distance_mod.Equals(0))
			{ 
				Utils.Message("{0} is too far away.", KitUnderConstruction.KitName);
				if(start_next_kit()) Utils.Message("Switching to the next kit in line.");
				return available_work; 
			}
			//try to get the structure resource
			var max_work = available_work*distance_mod;
			var work = max_work;
			double required_ec;
			var required_res = KitUnderConstruction.Module.RequiredMass(ref work, out required_ec)/GLB.StructureResource.density;
			var have_res = part.RequestResource(GLB.StructureResourceID, required_res);
			if(required_res > 0 && have_res.Equals(0)) 
			{
				Utils.Message("Not enough {0}. Construction of {1} was put on hold.", 
				              GLB.StructureResourceName, KitUnderConstruction.KitName);
				stop();
				return 0;
			}
			//try to get EC
			var have_ec = part.RequestResource(Utils.ElectricChargeID, required_ec);
			if(have_ec/required_ec < GLB.WorkshopShutdownThreshold) 
			{ 
				Utils.Message("Not enough energy. Construction of {0} was put on hold.", 
				              KitUnderConstruction.KitName);
				stop();
				return 0;
			}
			//do the work, if any
			if(required_res > 0 && required_ec > 0)
			{
				have_res *= have_ec/required_ec;
				work *= have_res/required_res;
			}
			else work = 0;
			KitUnderConstruction.Module.DoSomeWork(work);
			if(KitUnderConstruction.Module.Completeness >= 1)
			{
				KitUnderConstruction.Module.AllowLaunch();
				start_next_kit();
			}
			//return unused structure resource
			if(have_res < required_res)
				part.RequestResource(GLB.StructureResourceID, have_res-required_res);
			return available_work-work/distance_mod;
		}

		void FixedUpdate()
		{
			if(!HighLogic.LoadedSceneIsFlight || !Working || workforce.Equals(0)) return;
            var deltaTime = get_delta_time();
			if(deltaTime < 0) return;
			//check current kit
			if(!KitUnderConstruction.Recheck() && !start_next_kit()) return;
			var available_work = workforce*deltaTime;
			while(Working && available_work > TimeWarp.fixedDeltaTime/10)
				available_work = do_some_work(available_work);
			if(deltaTime > TimeWarp.fixedDeltaTime*2)
			{
				update_ETA();
                checkin();
			}
		}

		#region Target Actions
		KitInfo target_kit;
		void check_target_kit(KitInfo target)
		{
			target_kit = null;
			if(!target.Recheck()) return;
			if(dist2target(target) > GLB.MaxDistanceToWorkshop)
			{
				Utils.Message("{0} is too far away. Needs to be closer that {1}m", 
				              target.KitName, GLB.MaxDistanceToWorkshop);
				return;
			}
			target_kit = target;
		}
		#endregion

		#region Resource Transfer
		readonly ResourceManifestList transfer_list = new ResourceManifestList();
		VesselResources host_resources, kit_resources;

		void setup_resource_transfer(KitInfo target)
		{
			check_target_kit(target);
			if(target_kit == null) return;
			host_resources = new VesselResources(vessel);
			kit_resources = target_kit.Module.GetConstructResources();
			transfer_list.NewTransfer(host_resources, kit_resources);
			if(transfer_list.Count > 0)
            {
				resources_window.Show(true);
                resources_window.TransferAction = delegate
                {
                    double dM, dC;
                    transfer_list.TransferResources(host_resources, kit_resources, out dM, out dC);
                    target_kit.Kit.Mass += (float)dM;
                    target_kit.Kit.Cost += (float)dC;
                };
            }
			else
			{
                resources_window.TransferAction = null;
				host_resources = null;
				kit_resources = null;
				target_kit = null;
			}
		}
		#endregion

		#region Resource Transfer
		int kit_crew_capacity;

		void setup_crew_transfer(KitInfo target)
		{
			check_target_kit(target);
			if(target_kit == null) return;
			target_kit.Module.CrewSource = vessel;
			target_kit.Module.KitCrew = new List<ProtoCrewMember>();
			kit_crew_capacity = target_kit.Module.KitCrewCapacity();
			crew_window.Show(true);
		}
		#endregion

		#region GUI
		ResourceTransferWindow resources_window;
		CrewTransferWindow crew_window;
		HashSet<KitInfo> highlighted_kits = new HashSet<KitInfo>();
		KitInfo highlight_kit;

		[KSPField(isPersistant = true)] public bool show_window;
		const float width = 550;
		const float height = 60;

		[KSPEvent(guiName = "Construction Window", guiActive = true, active = true)]
		public void ToggleConstructionWindow()
		{ show_window = !show_window; }

		void disable_highlights()
		{
			if(highlighted_kits.Count > 0)
			{
				foreach(var kit in highlighted_kits)
				{
					if(kit.Module != null &&
					   (highlight_kit == null ||
					    kit.Module != highlight_kit.Module))
                    {
						kit.Module.part.SetHighlightDefault();
                    }
				}
				highlighted_kits.Clear();
			}
		}

		void update_highlight_kit(KitInfo info)
		{
			if(Event.current.type == EventType.Repaint &&  
			   Utils.MouseInLastElement()) 
				highlight_kit = info;
		}

        void info_pane()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("<color=silver>Efficiency:</color> <b>{0:P1}</b> " +
                                          "<color=silver>Workforce:</color> <b>{1:F1}</b>/{2:F1} SK", 
                                          Efficiency, workforce, max_workforce), 
                            Styles.boxed_label, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

		void construction_pane()
		{
			if(KitUnderConstruction.ModuleValid) 
			{
				GUILayout.BeginVertical(Styles.white);
				var label = (Working? "Constructing: " : "Paused: ")+KitUnderConstruction.KitName;
				GUILayout.Label(label, Working? Styles.green : Styles.yellow, GUILayout.ExpandWidth(true));
				GUILayout.BeginHorizontal();
				if(KitUnderConstruction.Module.Completeness < 1)
					GUILayout.Label(KitUnderConstruction.StructureStatus, Styles.white, GUILayout.ExpandWidth(true));
				GUILayout.Label(KitUnderConstruction.Module.KitStatus, Styles.white, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				if(KitUnderConstruction.Module.Completeness < 1)
					GUILayout.Label(KitUnderConstruction.Module.PartStatus, Styles.white, GUILayout.ExpandWidth(true));
				if(Working)
				{
					if(distance_mod < 1)
						GUILayout.Label(string.Format("Efficiency (due to distance): {0:P1}", distance_mod), Styles.fracStyle(distance_mod), GUILayout.ExpandWidth(true));
                    GUILayout.BeginHorizontal();
					GUILayout.Label(ETA_Display, Styles.boxed_label, GUILayout.ExpandWidth(true));
                    if(EndUT > 0 &&
                       TimeWarp.fetch != null &&
                       GUILayout.Button(ProtoGroundWorkshop.WarpToButton, Styles.enabled_button, GUILayout.ExpandWidth(false)))
                        TimeWarp.fetch.WarpTo(EndUT);
                    GUILayout.EndHorizontal();
                }
				if(GUILayout.Button(new GUIContent("Stop", " And move back to the Queue"), 
				                    Styles.danger_button, GUILayout.ExpandWidth(true)))
				{
					Queue.Enqueue(KitUnderConstruction);
                    stop(true);
				}
				GUILayout.EndVertical();
			}
			if(KitUnderConstruction.ModuleValid || Queue.Count > 0)
			{
				if(Utils.ButtonSwitch("Construct Kit", ref Working, "Start, Pause or Resume construction", GUILayout.ExpandWidth(true)))
				{ 
					if(Working && can_construct()) start(); 
					else stop();
				}
			}
		}

		Vector2 queue_scroll = Vector2.zero;
		void queue_pane()
		{
			if(Queue.Count > 0)
			{
                GUILayout.Label("Construction Queue", Styles.label, GUILayout.ExpandWidth(true));
				GUILayout.BeginVertical(Styles.white);
				queue_scroll = GUILayout.BeginScrollView(queue_scroll, GUILayout.Height(height), GUILayout.Width(width));
				KitInfo del = null;
				KitInfo up = null;
				foreach(var info in Queue) 
				{
					GUILayout.BeginHorizontal();
					GUILayout.Label(info.ToString(), Styles.boxed_label, GUILayout.ExpandWidth(true));
					update_highlight_kit(info);
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
		}

		Vector2 built_scroll = Vector2.zero;
		void built_kits_pane()
		{
			if(nearby_built_kits.Count == 0) return;
            GUILayout.Label("Completed DIY kits nearby:", Styles.label, GUILayout.ExpandWidth(true));
			GUILayout.BeginVertical(Styles.white);
			built_scroll = GUILayout.BeginScrollView(built_scroll, GUILayout.Height(height), GUILayout.Width(width));
			KitInfo crew = null;
			KitInfo resources = null;
			KitInfo launch = null;
			foreach(var info in nearby_built_kits)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label(info.ToString(), Styles.boxed_label, GUILayout.ExpandWidth(true));
				update_highlight_kit(info);
				if(GUILayout.Button(new GUIContent("Resources", "Transfer resources between the workshop and the assembled vessel"), 
				                    Styles.active_button, GUILayout.ExpandWidth(false)))
					resources = info;
				if(GUILayout.Button(new GUIContent("Crew", "Select crew for the assembled vessel"), 
				                    Styles.active_button, GUILayout.ExpandWidth(false)))
					crew = info;
				if(GUILayout.Button(new GUIContent("Launch", "Launch assembled vessel"), 
				                    Styles.danger_button, GUILayout.ExpandWidth(false)))
					launch = info;
				GUILayout.EndHorizontal();
			}
			if(resources != null) 
				setup_resource_transfer(resources);
			if(crew != null)
				setup_crew_transfer(crew);
			if(launch != null && launch.Recheck())
				launch.Module.Launch();
			GUILayout.EndScrollView();
			GUILayout.EndVertical();
		}

		Vector2 unbuilt_scroll = Vector2.zero;
		void nearby_kits_pane()
		{
			if(nearby_unbuilt_kits.Count == 0) return;
            GUILayout.Label("Uncompleted DIY kits nearby:", Styles.label, GUILayout.ExpandWidth(true));
			GUILayout.BeginVertical(Styles.white);
			unbuilt_scroll = GUILayout.BeginScrollView(unbuilt_scroll, GUILayout.Height(height), GUILayout.Width(width));
			KitInfo add = null;
			KitInfo deploy = null;
			foreach(var info in nearby_unbuilt_kits)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label(info.ToString(), Styles.boxed_label, GUILayout.ExpandWidth(true));
				update_highlight_kit(info);
				if(info.Module.Deployed)
				{
					if(GUILayout.Button(new GUIContent("Add", "Add this kit to construction queue"), 
					                    Styles.enabled_button, GUILayout.ExpandWidth(false)))
						add = info;
				}
				else if(!info.Module.Deploying)
				{
					if(GUILayout.Button(new GUIContent("Deploy", "Deploy this kit and fix it to the ground"), 
					                    Styles.active_button, GUILayout.ExpandWidth(false)))
						deploy = info;
				}
				GUILayout.EndHorizontal();
			}
			if(add != null) 
				Queue.Enqueue(add);
			else if(deploy != null && deploy.Recheck())
				deploy.Module.Deploy();
			GUILayout.EndScrollView();
			GUILayout.EndVertical();
		}

		Rect WindowPos = new Rect((Screen.width-width)/2, Screen.height/4, width, height*4);
		void main_window(int WindowID)
		{
			GUILayout.BeginVertical();
            info_pane();
			nearby_kits_pane();
			queue_pane();
			construction_pane();
			built_kits_pane();
			if(GUILayout.Button("Close", Styles.close_button, GUILayout.ExpandWidth(true)))
				show_window = false;
			GUILayout.EndVertical();
			GUIWindowBase.TooltipsAndDragWindow();
		}

		string LockName = ""; //inited OnStart
		void OnGUI()
		{
			if(Time.timeSinceLevelLoad < 3) return;
			if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
			Styles.Init();
			if(show_window && GUIWindowBase.HUD_enabled && vessel.isActiveVessel)
			{
				Styles.Init();
				Utils.LockIfMouseOver(LockName, WindowPos);
				WindowPos = GUILayout.Window(GetInstanceID(), 
				                             WindowPos, main_window, part.partInfo.title,
				                             GUILayout.Width(width),
				                             GUILayout.Height(height)).clampToScreen();
				if(target_kit != null && target_kit.Recheck())
				{
					resources_window.Draw(string.Format("Transfer resources to {0}", target_kit.KitName), transfer_list);
					crew_window.Draw(vessel.GetVesselCrew(), target_kit.Module.KitCrew, kit_crew_capacity);
				}
				else
				{
					resources_window.UnlockControls();
					crew_window.UnlockControls();
				}
			}
			else
			{
				Utils.LockIfMouseOver(LockName, WindowPos, false);
				resources_window.UnlockControls();
				crew_window.UnlockControls();
			}
		}
		#endregion
	}
}

