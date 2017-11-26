//   WorkshopBase.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using System.Linq;
using UnityEngine;
using AT_Utils;
using Experience;

namespace GroundConstruction
{
    public interface IWorkshopTask : IConfigNode
    {
        Guid ID { get; }
        bool Valid { get; }
        bool Complete { get; }
        bool Recheck();
    }

    public abstract class WorkshopBase<T> : PartModule where T : class, IWorkshopTask, new()
    {
        [KSPField(isPersistant = true)] public PersistentQueue<T> Queue = new PersistentQueue<T>();
        [KSPField(isPersistant = true)] public T CurrentTask;

        [KSPField(isPersistant = true)] public bool Working;
        [KSPField(isPersistant = true)] public double LastUpdateTime = -1;
        [KSPField(isPersistant = true)] public double EndUT = -1;
        public string ETA_Display { get; protected set; } = "Stalled...";

        protected float workforce = 0;
        protected float max_workforce = 0;

        public string Workforce_Display
        { get { return string.Format("Workforce: {0:F1}/{1:F1} SK", workforce, max_workforce); } }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            LockName = GetType().Name+GetInstanceID();
        }

        protected virtual void update_max_workforce()
        {
            max_workforce = part.CrewCapacity*5;
        }

        protected virtual void update_workforce<E>()
            where E : ExperienceEffect
        {
            workforce = 0;
            foreach(var kerbal in part.protoModuleCrew)
            {
                var worker = 0f;
                var trait = kerbal.experienceTrait;
                foreach(var effect in trait.Effects)
                {
                    if(effect is E)
                    { worker = 1; break; }
                }
                worker *= Mathf.Max(trait.CrewMemberExperienceLevel(), 0.5f);
                workforce += worker;
            }
        }

        protected void start()
        {
            Working = true;
            if(CurrentTask.Recheck())
                update_ETA();
            on_start();
            checkin();
        }
        protected virtual void on_start() {}

        protected void stop(bool reset = false)
        {
            Working = false;
            EndUT = -1;
            ETA_Display = "";
            LastUpdateTime = -1;
            TimeWarp.SetRate(0, false);
            on_stop(reset);
            checkin();
        }
        protected virtual void on_stop(bool reset) {}

        protected double get_delta_time()
        {
            if(Time.timeSinceLevelLoad < 1 || !FlightGlobals.ready) return -1;
            if(LastUpdateTime < 0)
            {
                LastUpdateTime = Planetarium.GetUniversalTime();
                return TimeWarp.fixedDeltaTime;
            }
            var time = Planetarium.GetUniversalTime();
            var dT = time - LastUpdateTime;
            LastUpdateTime = time;
            return dT;
        }

        protected bool start_next_item()
        {
            CurrentTask = new T();
            if(Queue.Count > 0)
            {
                while(Queue.Count > 0 && !CurrentTask.Recheck())
                    CurrentTask = Queue.Dequeue();
                if(CurrentTask.Recheck())
                {
                    start();
                    return true;
                }
            }
            stop(true);
            return false;
        }

        protected abstract bool can_construct();
        protected abstract double do_some_work(double available_work);
        protected abstract void update_ETA();
        protected abstract void checkin();

        protected virtual void on_update() {}
        protected virtual void update_ui_data()
        {
            if(Queue.Count == 0) return;
            Queue = new PersistentQueue<T>(Queue.Where(task => task.Valid && !task.Complete));
        }

        void Update()
        {
            if(!HighLogic.LoadedSceneIsFlight) return;
            if(!FlightDriver.Pause && FlightGlobals.ready && Time.timeSinceLevelLoad > 1)
            {
                if(CurrentTask.Valid)
                {
                    if(CurrentTask.Recheck())
                    {
                        //update ETA if working
                        if(Working)
                        {
                            if(can_construct()) update_ETA();
                            else stop();
                        }
                        else if(CurrentTask.Complete)
                            stop(true);
                    }
                    else
                        stop(true);
                }
            }
            if(show_window)
                update_ui_data();
            on_update();
        }

        void FixedUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || !Working || workforce.Equals(0)) return;
            var deltaTime = get_delta_time();
            if(deltaTime < 0) return;
            //check current kit
            if(!CurrentTask.Recheck() && !start_next_item()) return;
            var available_work = workforce*deltaTime;
            while(Working && available_work > TimeWarp.fixedDeltaTime/10)
                available_work = do_some_work(available_work);
            if(deltaTime > TimeWarp.fixedDeltaTime*2)
            {
                update_ETA();
                checkin();
            }
        }

        #region GUI
        [KSPField(isPersistant = true)] public bool show_window;
        protected const float width = 550;
        protected const float height = 60;
        protected Rect WindowPos = new Rect((Screen.width-width)/2, Screen.height/4, width, height*4);
        protected string LockName = ""; //inited OnStart

        [KSPEvent(guiName = "Construction Window", guiActive = true, active = true)]
        public void ToggleWindow()
        { show_window = !show_window; }

        protected abstract void draw();
        protected virtual void unlock() {}

        void OnGUI()
        {
            if(Time.timeSinceLevelLoad < 3) return;
            if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
            if(show_window && GUIWindowBase.HUD_enabled && vessel.isActiveVessel)
            {
                Styles.Init();
                draw();
            }
            else
            {
                Utils.LockIfMouseOver(LockName, WindowPos, false);
                unlock();
            }
        }
        #endregion
    }
}

