//   Job.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace GroundConstruction
{
    public class Job : JobBase
    {
        public double TotalWork { get; private set; }

        readonly List<JobStage> stages = new List<JobStage>();
        readonly List<JobParameter> parameters = new List<JobParameter>();

        public override bool Valid => base.Valid && stages.Count > 0;

        public override bool Complete => CurrentIndex >= stages.Count;

        public override double WorkLeft => TotalWork * (1 - get_fraction());

        public override int StagesCount => stages.Count;

        public JobStage this[int index] => stages[index];

        public JobStage CurrentStage => 
        CurrentIndex >= 0 && CurrentIndex < stages.Count ? stages[CurrentIndex] : null;

        protected void fill_member_collection<T>(IList<T> collection) where T : class
        {
            foreach(var fi in GetType()
                    .GetFields(BindingFlags.Public|
                               BindingFlags.Instance|
                               BindingFlags.FlattenHierarchy)
                    .Where(fi => typeof(T).IsAssignableFrom(fi.FieldType)))
            {
                if(fi.GetValue(this) is T item)
                    collection.Add(item);
            }
        }

        public Job()
        {
            fill_member_collection(stages);
            fill_member_collection(parameters);
            stages.Sort();
        }

        public override void NextStage()
        {
            if(CurrentIndex < stages.Count && CurrentStage.Complete)
                CurrentIndex += 1;
        }

        protected void update_parameters()
        {
            var frac = (float)get_fraction();
            parameters.ForEach(p => p.Update(frac));
        }

        protected void update_total_work()
        {
            TotalWork = 0;
            stages.ForEach(s => TotalWork += s.TotalWork);
        }

        protected double get_fraction()
        {
            if(Complete)
                return 1;
            var work = 0.0;
            for(int i = 0; i <= CurrentIndex; i++)
                work += stages[i].WorkDone;
            return work / TotalWork;
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            update_total_work();
        }

        public override double DoSomeWork(double work)
        {
            var stage = CurrentStage;
            if(stage != null)
            {
                work = stage.DoSomeWork(work);
                update_parameters();
            }
            return work;
        }

        public override void SetComplete(bool complete)
        {
            stages.ForEach(s => s.SetComplete(complete));
            CurrentIndex = stages.Count;
        }

        public override void SetStageComplete(int stage, bool complete)
        {
            if(complete)
            {
                if(stage < CurrentIndex)
                    return;
                for(int i = CurrentIndex; i <= stage; i++)
                    stages[i].SetComplete(complete);
                CurrentIndex = stage + 1;
            }
            else
            {
                if(stage > CurrentIndex)
                    return;
                for(int i = stage, count = stages.Count; i < count; i++)
                    stages[i].SetComplete(complete);
                CurrentIndex = stage;
            }
            update_parameters();
        }
    }
}

