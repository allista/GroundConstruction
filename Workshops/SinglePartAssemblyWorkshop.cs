//   SinglePartAssemblyWorkshop.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri
using System.Collections.Generic;
using System.Linq;

namespace GroundConstruction
{
    public class SinglePartAssemblyWorkshop : AssemblyWorkshop
    {
        protected override List<IAssemblySpace> get_assembly_spaces() =>
        part.FindModulesImplementing<IAssemblySpace>().Where(s => s.Valid).ToList();

        protected override bool check_host(AssemblyKitInfo task) =>
        base.check_host(task) && task.Module != null && task.Module.part == part;

        protected override void update_kits()
        {
            base.update_kits();
            if(vessel.loaded)
                update_kits(part.FindModulesImplementing<IKitContainer>());
        }
    }
}
