//   PartKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
    public sealed class PartKit : DIYKit
    {
        public new const string NODE_NAME = "PART_KIT";

        [Persistent] public uint craftID;
        [Persistent] public float Complexity;
        [Persistent] public bool PseudoPart;

        public PartKit() { }

        public PartKit(string name, float mass, float cost, bool assembled)
        {
            Name = name;
            craftID = 0;
            PseudoPart = true;
            Mass.Add(1, mass);
            Cost.Add(1, cost);
            if(assembled)
            {
                Complexity = 0;
                Construction.TotalWork = total_work(Construction, mass);
                Assembly.TotalWork = 0;
                update_total_work();
                SetStageComplete(ASSEMBLY, true);
            }
            else
            {
                Complexity = 1;
                Assembly.TotalWork = total_work(Assembly, mass);
                Construction.TotalWork = 0;
                update_total_work();
            }
        }

        public PartKit(Part part, bool assembled = true)
        {
            Name = part.partInfo.title;
            craftID = part.craftID;
            PseudoPart = false;
            var is_DIY_Kit = part.Modules.Contains<DeployableKitContainer>();
            var dry_cost = Mathf.Max(part.DryCost(), 0);
            Mass.Add(1, part.mass);
            Cost.Add(1, dry_cost);
            if(is_DIY_Kit)
            {
                Complexity = 1;
                Assembly.TotalWork = total_work(Assembly, part.mass);
                Construction.TotalWork = 0;
                update_total_work();
                if(assembled)
                    SetComplete(assembled);
            }
            else
            {
                Complexity = Mathf.Clamp01(1 - 1 / ((dry_cost / part.mass + GLB.IgnoreModules.SizeOfDifference(part.Modules) * 1000) * GLB.ComplexityFactor + 1));
                var structure_mass = part.mass * Mathf.Clamp01(1 - Complexity);
                var structure_cost = Mathf.Min(structure_mass / GLB.ConstructionResource.def.density * GLB.ConstructionResource.def.unitCost, dry_cost*0.9f);
                var kit_mass = part.mass - structure_mass;
                var kit_cost = dry_cost - structure_cost;
                Construction.TotalWork = total_work(Construction, structure_mass);
                Assembly.TotalWork = total_work(Assembly, kit_mass);
                update_total_work();
                var frac = (float)(Assembly.TotalWork / TotalWork);
                //Utils.Log("{}: C {}, frac {}, kit_mass {}, kit_cost {}", 
                          //Complexity, Name, frac, kit_mass, kit_cost);//debug
                Mass.Add(frac, kit_mass);
                Cost.Add(frac, kit_cost);
                SetStageComplete(ASSEMBLY, assembled);
            }
        }

        double total_work(JobStage task, double end_mass) =>
            (Complexity * task.Resource.ComplexityWork + task.Resource.WorkPerMass)
            * end_mass
            * 3600;

        //deprecated config conversion
        public override void Load(ConfigNode node)
        {
            base.Load(node);
            if(!Construction.Valid)
                CurrentIndex = -1;
            else if(node.HasValue("Completeness"))
            {
                CurrentIndex = CONSTRUCTION;
                Assembly.TotalWork = Assembly.WorkDone = Construction.TotalWork;
                update_total_work();
                float v;
                var frac = (float)(Assembly.TotalWork / TotalWork);
                var val = node.GetValue("Title");
                if(!string.IsNullOrEmpty(val))
                    Name = val;
                val = node.GetValue("PartMass");
                if(float.TryParse(val, out v))
                    Mass.Add(1, v);
                val = node.GetValue("PartCost");
                if(float.TryParse(val, out v))
                    Cost.Add(1, v);
                val = node.GetValue("KitMass");
                if(float.TryParse(val, out v))
                    Mass.Add(frac, v);
                val = node.GetValue("KitCost");
                if(float.TryParse(val, out v))
                    Cost.Add(frac, v);
                frac = (float)get_fraction();
                Mass.Update(frac);
                Cost.Update(frac);
            }
        }
    }
}
