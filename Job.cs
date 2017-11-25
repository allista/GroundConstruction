//   Job.cs
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
    public abstract class Work : ConfigNodeObject
    {
        [Persistent] public double TotalWork;
        public abstract void SetComplete(bool complete);
    }


    public class Task : Work
    {
        [Persistent] public double WorkDone;

        public Job Job;
        public Task Prev, Next;

        public readonly ResourceUsageInfo Resource;

        readonly List<Task> subtasks = new List<Task>();
        int current_subtask = -1;

        public Task CurrentSubtask 
        { 
            get 
            { 
                return current_subtask < 0 || current_subtask >= subtasks.Count?
                    this : subtasks[current_subtask].CurrentSubtask;
            }
        }

        public float Completeness { get { return (float)(GetWorkDone()/GetTotalWork()); } }
        public bool Complete { get { return GetWorkDone() >= GetTotalWork(); } }
        public double WorkLeft { get { return GetTotalWork()-GetWorkDone(); } }

        public Task(Job job, ResourceUsageInfo resource, double total_work)
        {
            Job = job;
            Resource = resource;
            TotalWork = total_work;
            if(job.Last)
            {
                Prev = job.Last;
                Prev.Next = job.Last = this;
            }
            else
                job.First = job.Last = job.Current = this;
            Job.UpdateTotalWork();
        }

        public void AddSubtask(Task task)
        {
            if(task != null && task.Resource.id == Resource.id)
            {
                subtasks.Add(task);
                if(Next != null)
                    Next.AddSubtask(task.Next);
                if(current_subtask < 0)
                    current_subtask = 0;
                Job.UpdateTotalWork();
            }
        }

        public double GetTotalWork()
        {
            var work = TotalWork;
            subtasks.ForEach(t => work += t.TotalWork);
        }

        public double GetWorkDone()
        {
            var work = WorkDone;
            subtasks.ForEach(t => work += t.WorkDone);
        }

        public double TotalWorkWithPrev() 
        { 
            return Prev == null? 
                GetTotalWork() : GetTotalWork()+Prev.TotalWorkWithPrev(); 
        }

        public double WorkDoneWithPrev() 
        { 
            return Prev == null? 
                GetWorkDone() : GetWorkDone()+Prev.TotalWorkWithPrev(); 
        }

        public float TotalFraction()
        {
            return (float)(TotalWorkWithPrev()/Job.TotalWork);
        }

        public override void SetComplete(bool complete)
        {
            WorkDone = complete ? TotalWork : 0;
            subtasks.ForEach(t => t.SetComplete(complete));
            if(complete && Prev != null) 
            {
                Prev.SetComplete(complete);
                Job.Current = Next;
            }
            else if(!complete && Next != null)
            {
                Next.SetComplete(complete);
                Job.Current = this;
            }
            Job.UpdateParams();
        }

        public double DoSomeWork(double work)
        {
            Task task;
            do {
                task = CurrentSubtask;
                if(task == this)
                {
                    var dwork = Math.Min(work, task.TotalWork-task.WorkDone);
                    task.WorkDone += dwork;
                    work -= dwork;
                    break;
                }
                work = task.DoSomeWork(work);
                if(task.Complete)
                    current_subtask += 1;
            } while(work > 1e-5);
            return Math.Max(work, 0);
        }
    }


    public class Job : Work
    {
        public class Param : ConfigNodeObject
        {
            [Persistent] public float Value;
            [Persistent] public FloatCurve Curve = new FloatCurve();

            public float Min { get { return Curve.Curve.keys[0].value; } }
            public float Max { get { return Curve.Curve.keys[Curve.Curve.length-1].value; } }

            public Param()
            {
                Curve.Add(0, 0);
            }

            public void Update(float fraction)
            {
                Value = Curve.Evaluate(fraction);
            }
        }

        [Persistent] public PersistentDictS<Param> Parameters = new PersistentDictS<Param>();

        public Task First { get; protected set; }
        public Task Last { get; protected set; }
        public Task Current { get; protected set; }

        public virtual bool Valid { get { return First != null; } }
        public bool Complete { get { return First != null && Current == null; } }

        public double GetFraction()
        { 
            return Current != null? 
                Current.WorkDoneWithPrev()/TotalWork : 
                (First != null? 1.0 : 0.0); 
        }

        public Job CurrentSubJob()
        {
            return Current == null? 
                this : Current.CurrentSubtask.Job;
        }

        public void AddSubJob(Job sub)
        {
            if(First != null)
                First.AddSubtask(sub.First);
        }

        public double DoSomeWork(double work)
        {
            if(Current != null) 
            {
                var left = Current.DoSomeWork(work);
                if(!left.Equals(work))
                    UpdateParams();
                if(Current.Complete)
                    Current = Current.Next;
                work = left;
            }
            return work;
        }

        public void UpdateTotalWork()
        {
            if(Last != null)
                TotalWork = Last.TotalWorkWithPrev();
        }

        public void UpdateParams()
        {
            var frac = GetFraction();
            foreach(var val in Parameters.Values)
                val.Update((float)frac);
        }

        public override void SetComplete(bool complete)
        {
            if(First != null)
            {
                Current = null;
                if(complete) Last.SetComplete(complete);
                else First.SetComplete(complete);
            }
        }

        protected Task add_task(ResourceUsageInfo resource, float end_fraction)
        {
            return new Task(this, resource, end_fraction);
        }

        public static implicit operator bool(Job job) { return job != null && job.Valid; }
    }
}

