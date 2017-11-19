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
    public class PartKit : DIYKit
    {
        public new const string NODE_NAME = "PART_KIT";

        [Persistent] public string Name;
        [Persistent] public string Title;
        [Persistent] public uint   craftID;

        [Persistent] public float PartMass;
        [Persistent] public float KitMass;

        [Persistent] public float PartCost;
        [Persistent] public float KitCost;

        [Persistent] public float Complexity; //fraction

        public float ConstructionMassLeft { get { return Assembly.Complete? PartMass-Mass : PartMass-KitMass; } }
        public float AssemblyMassLeft { get { return Assembly.Complete? 0 : KitMass-Mass; } }

        public PartKit() {}

        public PartKit(Part part, bool assembled = true)
        {
            Name = part.partInfo.name;
            Title = part.partInfo.title;
            craftID  = part.craftID;
            var is_DIY_Kit = part.Modules.Contains<ModuleConstructionKit>();
            var res_mass = part.GetResourceMass();
            var dry_cost = Mathf.Max(part.DryCost(), 0);
            PartMass = part.mass+res_mass;
            PartCost = dry_cost+part.ResourcesCost();
            if(is_DIY_Kit)
            {
                KitMass = PartMass;
                KitCost = PartCost;
                Mass = assembled? KitMass : 0;
                Cost = assembled? KitCost : 0;
                Complexity = 1;
                Assembly.Completeness = assembled? 1 : 0;
                Construction.Completeness = 1;
            }
            else
            {
                Complexity = 1-1/((dry_cost/part.mass+part.Modules.Count*1000)*GLB.ComplexityFactor+1);
                var structure_mass = part.mass*(1-Complexity);
                var structure_cost = Mathf.Min(structure_mass/GLB.ConstructionResource.def.density*GLB.ConstructionResource.def.unitCost, dry_cost);
                KitMass = PartMass - structure_mass;
                KitCost = PartCost - structure_cost;
                Mass = assembled? KitMass : 0;
                Cost = assembled? KitCost : 0;
                if(assembled)
                    Assembly.Completeness = 1;
                else
                {
                    Assembly.Completeness = 0;
                    Assembly.TotalWork = (Complexity*Assembly.Resource.ComplexityWork + KitMass*Assembly.Resource.WorkPerMass)*3600;
                }
                Construction.TotalWork = (Complexity*Construction.Resource.ComplexityWork + structure_mass*Construction.Resource.WorkPerMass)*3600;
            }
        }


        public override double AssembleyRequirement(double work, out double energy, out int resource_id, out double resource_mass)
        {
            return Assembly.Requirement(work, 0, KitMass, out energy, out resource_id, out resource_mass);
        }

        public override double ConstructionRequerement(double work, out double energy, out int resource_id, out double resource_mass)
        {
            return Construction.Requirement(work, KitMass, PartMass, out energy, out resource_id, out resource_mass);
        }

        public override bool Assemble(double work)
        {
            if(Assembly.Completeness < 1)
            {
                Assembly.DoSomeWork(work);
                Mass = KitMass*Assembly.Completeness;
                Cost = KitCost*Assembly.Completeness;
                return false;
            }
            return true;
        }

        public override bool Construct(double work)
        {
            if(Construction.Completeness < 1)
            {
                Construction.DoSomeWork(work);
                Mass = Mathf.Lerp(KitMass, PartMass, Construction.Completeness);
                Cost = Mathf.Lerp(KitCost, PartCost, Construction.Completeness);
                return false;
            }
            return true;
        }
    }
}
