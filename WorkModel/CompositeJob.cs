//   CompositeJob.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri

using System.Linq;
using AT_Utils;

namespace GroundConstruction
{
    public class CompositeJob<T> : JobBase where T : Job, new()
    {
        [Persistent] public PersistentList<T> Jobs = new PersistentList<T>();

        public override bool Valid => base.Valid && Jobs.Count > 0;
        public virtual bool Empty => Jobs.Count == 0;

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
            Jobs.ForEach(j =>
            {
                if(j.StagesCount > stage)
                    work += j[stage].WorkLeft;
            });
            return work;
        }
        
        public double TotalWorkInStage(int stage)
        {
            var work = 0.0;
            Jobs.ForEach(j =>
            {
                if(j.StagesCount > stage)
                    work += j[stage].TotalWork;
            });
            return work;
        }

        public override bool Complete => Jobs.TrueForAll(j => j.Complete);

        public bool StageStarted(int stage) => Jobs.Any(j => j.StagesCount > stage && j[stage].WorkDone > 0);

        public bool StageComplete(int stage) => Jobs.TrueForAll(j => j.StagesCount <= stage || j[stage].Complete);

        public T CurrentJob => CurrentIndex < Jobs.Count ? Jobs[CurrentIndex] : null;

        public int CurrentStageIndex => Jobs.Count > 0 ? Jobs.Min(j => j.CurrentIndex) : -1;

        public override int StagesCount => Jobs.Count > 0 ? Jobs[0].StagesCount : -1;

        public override void NextStage()
        {
            var stages = StagesCount;
            var stage = CurrentStageIndex;
            while(stage >= 0 && stage < stages && StageComplete(stage))
            {
                stage += 1;
                Jobs.ForEach(j =>
                {
                    if(j.CurrentIndex < stage)
                        j.CurrentIndex = stage;
                });
                CurrentIndex = 0;
            }
        }

        private T next_job(int current_stage)
        {
            T job;
            do
            {
                CurrentIndex += 1;
                job = CurrentJob;
            } while(job != null && (job.CurrentIndex != current_stage || job.CurrentStage.Complete));
            return job;
        }

        public override double DoSomeWork(double work)
        {
            var job = CurrentJob;
            var stage = CurrentStageIndex;
            while(work > 0 && job != null)
            {
                if(job.CurrentIndex == stage)
                {
//                    Utils.Log("Doing {} work on {}", work, job); //debug
                    work = job.DoSomeWork(work);
                    if(job.CurrentStage.Complete)
                    {
//                        Utils.Log("Completed {}", job); //debug
                        job = next_job(stage);
                    }
                }
                else
                {
//                    Utils.Log("Skipping {}", job); //debug
                    job = next_job(stage);
                }
            }
            if(job == null)
                NextStage();
            return work;
        }

        public override void SetComplete(bool complete) =>
            Jobs.ForEach(j => j.SetComplete(complete));

        public override void SetStageComplete(int stage, bool complete) =>
            Jobs.ForEach(j => j.SetStageComplete(stage, complete));
    }
}
