//   JobStage.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;

namespace GroundConstruction
{
    public class JobStage : WorkBase, IComparable<JobStage>
    {
        [Persistent] public double TotalWork;
        [Persistent] public double WorkDone;

        public readonly ResourceUsageInfo Resource;

        public override double WorkLeft => TotalWork - WorkDone;

        public override bool Complete => WorkDone >= TotalWork;

        public readonly int Index;

        public JobStage(int i = 0, ResourceUsageInfo res = null)
        { 
            Index = i; 
            Resource = res;
        }

        public override void SetComplete(bool complete) => 
        WorkDone = complete ? TotalWork : 0;

        public override double DoSomeWork(double work)
        { 
            if(Complete)
                return work;
            if(work >= WorkLeft)
            {
                var dwork = WorkLeft;
                WorkDone = TotalWork;
                return work - dwork;
            }
            WorkDone += work;
            return 0;
        }

        public int CompareTo(JobStage other) => Index.CompareTo(other.Index);
    }
}

