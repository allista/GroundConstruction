//   GroundAssemblyWorkshop.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri
using System.Linq;
using System.Collections.Generic;
using AT_Utils;
using UnityEngine;

namespace GroundConstruction
{
    public class GroundAssemblyWorkshop : AssemblyWorkshop
    {
        float distance_mod = -1;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            workshopType = WorkshopType.GROUND;
        }

        protected override List<IAssemblySpace> get_assembly_spaces()
        {
            var spaces = new List<IAssemblySpace>();
            foreach(var vsl in FlightGlobals.Vessels)
            {
                if(!vsl.loaded) continue;
                var vsl_spaces = VesselKitInfo.GetKitContainers<IAssemblySpace>(vsl).Where(s => s.Valid);
                if(vsl_spaces != null)
                    spaces.AddRange(vsl_spaces);
            }
            return spaces;
        }

        protected override bool check_host(AssemblyKitInfo task) => true;

        protected override void update_kits()
        {
            base.update_kits();
            var queued = get_queued_ids();
            foreach(var vsl in FlightGlobals.Vessels)
            {
                if(!vsl.loaded) continue;
                var containers = VesselKitInfo.GetKitContainers<IAssemblySpace>(vsl).Where(c => c.Valid);
                if(containers == null) continue;
                foreach(var vsl_kit in containers.SelectMany(c => c.GetKits()))
                {
                    if(vsl_kit != null && vsl_kit.Valid &&
                    vsl_kit != CurrentTask.Kit && !queued.Contains(vsl_kit.id) &&
                    (vessel.vesselTransform.position - vsl.vesselTransform.position).magnitude < GLB.MaxDistanceToWorkshop)
                        sort_task(new AssemblyKitInfo(vsl_kit));
                }
            }
        }

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

        protected override void info_pane()
        {
            GUILayout.BeginVertical();
            base.info_pane();
            if(distance_mod >= 0 && distance_mod < 1)
                GUILayout.Label(string.Format("Efficiency due to distance: {0:P1}", distance_mod),
                                Styles.fracStyle(distance_mod), GUILayout.ExpandWidth(true));
            GUILayout.EndVertical();
        }
    }
}
