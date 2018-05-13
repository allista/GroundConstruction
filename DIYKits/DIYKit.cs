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
            public bool Valid => resource != null;

            public void Update(Requirements other)
            {
                if(!other) return;
                if(resource == null)
                    resource = other.resource;
                if(resource.id == other.resource.id)
                {
                    work += other.work;
                    energy += other.energy;
                    resource_amount += other.resource_amount;
                    resource_mass += other.resource_mass;
                }
            }

            public void Clear()
            {
                work = energy = resource_mass = resource_amount = 0;
                resource = null;
            }

            public static implicit operator bool(Requirements r) => r != null && r.Valid;

            public override string ToString() =>
            string.Format("Requirements: work {0}, energy {1}, amount {2}, mass {3}, resource {4}",
                          work, energy, resource_amount, resource_mass, resource);
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
            var req = new Requirements();
            if(work <= 0)
                return req;
            var stage = CurrentStage;
            if(stage == null)
                return req;
            work = Math.Min(work, stage.WorkLeft);
            var frac = (float)get_fraction();
            req.resource = stage.Resource;
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
            if(!remainder)
            {
                if(remainder == null)
                    remainder = new Requirements();
                var stage = CurrentStage;
                if(stage != null)
                    remainder.Update(RequirementsForWork(stage.WorkLeft));
            }
            return remainder;
        }

        public static void Draw(string Name, int stage, double total_work, Requirements remainder)
        {
            GUILayout.BeginVertical(Styles.white);
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("<color=lime><b>{0}</b></color>", Name), Styles.rich_label, GUILayout.ExpandWidth(true));
            var status = StringBuilderCache.Acquire();
            if(remainder.work > 0)
            {
                status.Append(stage == ASSEMBLY ? " Assembly:" : " Construction:");
                status.AppendFormat(" <b>{0:P1}</b>", (total_work - remainder.work) / total_work);
            }
            else
                status.Append(" Complete.");
            GUILayout.Label(status.ToStringAndRelease(), Styles.rich_label, GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            if(remainder.work > 0)
            {
                var requirements = string
                    .Format("Needs: <color=brown><b>{0}</b></color> of {1}, <color=orange><b>{2}</b></color>, <b>{3:F1} SKH</b>",
                            Utils.formatBigValue((float)remainder.resource_amount, " u"),
                            remainder.resource.name,
                            Utils.formatBigValue((float)remainder.energy, " EC"),
                            remainder.work / 3600);
                GUILayout.Label(requirements, Styles.rich_label, GUILayout.ExpandWidth(true));
            }
            GUILayout.EndVertical();
        }

        public void Draw()
        {
            var stage = CurrentStage;
            var rem = RemainingRequirements();
            Draw(Name, CurrentIndex, stage != null ? stage.TotalWork : 1, rem);
        }

        public override double DoSomeWork(double work)
        {
            if(work > 0)
                remainder.Clear();
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

