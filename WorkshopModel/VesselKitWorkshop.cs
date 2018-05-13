//   VesselKitWorkshop.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
    public abstract class VesselKitWorkshop : WorkshopBase<VesselKitInfo, ConstructionSkill>
    {
        protected abstract int STAGE { get; }

        protected double serve_requirements(double work)
        {
            var req = CurrentTask.Kit.RequirementsForWork(work);
            //this.Log(req.ToString());//debug
            double have_res = 0, have_ec = 0, used_res = 0, used_ec = 0;
            //get required resource
            if(req.resource_amount > 0)
            {
                have_res = part.RequestResource(req.resource.id, req.resource_amount);
                if(req.resource_amount > 0 && have_res.Equals(0))
                {
                    Utils.Message("Not enough {0}. The work on {1} was put on hold.", req.resource.name, CurrentTask.Name);
                    work = 0;
                    goto end;
                }
            }
            //get required EC
            if(req.energy > 0)
            {
                have_ec = part.RequestResource(Utils.ElectricCharge.id, req.energy);
                if(have_ec/req.energy < GLB.WorkshopShutdownThreshold)
                {
                    Utils.Message("Not enough energy. The work on {0} was put on hold.", CurrentTask.Name);
                    work = 0;
                    goto end;
                }
            }
            //correct the amount of work we can do and of resources we need
            var frac = 1.0;
            if(req.resource_amount > 0)
                frac = have_res/req.resource_amount;
            if(req.energy > 0)
                frac = Math.Min(frac, have_ec/req.energy);
            used_res = req.resource_amount*frac;
            used_ec = req.energy*frac;
            work = req.work*frac;
            //return unused resources
            end:
            if(used_res < have_res)
                part.RequestResource(req.resource.id, used_res-have_res);
            if(used_ec < have_ec)
                part.RequestResource(Utils.ElectricCharge.id, used_ec-have_ec);
            if(work.Equals(0))
                stop();
            return work;
        }

        protected override double do_some_work(double available_work)
        {
            var work = serve_requirements(available_work);
            //this.Log("can do work: {}", work);//debug
            if(work > 0)
            {
                CurrentTask.Kit.DoSomeWork(work);
                if(CurrentTask.Kit.StageComplete(STAGE))
                {
                    on_task_complete(CurrentTask);
                    CurrentTask.Kit.NextStage();
                    start_next_item();
                }
            }
            return available_work-work;
        }

        protected override void on_stop(bool reset)
        {
            if(check_task(CurrentTask))
                CurrentTask.Kit.CheckoutWorker(this);
            if(reset)
                reset_current_task();
        }

        #region implemented abstract members of WorkshopBase
        protected override void update_ETA()
        {
            update_workforce();
            var lastEndUT = EndUT;
            if(EffectiveWorkforce > 0)
            {
                if(LastUpdateTime < 0)
                    LastUpdateTime = Planetarium.GetUniversalTime();
                CurrentTask.Kit.CheckinWorker(this);
                var ETA = CurrentTask.Kit.CurrentTaskETA;
                if(ETA > 0)
                {
                    var time = Planetarium.GetUniversalTime();
                    EndUT = time+ETA;
                    ETA_Display = "Time left: "+KSPUtil.PrintTimeCompact(ETA, false);
                }
            }
            else
                EndUT = -1;
            if(EndUT < 0)
                ETA_Display = "Stalled...";
            if(Math.Abs(EndUT-lastEndUT) > 1)
                checkin();
        }

        protected abstract void main_window(int WindowID);
        protected override void draw()
        {
            Utils.LockIfMouseOver(LockName, WindowPos);
            WindowPos = GUILayout.Window(GetInstanceID(),
                                         WindowPos, main_window, part.partInfo.title,
                                         GUILayout.Width(width),
                                         GUILayout.Height(height)).clampToScreen();
        }
        #endregion
    }
}

