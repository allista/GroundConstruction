//   DIYKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using AT_Utils;

namespace GroundConstruction
{
    /// <summary>
    /// DIY kit is a job that consists of two stages, Assembly and Construction,
    /// and contains two parameters, Mass and Cost.
    /// It defines two additional methods to obtain information about required
    /// resources and energy for the work: RequirementsForWork and RemainingRequirements.
    /// </summary>
    public abstract class DIYKit : Job
    {
        public new const string NODE_NAME = "DIY_KIT";

        public const int ASSEMBLY = 0;
        public const int CONSTRUCTION = 1;

        [Persistent] public JobStage Assembly = new JobStage(ASSEMBLY, GLB.AssemblyResource);
        [Persistent] public JobStage Construction = new JobStage(CONSTRUCTION, GLB.ConstructionResource);

        [Persistent] public JobParameter Mass = new JobParameter();
        [Persistent] public JobParameter Cost = new JobParameter();

        protected DIYKit()
        {
            CurrentIndex = 0;
        }

        public double RequirementsForWork(double work, out double energy, out ResourceUsageInfo resource, out double resource_amount)
        {
            energy = 0;
            resource = null;
            resource_amount = 0;
            if(work <= 0)
                return 0;
            var stage = CurrentStage;
            if(stage == null)
                return 0;
            work = Math.Min(work, stage.WorkLeft);
            var frac = (float)get_fraction();
            var resource_mass = Math.Max(Mass.Curve.Evaluate(frac + (float)(work / TotalWork)) -
                                Mass.Curve.Evaluate(frac), 0);
            resource_amount = resource_mass / stage.Resource.def.density;
            resource = stage.Resource;
            energy = resource_mass * stage.Resource.EnergyPerMass;
//            Utils.Log("Req: work {}, frac {}, frac+work {}, r.mass {}", 
//                      work, frac, frac + (work / TotalWork), resource_mass);//debug
            return work;
        }

        public double RemainingRequirements(out double energy, out ResourceUsageInfo resource, out double resource_amount)
        {
            energy = 0;
            resource = null;
            resource_amount = 0;
            var stage = CurrentStage;
            return stage != null ? 
                RequirementsForWork(stage.WorkLeft, out energy, out resource, out resource_amount) : 0;
        }

        public static string Status(string Name, int stage, double work_left, double total_work, 
                                    double energy, ResourceUsageInfo resource, double resource_amount)
        {
            var s = string.Format("\"{0}\" ", Name);
            if(work_left > 0)
            {
                s += string.Format(" needs: {0} of {1}, {2}, {3:F1} SKH.",
                                   Utils.formatBigValue((float)resource_amount, "u"), resource.name, 
                                   Utils.formatBigValue((float)energy, "EC"),
                                   work_left / 3600);
                s += stage == ASSEMBLY ? " Assembly:" : " Construction:";
                s += string.Format(" {0:P1}", (total_work - work_left) / total_work);
            }
            else
                s += " Complete.";
            return s;
        }

        public string Status()
        {
            ResourceUsageInfo resource;
            double energy, resource_amount;
            var work_left = RemainingRequirements(out energy, out resource, out resource_amount);
            var total_work = work_left > 0 ? CurrentStage.TotalWork : 1;
            return Status(Name, CurrentIndex, work_left, total_work, energy, resource, resource_amount);
        }

        //deprecated config conversion
        public override void Load(ConfigNode node)
        {
            base.Load(node);
            if(node.HasValue("Completeness"))
                Construction.Load(node);
        }
    }
}

