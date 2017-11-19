//   DIYKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using System.Collections.Generic;
using AT_Utils;

namespace GroundConstruction
{
    public interface iDIYKit
    {
        bool Valid { get; }

        double AssembleyRequirement(double work, out double energy, out int resource_id, out double resource_mass);
        double ConstructionRequerement(double work, out double energy, out int resource_id, out double resource_mass);

        bool Assemble(double work);
        bool Construct(double work);
    }

    public abstract class DIYKit : ConfigNodeObject, iDIYKit
    {
        public new const string NODE_NAME = "DIY_KIT";

        protected static Globals GLB { get { return Globals.Instance; } }

        public class Job : ConfigNodeObject
        {
            public readonly uint id;

            [Persistent] public double TotalWork;    //seconds
            [Persistent] public double WorkDone;     //seconds
            [Persistent] public float  Completeness; //fraction

            public ResourceUsageInfo Resource { get; private set; }

            public double WorkLeft { get { return TotalWork-WorkDone; } }
            public bool Complete { get { return Completeness >= 1; } }

            public Job(uint id = 0, ResourceUsageInfo resource = null, float completeness = 0)
            {
                this.id = id;
                Resource = resource;
                Completeness = completeness;
            }

            public void AddSubtask(Job job)
            {
                TotalWork += job.TotalWork;
                WorkDone += job.WorkDone;
                update_completeness();
            }

            public void DoSomeWork(double work)
            {
                WorkDone = Math.Min(TotalWork, WorkDone+work);
                update_completeness();
            }

            public void DoSomeWork(Job job)
            {
                WorkDone = Math.Min(TotalWork, WorkDone+job.TotalWork);
                update_completeness();
            }

            public double Requirement(double work, double start_mass, double end_mass, out double energy, out int resource_id, out double resource_mass)
            {
                if(Complete)
                {
                    energy = 0;
                    resource_id = Resource.id;
                    resource_mass = 0;
                    return 0;
                }
                work = Math.Min(work, WorkLeft);
                resource_id = Resource.id;
                resource_mass = work/TotalWork*(end_mass-start_mass);
                energy = resource_mass*Resource.EnergyPerMass;
                return work;
            }

            void update_completeness()
            {
                Completeness = (float)Math.Min(WorkDone/TotalWork, 1);
            }
        }

        [Persistent] public Job Assembly = new Job(0, GLB.AssemblyResource, 1); //a kit is assembled by default
        [Persistent] public Job Construction = new Job(1, GLB.ConstructionResource);

        [Persistent] public float Mass = -1;
        [Persistent] public float Cost;

        public bool Valid { get { return Mass >= 0; } }

        Dictionary<uint,Job> jobs = new Dictionary<uint, Job>{
            {Assembly.id, Assembly}, 
            {Construction.id, Construction}
        };

        public Job SameJob(Job other)
        { 
            Job job;
            if(jobs.TryGetValue(other.id, out job))
                return job;
            return null;
        }

        public abstract double AssembleyRequirement(double work, out double energy, out int resource_id, out double resource_mass);
        public abstract double ConstructionRequerement(double work, out double energy, out int resource_id, out double resource_mass);

        public abstract bool Assemble(double work);
        public abstract bool Construct(double work);

        public static implicit operator bool(DIYKit kit)
        {
            return kit != null && kit.Valid;
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

