//   DIYKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
    public interface iDIYKit
    {
        DIYKit.Requirements RequirementsForWork(double work);
        DIYKit.Requirements RemainingRequirements();
        void Draw();
    }

    /// <summary>
    /// DIY kit is a job that consists of two stages, Assembly and Construction,
    /// and contains two parameters, Mass and Cost.
    /// It defines two additional methods to obtain information about required
    /// resources and energy for the work: RequirementsForWork and RemainingRequirements.
    /// </summary>
    public abstract class DIYKit : Job, iDIYKit
    {
        public new const string NODE_NAME = "DIY_KIT";

        public class Requirements
        {
            public double work;
            public double energy;
            public ResourceUsageInfo resource;
            public double resource_amount;
            public double resource_mass;

            public void Update(Requirements other)
            {
                if(resource != null && other.resource != null &&
                   resource.id == other.resource.id)
                {
                    work += other.work;
                    energy += other.energy;
                    resource_amount += other.resource_amount;
                    resource_mass += other.resource_mass;
                }
            }
        }

        public const int ASSEMBLY = 0;
        public const int CONSTRUCTION = 1;

        [Persistent] public JobStage Assembly = new JobStage(ASSEMBLY, GLB.AssemblyResource);
        [Persistent] public JobStage Construction = new JobStage(CONSTRUCTION, GLB.ConstructionResource);

        [Persistent] public JobParameter Mass = new JobParameter();
        [Persistent] public JobParameter Cost = new JobParameter();

        Requirements remainder;

        protected DIYKit()
        {
            CurrentIndex = 0;
        }

        public Requirements RequirementsForWork(double work)
        {
            if(work <= 0)
                return null;
            var stage = CurrentStage;
            if(stage == null)
                return null;
            var req = new Requirements();
            work = Math.Min(work, stage.WorkLeft);
            var frac = (float)get_fraction();
            req.resource_mass = Math.Max(Mass.Curve.Evaluate(frac + (float)(work / TotalWork)) -
                                Mass.Curve.Evaluate(frac), 0);
            req.resource_amount = req.resource_mass / stage.Resource.def.density;
            req.resource = stage.Resource;
            req.energy = req.resource_mass * stage.Resource.EnergyPerMass;
            req.work = work;
            return req;
        }

        public Requirements RemainingRequirements()
        {
            if(remainder == null)
            {
                var stage = CurrentStage;
                if(stage != null)
                    remainder = RequirementsForWork(stage.WorkLeft);
            }
            return remainder;
        }

        public static void Draw(string Name, int stage, double total_work, Requirements remainder)
        {
            GUILayout.BeginVertical(Styles.white);
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("<b>{0}</b>", Name), Styles.rich_label, GUILayout.ExpandWidth(true));
            var status = "";
            if(remainder.work > 0)
            {
                status += stage == ASSEMBLY ? " Assembly:" : " Construction:";
                status += string.Format(" <b>{0:P1}</b>", (total_work - remainder.work) / total_work);
            }
            else
                status += " Complete.";
            GUILayout.Label(status, Styles.rich_label, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            if(remainder.work > 0)
            {
                var requirements = string.Format("Needs: <b>{0}</b> of {1}, <b>{2}</b>, <b>{3:F1}</b> SKH.",
                                                 Utils.formatBigValue((float)remainder.resource_amount, "u"), 
                                                 remainder.resource.name, 
                                                 Utils.formatBigValue((float)remainder.energy, "EC"),
                                                 remainder.work / 3600);
                GUILayout.Label(requirements, Styles.rich_label, GUILayout.ExpandWidth(true));
            }
            GUILayout.EndVertical();
        }

        public void Draw()
        {
            var rem = RemainingRequirements();
            if(rem != null)
            {
                var total_work = rem.work > 0 ? CurrentStage.TotalWork : 1;
                Draw(Name, CurrentIndex, total_work, rem);
            }
        }

        public override double DoSomeWork(double work)
        {
            if(work > 0)
                remainder = null;
            return base.DoSomeWork(work);
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

