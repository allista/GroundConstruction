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
    /// DIY kit is a job that consists of two tasks, Assembly and Construction,
    /// and contains two parameters, Mass and Cost.
    /// It defines two additional methods to obtain information about required
    /// resources and energy for the work: RequirementsForWork and RemainingRequirements.
    /// </summary>
    public abstract class DIYKit : Job
    {
        public new const string NODE_NAME = "DIY_KIT";

        protected static Globals GLB { get { return Globals.Instance; } }

        [Persistent] public Task Assembly;
        [Persistent] public Task Construction;

        protected Param mass { get { return Parameters["Mass"]; } }
        protected Param cost { get { return Parameters["Cost"]; } }

        public virtual float Mass { get { return mass.Value; } }
        public virtual float Cost { get { return cost.Value; } }

        protected DIYKit()
        {
            Assembly = add_task(GLB.AssemblyResource, 0.5f);
            Construction = add_task(GLB.ConstructionResource, 1);
            Parameters.Add("Mass", new Param());
            Parameters.Add("Cost", new Param());
        }

        public double RequirementsForWork(double work, out double energy, out ResourceUsageInfo resource, out double resource_amount)
        {
            energy = 0;
            resource = null;
            resource_amount = 0;
            if(Current == null) 
                return 0;
            var task = Current.CurrentSubtask;
            var kit = task.Job as DIYKit;
            work = Math.Min(work, task.WorkLeft);
            if(kit != null)  
            {       
                var frac = (float)(task.WorkDoneWithPrev/TotalWork);
                var resource_mass = Math.Max(kit.mass.Curve.Evaluate(frac+(float)(work/kit.TotalWork)) - 
                                             kit.mass.Curve.Evaluate(frac), 0);
                resource_amount = resource_mass/task.Resource.def.density;
                resource = task.Resource;
                energy = resource_mass*task.Resource.EnergyPerMass;
                Utils.Log("Req: work {}, frac {}, frac+work {}, r.mass {}", 
                          work, frac, frac+(work/kit.TotalWork), resource_mass);//debug
            }
            return work;
        }

        public double RemainingRequirements(out double energy, out ResourceUsageInfo resource, out double resource_amount)
        {
            
        }

        public string Status()
        {
            var s = string.Format("\"{0}\" ", Name);
            ResourceUsageInfo resource;
            double energy, resource_amount;
            var work_left = RemainingRequirements(out energy, out resource, out resource_amount);
            Utils.Log("work {}/{}, energy {}, resource amount {}\n" +
                      "Assembly: {}\n" +
                      "Construction: {}\n", 
                      work_left, Current.TotalWorkWithSubtasks(), energy, resource_amount, Assembly, Construction);//debug
            if(work_left > 0)
            {
                s += string.Format(" needs: {0} of {1}, {2}, {3:F1} SKH.",
                               Utils.formatBigValue((float)resource_amount, "u"), resource.name, 
                               Utils.formatBigValue((float)energy, "EC"),
                               work_left/3600);
                var total_work = Current.TotalWorkWithSubtasks();
                if(work_left < total_work)
                {
                    s += Current == Assembly ? " Assembly:" : " Construction:";
                    s += string.Format(" {0:P1}", (total_work-work_left)/total_work);
                }
            }
            else s += " Complete.";
            return s;
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

