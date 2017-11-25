//   DIYKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;

namespace GroundConstruction
{
    public interface iDIYKit
    {
        bool Valid { get; }
        float CurrentMass { get; }
        float CurrentCost { get; }
        double RequirementsForWork(double work, out double energy, out int resource_id, out double resource_mass);
        double DoSomeWork(double work);
    }

    public abstract class DIYKit : Job, iDIYKit
    {
        public new const string NODE_NAME = "DIY_KIT";

        protected static Globals GLB { get { return Globals.Instance; } }

        [Persistent] public Task Assembly;
        [Persistent] public Task Construction;

        public Param Mass { get { return Parameters["Mass"]; } }
        public Param Cost { get { return Parameters["Cost"]; } }

        public virtual float CurrentMass { get { return Mass.Value; } }
        public virtual float CurrentCost { get { return Cost.Value; } }

        public virtual float EndMass { get { return Mass.Max; } }
        public virtual float EndCost { get { return Cost.Max; } }

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
            var resource_mass = Math.Max(Mass.Curve.Evaluate(frac+(float)(work/TotalWork)) - 
                                         Mass.Curve.Evaluate(frac), 0);
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
            if(node.HasValue("TotalWork"))
                Construction.Load(node);
        }
    }
}

