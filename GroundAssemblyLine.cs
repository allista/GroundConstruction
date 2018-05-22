//   GroundAssemblyLine.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri
using System;
namespace GroundConstruction
{
    public class GroundAssemblyLine : AssemblyWorkshop
    {
        protected override IAssemblySpace find_assembly_space(VesselKit kit, bool best) => 
        best? find_best_assembly_space(kit, part) : find_assembly_space(kit, part);

        protected override void main_window(int WindowID)
        {
            throw new NotImplementedException();
        }
    }
}
