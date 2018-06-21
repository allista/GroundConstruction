// SingleVesselConstructionWorkshop.cs
//
//  Author:
//       Allis Tauri allista@gmail.com
//
//  Copyright (c) 2018 Allis Tauri
using System.Linq;

namespace GroundConstruction
{
    public class SingleVesselConstructionWorkshop : ConstructionWorkshop
    {
        protected override void update_kits()
        {
            base.update_kits();
            var queued = get_queued_ids();
            if(vessel.loaded)
            {
                var containers = VesselKitInfo.GetKitContainers<IConstructionSpace>(vessel);
                if(containers != null)
                {
                    foreach(var vsl_kit in containers.SelectMany(c => c.GetKits()))
                    {
                        if(vsl_kit != null && vsl_kit.Valid &&
                           vsl_kit != CurrentTask.Kit && !queued.Contains(vsl_kit.id))
                            sort_task(new ConstructionKitInfo(vsl_kit));
                    }
                }
            }
        }

        protected override bool init_task(ConstructionKitInfo task) => true;
        protected override bool check_host(ConstructionKitInfo task) =>
        task.Module != null && task.Module.vessel == vessel;
    }
}
