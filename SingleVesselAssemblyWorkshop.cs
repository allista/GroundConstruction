//   SingleVesselAssemblyWorkshop.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri
using System;
using System.Collections.Generic;

namespace GroundConstruction
{
    public class SingleVesselAssemblyWorkshop : AssemblyWorkshop
    {
        protected override List<IAssemblySpace> get_assembly_spaces() =>
        VesselKitInfo.GetKitContainers<IAssemblySpace>(vessel);

        protected override bool check_host(AssemblyKitInfo task) =>
        task.Module != null && task.Module.vessel == vessel;

        protected override void update_kits()
        {
            base.update_kits();
            if(vessel.loaded)
                update_kits(VesselKitInfo.GetKitContainers<IKitContainer>(vessel));
        }

        protected override void main_window(int WindowID)
        {
            throw new NotImplementedException();
        }
    }
}
