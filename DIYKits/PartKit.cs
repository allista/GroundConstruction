//   PartKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
    public sealed class PartKit : DIYKit
    {
        public new const string NODE_NAME = "PART_KIT";

        [Persistent] public uint   craftID;
        [Persistent] public float  Complexity = -1;

        public override bool Valid
        { get { return base.Valid && Complexity >= 0; } }

        public PartKit() {}

        public PartKit(Part part, bool assembled = true)
        {
            Name = part.partInfo.title;
            craftID  = part.craftID;
            var is_DIY_Kit = part.Modules.Contains<ModuleConstructionKit>();
            var res_mass = part.GetResourceMass();
            var dry_cost = Mathf.Max(part.DryCost(), 0);
            var part_mass = part.mass+res_mass;
            var part_cost = dry_cost+part.ResourcesCost();
            mass.Curve.Add(1, part_mass);
            mass.Curve.Add(1, part_cost);
            if(is_DIY_Kit)
            {
                Complexity = 1;
                if(assembled) 
                    SetComplete(assembled);
                else
                {
                    Assembly.TotalWork = total_work(Assembly, part_mass);
                    Construction.TotalWork = 0;
                    UpdateTotalWork();
                }
            }
            else
            {
                Complexity = 1-1/((dry_cost/part.mass+part.Modules.Count*1000)*GLB.ComplexityFactor+1);
                var structure_mass = part.mass*(1-Complexity);
                var structure_cost = Mathf.Min(structure_mass/GLB.ConstructionResource.def.density*GLB.ConstructionResource.def.unitCost, dry_cost);
                var kist_mass = part_mass - structure_mass;
                var kit_cost = part_cost - structure_cost;
                Construction.TotalWork = total_work(Construction, structure_mass);
                Assembly.TotalWork = total_work(Assembly, kist_mass);
                UpdateTotalWork();
                var frac = Assembly.TotalFraction();
                mass.Curve.Add(frac, kist_mass, 0, 0);
                mass.Curve.Add(frac, kit_cost, 0, 0);
                Assembly.SetComplete(assembled);
            }
        }

        double total_work(Task task, double end_mass)
        {
            return (Complexity*task.Resource.ComplexityWork + end_mass*Construction.Resource.WorkPerMass)*3600;
        }
    }
}
