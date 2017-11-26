//   DIYKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;

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

        void get_requirements(double work, out double energy, out int resource_id, out double resource_amount)
        {
            if(Current == null)
            {
                energy = 0;
                resource_id = -1;
                resource_amount = 0;
            }
            var frac = (float)GetFraction();
            resource_id = Current.Resource.id;
            var resource_mass = Math.Max(mass.Curve.Evaluate(frac+(float)(work/TotalWork)) - 
                                         mass.Curve.Evaluate(frac), 0);
            resource_amount = resource_mass/Current.Resource.def.density;
            energy = resource_mass*Current.Resource.EnergyPerMass;
        }

        public double RequirementsForWork(double work, out double energy, out int resource_id, out double resource_amount)
        {
            work = Math.Min(work, Current.WorkLeft);
            get_requirements(work, out energy, out resource_id, out resource_amount);
            return work;
        }

        public double RemainingRequirements(out double energy, out int resource_id, out double resource_amount)
        {
            var work = Current.WorkLeft;
            get_requirements(work, out energy, out resource_id, out resource_amount);
            return work;
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

