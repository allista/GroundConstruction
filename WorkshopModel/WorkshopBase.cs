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
using System.Collections.Generic;

namespace GroundConstruction
{
    public interface IWorkshopTask : IConfigNode
    {
        Guid ID { get; }
        string Name { get; }
        bool Valid { get; }
        bool Complete { get; }
        bool Recheck();
        void Draw();
    }

    [Flags] public enum WorkshopType 
    { 
        NONE = 0,
        GROUND = 1, 
        ORBITAL = 1 << 1, 
        OMNI = GROUND | ORBITAL 
    }

    public abstract class WorkshopBase : PartModule
    {
        protected static Globals GLB { get { return Globals.Instance; } }

        protected float workforce = 0;
        protected float max_workforce = 0;
        protected double loadedUT = -1;

        public WorkshopManager Manager;

        [KSPField] public WorkshopType workshopType;
        [KSPField(isPersistant = true)] public bool Working;
        [KSPField(isPersistant = true)] public double LastUpdateTime = -1;
        [KSPField(isPersistant = true)] public double EndUT = -1;

        public bool isOperable
        {
            get
            {
                var status = string.Empty;
                return IsOperable(vessel, workshopType, ref status);
            }
        }

        public abstract string Stage_Display { get; }

        public string ETA_Display { get; protected set; } = "Stalled...";

        public string Workforce_Display =>
        string.Format("Workforce: {0:F1}/{1:F1} SK", workforce, max_workforce);

        public float Workforce => workforce;
        public virtual float EffectiveWorkforce => workforce;

        public static bool IsOperable(Vessel vsl, WorkshopType workshopType, ref string status)
        {
            status = string.Empty;
            switch(workshopType)
            {
            case WorkshopType.OMNI:
                return true;
            case WorkshopType.GROUND:
                if(vsl.Landed) return true;
                status = "The workshop can only operate when the vessel is landed";
                return false;
            case WorkshopType.ORBITAL:
                if(vsl.InOrbit()) return true;
                status = "The workshop can only operate when the vessel is in orbit";
                return false;
            }
            return false;
        }

        public abstract IWorkshopTask GetCurrentTask();

        public abstract void StartTask(IWorkshopTask task);

        protected virtual void onVesselPacked(Vessel vsl)
        {
            if(vsl != vessel) return;
            loadedUT = -1;
        }

        protected virtual void onVesselUpacked(Vessel vsl)
        {
            if(vsl != vessel) return;
            loadedUT = Planetarium.GetUniversalTime();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            LockName = GetType().Name + GetInstanceID();
            if(HighLogic.LoadedSceneIsFlight)
                loadedUT = -1;
        }

        protected virtual void update_max_workforce() =>
        max_workforce = part.CrewCapacity * 5;

        protected virtual void update_workforce<E>()
            where E : ExperienceEffect
        {
            workforce = 0;
            foreach(var kerbal in part.protoModuleCrew)
            {
                var trait = kerbal.experienceTrait;
                for(int i = 0, traitEffectsCount = trait.Effects.Count; i < traitEffectsCount; i++)
                    if(trait.Effects[i] is E)
                    {
                        workforce += Mathf.Max(trait.CrewMemberExperienceLevel(), 0.5f);
                        break;
                    }
            }
        }

        protected abstract void start();

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
        protected virtual void on_stop(bool reset) { }

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

        protected virtual bool can_construct()
        {
            if(workforce.Equals(0))
            {
                Utils.Message("No workers in the workshop.");
                return false;
            }
            if(loadedUT < 0 || Planetarium.GetUniversalTime() - loadedUT < 3)
                return true;
            var status = string.Empty;
            if(!IsOperable(vessel, workshopType, ref status))
            {
                Utils.Message("{0}: {1}", part.Title(), status);
                return false;
            }
            return true;
        }

        protected abstract bool start_next_item();
        protected abstract double do_some_work(double available_work);
        protected abstract void update_ETA();

        protected virtual void checkin()
        {
            if(Manager != null)
                Manager.CheckinWorkshop(this);
        }

        public override void OnAwake()
        {
            base.OnAwake();
            GameEvents.onVesselGoOnRails.Add(onVesselPacked);
            GameEvents.onVesselGoOffRails.Add(onVesselUpacked);
        }

        protected virtual void OnDestroy()
        {
            GameEvents.onVesselGoOnRails.Remove(onVesselPacked);
            GameEvents.onVesselGoOffRails.Remove(onVesselUpacked);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.AddValue("WindowPos", new Vector4(WindowPos.x, WindowPos.y, WindowPos.width, WindowPos.height));
            node.AddValue("Workforce_Display", Workforce_Display);
            var task = GetCurrentTask();
            if(task != null)
                node.AddValue("CurrentTaskName", task.Name);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            var wpos = node.GetValue("WindowPos");
            if(wpos != null)
            {
                var vpos = ConfigNode.ParseVector4(wpos);
                WindowPos = new Rect(vpos.x, vpos.y, vpos.z, vpos.w);
            }
        }

        #region GUI
        [KSPField(isPersistant = true)] public bool show_window;
        protected const float width = 550;
        protected const float height = 60;
        protected Rect WindowPos = new Rect((Screen.width - width) / 2, Screen.height / 4, width, height * 4);
        protected string LockName = ""; //inited OnStart

        [KSPEvent(guiName = "Workshop Window", guiActive = true, active = true)]
        public void ToggleWindow()
        { show_window = !show_window; }

        protected abstract void draw();
        protected virtual void unlock() { }

        protected void BeginScroll(int num_items, ref Vector2 scroll_pos, 
                                   int row_height = 60, int max_rows = 2)
        {
            scroll_pos = GUILayout.BeginScrollView(scroll_pos,
                                                   GUILayout.Height(row_height * Math.Min(num_items, max_rows)),
                                                   GUILayout.Width(width));
        }

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

        public string DeployableStatus(IDeployable deployable)
        {
            switch(deployable.State)
            {
            case DeplyomentState.IDLE:
                return "Idle";
            case DeplyomentState.DEPLOYING:
                return "Deploying";
            case DeplyomentState.DEPLOYED:
                return "Deployed";
            }
            return "";
        }
        #endregion
    }

    public abstract class WorkshopBase<T, E> : WorkshopBase
        where T : class, IWorkshopTask, new()
        where E : ExperienceEffect
    {
        [KSPField(isPersistant = true)] public PersistentQueue<T> Queue = new PersistentQueue<T>();
        [KSPField(isPersistant = true)] public T CurrentTask = new T();

        public override IWorkshopTask GetCurrentTask() => CurrentTask;

        public override void StartTask(IWorkshopTask task)
        {
            var Task = task as T;
            if(Task != null && CurrentTask.ID != Task.ID && 
               check_task(Task) && init_task(Task))
            {
                if(CurrentTask.Valid)
                {
                    Queue.Enqueue(CurrentTask);
                    stop(true);
                }
                reset_current_task();
                CurrentTask = Task;
                start();
            }
        }

        protected void reset_current_task() => CurrentTask = new T();

        protected virtual void update_workforce() => update_workforce<E>();

        protected void update_and_checkin(Vessel vsl)
        {
            if(vsl != null && vsl == vessel &&
               part.started && isEnabled)
            {
                if(Working && CurrentTask.Recheck())
                    update_ETA();
                else
                    update_workforce();
                checkin();
            }
        }

        protected override void start()
        {
            Working = true;
            if(check_task(CurrentTask))
                update_ETA();
            on_start();
            checkin();
        }
        protected virtual void on_start() { }

        protected virtual bool check_task(T task) => task.Recheck();
        protected abstract bool init_task(T task);
        protected override bool start_next_item()
        {
            reset_current_task();
            if(Queue.Count > 0)
            {
                while(Queue.Count > 0)
                {
                    var task = Queue.Peek();
                    if(check_task(task))
                    {
                        if(init_task(task))
                        {
                            CurrentTask = task;
                            Queue.Dequeue();
                            start();
                            return true;
                        }
                        break;
                    }
                }
            }
            stop(true);
            return false;
        }

        protected virtual void on_update() {}

        protected virtual void update_ui_data()
        {
            if(Queue.Count == 0) return;
            Queue = new PersistentQueue<T>(Queue.Where(task => task.Recheck() && !task.Complete));
        }

        public override void OnAwake()
        {
            base.OnAwake();
            GameEvents.onVesselCrewWasModified.Add(update_and_checkin);
        }

        protected override void OnDestroy()
        {
            Utils.LockIfMouseOver(LockName, WindowPos, false);
            GameEvents.onVesselCrewWasModified.Remove(update_and_checkin);
            base.OnDestroy();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if(HighLogic.LoadedSceneIsFlight)
            {
                update_workforce();
                update_max_workforce();
            }
        }

        void Update()
        {
            if(!HighLogic.LoadedSceneIsFlight) return;
            if(!FlightDriver.Pause && FlightGlobals.ready && Time.timeSinceLevelLoad > 1)
            {
                if(CurrentTask.Valid)
                {
                    if(check_task(CurrentTask))
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
                if(show_window && GUIWindowBase.HUD_enabled && vessel.isActiveVessel)
                    update_ui_data();
            }
            on_update();
        }

        void FixedUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || !Working || workforce.Equals(0)) return;
            var deltaTime = get_delta_time();
            if(deltaTime < 0) return;
            //check current kit
            //this.Log("Delta time: {}", deltaTime);//debug
            //this.Log("0 CurrentTask: {}, check {}", CurrentTask, check_task(CurrentTask));//debug
            if(!check_task(CurrentTask) && !start_next_item()) return;
            var available_work = workforce * deltaTime;
            //this.Log("1 CurrentTask: {}, check {}", CurrentTask, check_task(CurrentTask));//debug
            //this.Log("available work: {}", available_work);//debug
            while(Working && available_work > TimeWarp.fixedDeltaTime / 10)
                available_work = do_some_work(available_work);
            //this.Log("available work left: {}", available_work);//debug
            if(deltaTime > TimeWarp.fixedDeltaTime * 2)
            {
                if(Working)
                    update_ETA();
                checkin();
            }
        }

        #region GUI
        protected HashSet<T> highlighted_tasks = new HashSet<T>();
        protected T highlight_task;

        protected void set_highlighted_task(T task)
        {
            if(Event.current.type == EventType.Repaint && Utils.MouseInLastElement())
                highlight_task = task;
        }

        Vector2 queue_scroll = Vector2.zero;
        protected virtual void queue_pane()
        {
            if(Queue.Count > 0)
            {
                GUILayout.Label("Construction Queue", Styles.label, GUILayout.ExpandWidth(true));
                GUILayout.BeginVertical(Styles.white);
                BeginScroll(Queue.Count, ref queue_scroll);
                T del = null;
                T up = null;
                foreach(var task in Queue)
                {
                    GUILayout.BeginHorizontal();
                    task.Draw();
                    set_highlighted_task(task);
                    if(GUILayout.Button(new GUIContent("^", "Move up"),
                                        Styles.normal_button, GUILayout.Width(25)))
                        up = task;
                    if(GUILayout.Button(new GUIContent("X", "Remove from Queue"),
                                        Styles.danger_button, GUILayout.Width(25)))
                        del = task;
                    GUILayout.EndHorizontal();
                }
                if(del != null) Queue.Remove(del);
                else if(up != null) Queue.MoveUp(up);
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
        }
        #endregion
    }
}

