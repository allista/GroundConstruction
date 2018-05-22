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

namespace GroundConstruction
{
    public class GroundWorkshop : ConstructionWorkshop
    {
        [KSPField] public bool AutoEfficiency;
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Workshop Efficiency", guiFormat = "P1")]
        public float Efficiency = 1;
        float distance_mod = -1;
        double loadedUT = -1;

        public override float EffectiveWorkforce { get { return workforce * distance_mod; } }

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
            GameEvents.onVesselGoOnRails.Add(onVesselPacked);
            GameEvents.onVesselGoOffRails.Add(onVesselUpacked);
        }

        protected override void OnDestroy()
        {
            GameEvents.onVesselGoOnRails.Remove(onVesselPacked);
            GameEvents.onVesselGoOffRails.Remove(onVesselUpacked);
            base.OnDestroy();
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

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if(!isEnabled) { enabled = false; return; }
            if(AutoEfficiency) compute_part_efficiency();
            if(Efficiency.Equals(0)) this.EnableModule(false);
            else if(HighLogic.LoadedSceneIsFlight)
                loadedUT = -1;
        }

        void compute_part_efficiency()
        {
            Efficiency = 0;
            if(part.CrewCapacity == 0) return;
            var usefull_volume = (Metric.Volume(part) - part.mass) * GLB.PartVolumeFactor;
            if(usefull_volume <= 0) return;
            Efficiency = Mathf.Lerp(0, GLB.MaxGenericEfficiency,
                                    Mathf.Min(usefull_volume / part.CrewCapacity / GLB.VolumePerKerbal, 1));
            if(Efficiency < GLB.MinGenericEfficiency) Efficiency = 0;
        }

		protected override  update_nearby_kits()
        {
            if(!FlightGlobals.ready) return;
            var queued = new HashSet<Guid>(Queue.Select(k => k.vesselID));
            unbuilt_kits.Clear();
            built_kits.Clear();
            foreach(var vsl in FlightGlobals.Vessels)
            {
                if(!vsl.loaded) continue;
                var containers = VesselKitInfo.GetKitContainers<IConstructionSpace>(vsl);
                if(containers == null) continue;
                foreach(var vsl_kit in containers.SelectMany(c => c.GetKits()))
                {
                    if(vsl_kit != null && vsl_kit.Valid && 
                       vsl_kit != CurrentTask.Kit && !queued.Contains(vsl.id) &&
                       (vessel.vesselTransform.position - vsl.vesselTransform.position).magnitude < GLB.MaxDistanceToWorkshop)
                    {
                        if(!vsl_kit.Complete)
                            unbuilt_kits.Add(new ConstructionKitInfo(vsl_kit));
                        else built_kits.Add(new ConstructionKitInfo(vsl_kit));
                    }
                }
            }
        }

        protected override void update_max_workforce()
        {
            base.update_max_workforce();
            max_workforce *= Efficiency;
        }

        protected override void update_workforce()
        {
            base.update_workforce();
            workforce *= Efficiency;
        }

		protected override bool init_task(ConstructionKitInfo task) => true;
		protected override bool check_host(ConstructionKitInfo task) => true;

		protected override bool can_construct()
        {
            if(!base.can_construct())
                return false;
            if(loadedUT < 0 || Planetarium.GetUniversalTime() - loadedUT < 3)
                return true;
            if(!vessel.Landed)
            {
                Utils.Message("Cannot construct unless landed.");
                return false;
            }
            if(vessel.horizontalSrfSpeed > GLB.DeployMaxSpeed)
            {
                Utils.Message("Cannot construct while mooving.");
                return false;
            }
            return true;
        }

        float dist2target(VesselKitInfo kit)
        { return (kit.Kit.Host.vessel.transform.position - vessel.transform.position).magnitude; }

        void update_distance_mod()
        {
            var dist = dist2target(CurrentTask);
            if(dist > GLB.MaxDistanceToWorkshop) distance_mod = 0;
            else distance_mod = Mathf.Lerp(1, GLB.MaxDistanceEfficiency,
                                           Mathf.Max((dist - GLB.MinDistanceToWorkshop) / GLB.MaxDistanceToWorkshop, 0));
        }

        protected override double do_some_work(double available_work)
        {
            if(distance_mod < 0)
                update_distance_mod();
            if(distance_mod.Equals(0))
            {
                Utils.Message("{0} is too far away.", CurrentTask.Name);
                if(start_next_item())
                    Utils.Message("Switching to the next kit in line.");
                return available_work;
            }
            return base.do_some_work(available_work * distance_mod) / distance_mod;
        }

        #region Target Actions
        protected override bool check_target_kit(ConstructionKitInfo target)
        {
            if(!base.check_target_kit(target))
                return false;
            if(dist2target(target) > GLB.MaxDistanceToWorkshop)
            {
                Utils.Message("{0} is too far away. Needs to be closer that {1}m",
                              target.Name, GLB.MaxDistanceToWorkshop);
                return false;
            }
            return true;
        }
        #endregion

        #region GUI
        public string ContainerStatus(IDeployableContainer container)
        {
            switch(container.State)
            {
            case ContainerDeplyomentState.IDLE:
                return "Idle";
            case ContainerDeplyomentState.DEPLOYING:
                return "Deploying...";
            case ContainerDeplyomentState.DEPLOYED:
                return "Deployed";
            }
            return "";
        }

		protected override void info_pane()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("<color=silver>Efficiency:</color> <b>{0:P1}</b> " +
                                          "<color=silver>Workforce:</color> <b>{1:F1}</b>/{2:F1} SK",
                                          Efficiency, workforce, max_workforce),
                            Styles.boxed_label, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        Vector2 unbuilt_scroll = Vector2.zero;
		protected override void unbuilt_kits_pane()
        {
            if(unbuilt_kits.Count == 0) return;
            GUILayout.Label("Unbuilt DIY kits:", Styles.label, GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(Styles.white);
			BeginScroll(unbuilt_kits.Count, ref unbuilt_scroll);
            ConstructionKitInfo add = null;
            IDeployableContainer deploy = null;
            foreach(var info in unbuilt_kits)
            {
                GUILayout.BeginHorizontal();
                info.Draw();
                set_highlighted_task(info);
                var depl = info.ConstructionSpace as IDeployableContainer;
                if(depl != null)
                {
                    if(depl.State == ContainerDeplyomentState.DEPLOYED)
                    {
                        if(GUILayout.Button(new GUIContent("Add", "Add this kit to construction queue"),
                                            Styles.enabled_button, GUILayout.ExpandWidth(false)))
                            add = info;
                    }
                    else if(depl.State != ContainerDeplyomentState.DEPLOYING)
                    {
                        if(GUILayout.Button(new GUIContent("Deploy", "Deploy this kit and fix it to the ground"),
                                            Styles.active_button, GUILayout.ExpandWidth(false)))
                            deploy = depl;
                    }
                    else
                        GUILayout.Label(ContainerStatus(depl), Styles.boxed_label, GUILayout.ExpandWidth(true));
                }
                else if(GUILayout.Button(new GUIContent("Add", "Add this kit to construction queue"),
                                         Styles.enabled_button, GUILayout.ExpandWidth(false)))
                    add = info;
                GUILayout.EndHorizontal();
            }
            if(add != null)
                Queue.Enqueue(add);
            else if(deploy != null)
                deploy.Deploy();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
        #endregion
    }
}

