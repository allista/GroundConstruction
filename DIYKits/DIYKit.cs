//   DIYKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri

using System;
using UnityEngine;
using AT_Utils;
using AT_Utils.UI;

namespace GroundConstruction
{
    public interface iDIYKit
    {
        DIYKit.Requirements RequirementsForWork(double work);
        DIYKit.Requirements RemainingRequirements();
        bool Draw(GUIStyle style = null);
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

            public Requirements Copy()
            {
                var newCopy = new Requirements();
                newCopy.Update(this);
                return newCopy;
            }

            public void Update(Requirements other)
            {
                if(!other)
                    return;
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
                $"Requirements: work {work}, energy {energy}, amount {resource_amount}, mass {resource_mass}, resource {resource}";
        }

        public const int ASSEMBLY = 0;
        public const int CONSTRUCTION = 1;

        [Persistent] public JobStage Assembly = new JobStage(ASSEMBLY, GLB.AssemblyResource);

        [Persistent]
        public JobStage Construction = new JobStage(CONSTRUCTION, GLB.ConstructionResource);

        [Persistent] public JobParameter Mass = new JobParameter();
        [Persistent] public JobParameter Cost = new JobParameter();

        Requirements remainder;

        protected DIYKit()
        {
            CurrentIndex = 0;
        }

        public float MassAtStage(int stage) =>
            Mass.Curve.Evaluate((float)(this[stage].TotalWork / TotalWork));

        public float CostAtStage(int stage) =>
            Cost.Curve.Evaluate((float)(this[stage].TotalWork / TotalWork));

        public Requirements RequirementsForWork(double work)
        {
            var req = new Requirements();
            if(work <= 0)
                return req;
            var stage = CurrentStage;
            if(stage == null)
                return req;
            work = Math.Min(work, stage.WorkLeft);
            var frac = get_fraction();
            //Utils.Log("frac {} +delta {}, dM {}", frac, (frac + (work / TotalWork)), 
            //Mass.Curve.Evaluate((float)(frac + (work / TotalWork))) -
            //Mass.Curve.Evaluate((float)frac)); //debug
            req.resource_mass = Math.Max(Mass.Curve.Evaluate((float)(frac + (work / TotalWork)))
                                         - Mass.Curve.Evaluate((float)frac),
                0);
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

        public static bool Draw(
            string Name,
            int stage,
            double total_work,
            Requirements remainder,
            GUIStyle style = null,
            string additional_info = ""
        )
        {
            GUILayout.BeginVertical(style ?? Styles.white);
            GUILayout.BeginHorizontal();
            var name = Colors.Good.Tag("<b>{0}</b>", Name);
            if(!string.IsNullOrEmpty(additional_info))
                name += " " + additional_info;
            var clicked = GUILayout.Button(name,
                Styles.rich_label,
                GUILayout.ExpandWidth(true));
            if(remainder.work > 0)
            {
                var status = StringBuilderCache.Acquire();
                status.Append(stage == ASSEMBLY ? " Assembly:" : " Construction:");
                status.AppendFormat(" <b>{0:P1}</b>", (total_work - remainder.work) / total_work);
                if(GUILayout.Button(status.ToStringAndRelease(),
                    Styles.rich_label,
                    GUILayout.ExpandWidth(false)))
                    clicked = true;
            }
            GUILayout.EndHorizontal();
            if(remainder.work > 0)
            {
                var requirements =
                    $@"Needs: <color=brown><b>{Utils.formatBigValue((float)remainder.resource_amount, " u")
                        }</b></color> of {remainder.resource.name
                        }, <color=orange><b>{Utils.formatBigValue((float)remainder.energy, " EC")
                        }</b></color>, <b>{remainder.work / 3600:F1} SKH</b>";
                if(GUILayout.Button(requirements, Styles.rich_label, GUILayout.ExpandWidth(true)))
                    clicked = true;
            }
            GUILayout.EndVertical();
            return clicked;
        }

        public bool Draw(GUIStyle style = null)
        {
            var stage = CurrentStage;
            var rem = RemainingRequirements();
            return Draw(Name, CurrentIndex, stage != null ? stage.TotalWork : 1, rem, style);
        }

        public override double DoSomeWork(double work)
        {
            if(work > 0 && remainder != null)
                remainder.Clear();
            return base.DoSomeWork(work);
        }

        public override void NextStage()
        {
            base.NextStage();
            if(remainder != null)
                remainder.Clear();
        }

        public override void SetStageComplete(int stage, bool complete)
        {
            base.SetStageComplete(stage, complete);
            if(remainder != null)
                remainder.Clear();
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
