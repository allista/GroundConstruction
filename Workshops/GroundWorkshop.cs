//   GroundWorkshop.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri
using System.Linq;
using UnityEngine;
using AT_Utils;
using System.Collections.Generic;

namespace GroundConstruction
{
    public class GroundWorkshop : ConstructionWorkshop
    {
        [KSPField] public bool AutoEfficiency;
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Workshop Efficiency", guiFormat = "P1")]
        public float Efficiency = 1;
        float distance_mod = -1;

        public override float EffectiveWorkforce => workforce * distance_mod;

        public override string GetInfo()
        {
            if(AutoEfficiency)
                compute_part_efficiency();
            if(isEnabled)
            {
                update_max_workforce();
                return $"Efficiency: {Efficiency:P}\n" + $"Max Workforce: {max_workforce:F1} SK";
            }
            return "";
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            workshopType = WorkshopType.GROUND;
            if(!isEnabled)
            {
                enabled = false; 
                return;
            }
            if(AutoEfficiency)
                compute_part_efficiency();
        }

        void compute_part_efficiency()
        {
            Efficiency = 0;
            if(part.CrewCapacity == 0) 
                return;
            var useful_volume = (Metric.Volume(part) - part.mass) * GLB.PartVolumeFactor;
            if(useful_volume <= 0) 
                return;
            Efficiency = Mathf.Lerp(0, GLB.MaxGenericEfficiency,
                                    Mathf.Min(useful_volume / part.CrewCapacity / GLB.VolumePerKerbal, 1));
            if(Efficiency < GLB.MinGenericEfficiency) Efficiency = 0;
            if(Efficiency.Equals(0))
                this.EnableModule(false);
        }

        protected override void update_kits()
        {
            base.update_kits();
            var queued = get_queued_ids();
            foreach(var vsl in FlightGlobals.Vessels)
            {
                if(!vsl.loaded) continue;
                var containers = VesselKitInfo.GetKitContainers<IConstructionSpace>(vsl)?.Where(s => s.Valid);
                if(containers == null) 
                    continue;
                foreach(var vsl_kit in containers.SelectMany(c => c.GetKits()))
                {
                    if(vsl_kit != null && vsl_kit.Valid &&
                       vsl_kit != CurrentTask.Kit && !queued.Contains(vsl_kit.id) &&
                       (vessel.vesselTransform.position - vsl.vesselTransform.position).magnitude < GLB.MaxDistanceToWorkshop)
                        sort_task(new ConstructionKitInfo(vsl_kit));
                }
            }
        }

        protected override IEnumerable<Vessel> get_recyclable_vessels()
        {
            foreach(var vsl in FlightGlobals.Vessels)
            {
                if(vsl.loaded
                   && !vsl.isEVA
                   && (vsl != vessel || vsl.Parts.Count > 1)
                   && (vessel.vesselTransform.position - vsl.vesselTransform.position).magnitude < GLB.MaxDistanceToWorkshop)
                    yield return vsl;
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

        protected override bool can_construct()
        {
            if(!base.can_construct())
                return false;
            if(vessel.horizontalSrfSpeed > GLB.DeployMaxSpeed)
            {
                Utils.Message("Engineers cannot work while the workshop is moving.");
                return false;
            }
            return true;
        }

        protected override double do_some_work(double available_work)
        {
            if(distance_mod < 0)
                distance_mod = current_task_distance_mod();
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
            if(dist2kit(target) > GLB.MaxDistanceToWorkshop)
            {
                Utils.Message("{0} is too far away. Needs to be closer that {1}m",
                              target.Name, GLB.MaxDistanceToWorkshop);
                return false;
            }
            return true;
        }
        #endregion

        #region GUI
        protected override void info_pane()
        {
            GUILayout.BeginVertical();
            GUILayout.Label(string.Format("<color=silver>Efficiency:</color> <b>{0:P1}</b> " +
                                          "<color=silver>Workforce:</color> <b>{1:F1}</b>/{2:F1} SK",
                                          Efficiency, workforce, max_workforce),
                            Styles.boxed_label, GUILayout.ExpandWidth(true));
            if(distance_mod >= 0 && distance_mod < 1)
                GUILayout.Label(string.Format("Efficiency (due to distance): {0:P1}", distance_mod),
                                Styles.fracStyle(distance_mod), GUILayout.ExpandWidth(true));
            GUILayout.EndVertical();
        }
        #endregion
    }
}

