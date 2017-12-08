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

            public readonly List<Task> Subtasks = new List<Task>();
            int current_subtask = -1;

            public Task CurrentSubtask 
            { 
                get 
                { 
                    return current_subtask < 0 || current_subtask >= Subtasks.Count?
                        this : Subtasks[current_subtask].CurrentSubtask;
                }
            }

            public override bool Complete { get { return WorkDoneWithSubtasks() >= TotalWorkWithSubtasks(); } }
            public double Fraction { get { return TotalWork > 0? WorkDone/TotalWork : 1; } }
            public double FractionWithSubtasks { get { return WorkDoneWithSubtasks()/TotalWorkWithSubtasks(); } }
            public double WorkLeft { get { return TotalWork-WorkDone; } }
            public double WorkLeftWithSubtasks { get { return TotalWorkWithSubtasks()-WorkDoneWithSubtasks(); } }
            public double TotalWorkWithPrev { get { return Prev == null? TotalWork : TotalWork+Prev.TotalWorkWithPrev; } }
            public double WorkDoneWithPrev { get { return Prev == null? WorkDone : WorkDone+Prev.WorkDoneWithPrev; } }

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
                Job.update_total_work();
            }

            public void AddSubtask(Task task)
            {
                if(task != null && task.Resource.id == Resource.id)
                {
                    Subtasks.Add(task);
                    if(Next != null)
                        Next.AddSubtask(task.Next);
                    if(current_subtask < 0)
                        current_subtask = 0;
                    if(current_subtask < Subtasks.Count && 
                       Subtasks[current_subtask].Complete)
                        current_subtask += 1;
                }
            }

            public IEnumerable<Job> AllJobs()
            {
                foreach(var subtask in Subtasks)
                {
                    foreach(var subjob in subtask.AllJobs())
                        yield return subjob;
                }
                yield return Job;
            }

            public double TotalWorkWithSubtasks()
            {
                var work = TotalWork;
                Subtasks.ForEach(t => work += t.TotalWorkWithSubtasks());
                return work;
            }

            public double WorkDoneWithSubtasks()
            {
                var work = WorkDone;
                Subtasks.ForEach(t => work += t.WorkDoneWithSubtasks());
                return work;
            }

            public override void SetComplete(bool complete)
            {
                WorkDone = complete ? TotalWork : 0;
                Subtasks.ForEach(t => t.SetComplete(complete));
                if(complete && Prev != null) 
                    Prev.SetComplete(complete);
                else if(!complete && Next != null)
                    Next.SetComplete(complete);
                Job.UpdateCurrentTask();
                Job.update_params();
            }

            public double DoSomeWork(double work)
            {
                var task = current_subtask < 0 || current_subtask > Subtasks.Count?
                    this : Subtasks[current_subtask];
                double dwork;
                if(task == this)
                {
                    dwork = Math.Min(work, task.TotalWork-task.WorkDone);
                    if(dwork > 0)
                    {
                        task.WorkDone += dwork;
                        Job.update_params();
                    }
                }
                else
                {
                    dwork = task.DoSomeWork(work);
                    if(task.Complete)
                        current_subtask += 1;
                }
                return Math.Max(work-dwork, 0);
            }
        }
    }
}

