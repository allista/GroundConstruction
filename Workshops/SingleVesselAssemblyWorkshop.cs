//   SingleVesselAssemblyWorkshop.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri
using System.Collections.Generic;
using System.Linq;

namespace GroundConstruction
{
    public class SingleVesselAssemblyWorkshop : AssemblyWorkshop
    {
        protected override List<IAssemblySpace> get_assembly_spaces() =>
        VesselKitInfo.GetKitContainers<IAssemblySpace>(vessel)?.Where(s => s.Valid).ToList();

        protected override bool check_host(AssemblyKitInfo task) =>
        base.check_host(task) && task.Module != null && task.Module.vessel == vessel;

        protected override void update_kits()
        {
            base.update_kits();
            if(vessel.loaded)
                update_kits(VesselKitInfo
                            .GetKitContainers<IAssemblySpace>(vessel)?.Cast<IKitContainer>());
        }
    }
}
