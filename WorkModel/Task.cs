//   Task.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using System.Collections.Generic;

namespace GroundConstruction
{
    public partial class Job
    {
        /// <summary>
        /// Task is a single piece of work on something. 
        /// Its type is defined by the resource that requires to do the work.
        /// It can contain subtasks of the same type.
        /// </summary>
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

            public double Completeness { get { return GetWorkDone()/GetTotalWork(); } }
            public bool Complete { get { return GetWorkDone() >= GetTotalWork(); } }
            public double WorkLeft { get { return GetTotalWork()-GetWorkDone(); } }

            public Task(Job job, ResourceUsageInfo resource, double total_work)
            {
                Job = job;
                Resource = resource;
                TotalWork = total_work;
                if(job.Last != null)
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
                    if(current_subtask < subtasks.Count && 
                       subtasks[current_subtask].Complete)
                        current_subtask += 1;
                    Job.UpdateTotalWork();
                }
            }

            public double GetTotalWork()
            {
                var work = TotalWork;
                subtasks.ForEach(t => work += t.TotalWork);
                return work;
            }

            public double GetWorkDone()
            {
                var work = WorkDone;
                subtasks.ForEach(t => work += t.WorkDone);
                return work;
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
    }
}

