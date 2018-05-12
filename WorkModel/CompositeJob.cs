//   CompositeJob.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using AT_Utils;

namespace GroundConstruction
{

    public class CompositeJob<T> : JobBase where T : Job, new()
    {
        [Persistent] public PersistentList<T> Jobs = new PersistentList<T>();

        public override bool Valid => base.Valid && Jobs.Count > 0;

        public override double WorkLeft
        {
            get
            {
                var work = 0.0;
                Jobs.ForEach(j => work += j.WorkLeft);
                return work;
            }
        }

        public double WorkLeftInStage(int stage)
        {
            var work = 0.0;
            Jobs.ForEach(j => work += j[stage].WorkLeft);
            return work;
        }

        public override bool Complete => Jobs.TrueForAll(j => j.Complete);

        public bool StageComplete(int stage) => Jobs.TrueForAll(j => j[stage].Complete);

        public T CurrentJob => CurrentIndex < Jobs.Count ? Jobs[CurrentIndex] : null;

        public int CurrentStageIndex => Jobs.Count > 0 ? Jobs[0].CurrentIndex : -1;

        public override int StagesCount => Jobs.Count > 0 ? Jobs[0].StagesCount : -1;

        public override void NextStage()
        {
            var stage = CurrentStageIndex;
            if(stage >= 0 && stage < StagesCount && StageComplete(stage))
            {
                stage += 1;
                Jobs.ForEach(j => j.CurrentIndex = stage);
            }
        }

        public override double DoSomeWork(double work)
        {
            var job = CurrentJob;
            if(job != null)
            {
                work = job.DoSomeWork(work);
                if(job.CurrentStage.Complete)
                    CurrentIndex += 1;
            }
            return work;
        }

        public override void SetComplete(bool complete) => 
        Jobs.ForEach(j => j.SetComplete(complete));

        public override void SetStageComplete(int stage, bool complete) => 
        Jobs.ForEach(j => j.SetStageComplete(stage, complete));
    }
}

