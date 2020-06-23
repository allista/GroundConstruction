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

        public PartKit(
            string name,
            float mass,
            float cost,
            float assembly_fraction,
            float additional_work,
            bool assembled
        )
        {
            Name = name;
            craftID = 0;
            PseudoPart = true;
            Mass.Add(1, mass);
            Cost.Add(1, cost);
            Complexity = assembly_fraction;
            var assembly_mass = mass * assembly_fraction;
            var assembly_work = additional_work * assembly_fraction;
            Assembly.TotalWork = total_work(Assembly, assembly_mass) + assembly_work;
            Construction.TotalWork = total_work(Construction, mass - assembly_mass)
                                     + additional_work
                                     - assembly_work;
            update_total_work();
            if(assembled)
                SetStageComplete(ASSEMBLY, true);
        }

        public PartKit(Part part, bool assembled = true)
        {
            Name = part.partInfo.title;
            craftID = part.craftID;
            PseudoPart = false;
            part.needPrefabMass = true;
            part.UpdateMass();
            var kit_container = part.Modules.GetModule<DeployableKitContainer>();
            var dry_cost = Mathf.Max(part.DryCost(), 0);
            float kit_mass, kit_cost;
            Mass.Add(1, part.mass);
            Cost.Add(1, dry_cost);
            if(kit_container != null)
            {
                var kit = kit_container.kit;
                kit_mass = kit.MassAtStage(ASSEMBLY);
                kit_cost = kit.CostAtStage(ASSEMBLY);
                Complexity = 0;
                Assembly.TotalWork = kit.TotalWorkInStage(ASSEMBLY);
                Construction.TotalWork = total_work(Construction, part.mass - kit_mass);
            }
            else
            {
                Complexity =
                    Mathf.Clamp01(1
                                  - 1
                                  / ((dry_cost / part.mass
                                      + GLB.IgnoreModules.SizeOfDifference(part.Modules) * 1000)
                                     * GLB.ComplexityFactor
                                     + 1));
                var structure_mass = part.mass * Mathf.Clamp01(1 - Complexity);
                var structure_cost =
                    Mathf.Min(
                        structure_mass
                        / GLB.ConstructionResource.def.density
                        * GLB.ConstructionResource.def.unitCost,
                        dry_cost * 0.9f);
                kit_mass = part.mass - structure_mass;
                kit_cost = dry_cost - structure_cost;
                Assembly.TotalWork = total_work(Assembly, kit_mass);
                Construction.TotalWork = total_work(Construction, structure_mass);
            }
            update_total_work();
            var frac = (float)(Assembly.TotalWork / TotalWork);
            Mass.Add(frac, kit_mass);
            Cost.Add(frac, kit_cost);
            if(assembled || frac.Equals(0))
                SetStageComplete(ASSEMBLY, true);
        }

        double total_work(JobStage task, double end_mass) =>
            (Complexity * task.Resource.ComplexityWork + task.Resource.WorkPerMass)
            * end_mass
            * 3600;

        public static void GetRequirements(
            Part p,
            out Requirements assembly_requirements,
            out Requirements construction_requirements
        )
        {
            var kit = new PartKit(p, false);
            if(kit.CurrentIndex == ASSEMBLY)
            {
                assembly_requirements = kit.RemainingRequirements().Copy();
                kit.SetStageComplete(DIYKit.ASSEMBLY, true);
            }
            else
                assembly_requirements = new Requirements();
            construction_requirements = kit.RemainingRequirements().Copy();
        }
    }
}
