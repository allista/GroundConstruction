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
        bool Draw(GUIStyle style = null);
    }

    [Flags]
    public enum WorkshopType
    {
        NONE = 0,
        GROUND = 1,
        ORBITAL = 1 << 1,
        OMNI = GROUND | ORBITAL
    }

    public abstract class WorkshopBase : PartModule
    {
        protected static Globals GLB => Globals.Instance;

        protected float workforce = 0;
        protected float max_workforce = 0;
        protected double loadedUT = -1;

        public WorkshopManager Manager;

        [KSPField] public WorkshopType workshopType;
        [KSPField(isPersistant = true)] public bool Working;
        [KSPField(isPersistant = true)] public double LastUpdateTime = -1;
        [KSPField(isPersistant = true)] public double EndUT = -1;

        public bool isOperable => IsOperable(vessel, workshopType, out _);

        public abstract string Stage_Display { get; }

        public string ETA_Display { get; protected set; } = "Stalled...";

        public string Workforce_Display => $"Workforce: {workforce:F1}/{max_workforce:F1} SK";

        public float Workforce => workforce;
        public virtual float EffectiveWorkforce => workforce;

        public static bool IsOperable(Vessel vsl, WorkshopType workshopType, out string status)
        {
            status = string.Empty;
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch(workshopType)
            {
                case WorkshopType.OMNI:
                    return true;
                case WorkshopType.GROUND:
                    if(vsl.Landed)
                        return true;
                    status = "The workshop can only operate when the vessel is landed";
                    return false;
                case WorkshopType.ORBITAL:
                    if(vsl.InOrbit())
                        return true;
                    status = "The workshop can only operate when the vessel is in orbit";
                    return false;
                default:
                    return false;
            }
        }

        public abstract IWorkshopTask GetCurrentTask();

        public abstract IEnumerator<YieldInstruction> StartTask(IWorkshopTask task);

        protected virtual void onVesselPacked(Vessel vsl)
        {
            if(vsl != vessel)
                return;
            loadedUT = -1;
        }

        protected virtual void onVesselUpacked(Vessel vsl)
        {
            if(vsl != vessel)
                return;
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
            max_workforce = part.CrewCapacity * KerbalRoster.GetExperienceMaxLevel();

        protected abstract void update_workforce();

        protected abstract void start();

        protected void stop(bool reset = false)
        {
            Working = false;
            EndUT = -1;
            ETA_Display = "";
            LastUpdateTime = -1;
            Utils.StopTimeWarp();
            on_stop(reset);
            checkin();
        }

        protected virtual void on_stop(bool reset) { }

        protected virtual bool can_construct()
        {
            if(workforce.Equals(0))
            {
                Utils.Message("No workers in the workshop.");
                return false;
            }
            if(loadedUT < 0 || Planetarium.GetUniversalTime() - loadedUT < 3)
                return true;
            if(!IsOperable(vessel, workshopType, out var status))
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
        {
            show_window = !show_window;
        }

        protected abstract void draw();
        protected virtual void unlock() { }

        protected static void BeginScroll(
            int num_items,
            ref Vector2 scroll_pos,
            int row_height = 60,
            int max_rows = 2
        )
        {
            scroll_pos = GUILayout.BeginScrollView(scroll_pos,
                GUILayout.Height(row_height * Math.Min(num_items, max_rows)),
                GUILayout.Width(width));
        }

        private void OnGUI()
        {
            if(Time.timeSinceLevelLoad < 3)
                return;
            if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
                return;
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

        public static string DeployableStatus(IDeployable deployable)
        {
            switch(deployable.State)
            {
                case DeploymentState.IDLE:
                    return "Idle";
                case DeploymentState.DEPLOYING:
                    return $"Deploying\n{deployable.DeploymentInfo}";
                case DeploymentState.DEPLOYED:
                    return "Deployed";
                default:
                    return "";
            }
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

        public override IEnumerator<YieldInstruction> StartTask(IWorkshopTask task)
        {
            if(!(task is T Task) || !check_task(Task))
                yield break;
            if(CurrentTask.ID != Task.ID)
            {
                if(!init_task(Task))
                    yield break;
                if(CurrentTask.Valid)
                {
                    Queue.Enqueue(CurrentTask);
                    stop(true);
                }
                reset_current_task();
                CurrentTask = Task;
            }
            if(workforce <= 0)
            {
                get_required_crew();
                yield return null;
                update_workforce();
            }
            start();
        }

        protected void reset_current_task() => CurrentTask = new T();

        protected Type worker_effect => typeof(E);

        protected override void update_workforce() => workforce = ConstructionUtils.PartWorkforce<E>(part, 0.5f);

        protected int do_crew_transfer;

        private void get_required_crew()
        {
            if(part.CrewCapacity <= part.protoModuleCrew.Count)
                return;
            var sortedCrew = vessel.GetVesselCrew();
            sortedCrew.Sort((a, b) => -a.experienceLevel.CompareTo(b.experienceLevel));
            CrewTransferBatch.moveCrew(vessel,
                part,
                ConstructionUtils.GetCrewWithEffect<E>(sortedCrew.Where(k =>
                        !k.KerbalRef.InPart.HasModuleImplementing<WorkshopBase>()))
                    .ToList());
        }

        private void dismiss_crew()
        {
            if(part.protoModuleCrew.Count == 0)
                return;
            CrewTransferBatch.moveCrew(part,
                vessel.Parts.Where(p => !p.HasModuleImplementing<WorkshopBase>()).ToList());
        }

        private void update_and_checkin(Vessel vsl)
        {
            if(vsl != null && vsl == vessel && part.started && isEnabled)
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
                    Queue.Dequeue();
                }
            }
            stop(true);
            return false;
        }

        protected virtual void on_update()
        {
            //transfer required crew if requested
            if(do_crew_transfer > 0)
                get_required_crew();
            else if(do_crew_transfer < 0)
                dismiss_crew();
            do_crew_transfer = 0;
            //highlight kit under the mouse
            disable_highlights();
            if(highlight_part != null)
            {
                highlight_part.HighlightAlways(Color.yellow);
                highlighted_parts.Add(highlight_part);
            }
            highlight_part = null;
        }

        protected virtual void update_ui_data()
        {
            if(Queue.Count == 0)
                return;
            Queue = new PersistentQueue<T>(Queue.Where(task => check_task(task) && !task.Complete));
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

        private void Update()
        {
            if(!HighLogic.LoadedSceneIsFlight)
                return;
            if(!FlightDriver.Pause && FlightGlobals.ready && Time.timeSinceLevelLoad > 1)
            {
                if(CurrentTask.Valid)
                {
                    if(check_task(CurrentTask))
                    {
                        //update ETA if working
                        if(Working)
                        {
                            if(can_construct())
                                update_ETA();
                            else
                                stop();
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

        private void FixedUpdate()
        {
            if(!Working || !HighLogic.LoadedSceneIsFlight)
                return;
            if(EffectiveWorkforce.Equals(0))
            {
                stop();
                return;
            }
            var deltaTime = ConstructionUtils.GetDeltaTime(ref LastUpdateTime);
            if(deltaTime < 0)
                return;
            //check current kit
//            this.Log($"FixedUpdate dT {deltaTime}");//debug
//            this.Log($"FixedUpdate CurrentTask 0: {CurrentTask}, check {check_task(CurrentTask)}");//debug
            if(!check_task(CurrentTask) && !start_next_item())
                return;
            var available_work = workforce * deltaTime;
//            this.Log($"FixedUpdate CurrentTask 0: {CurrentTask}, check {check_task(CurrentTask)}");//debug
//            this.Log($"FixedUpdate available work: {available_work}");//debug
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
        protected HashSet<Part> highlighted_parts = new HashSet<Part>();
        protected Part highlight_part;
        protected T selected_task;

        protected readonly GUIContent getCrewButton =
            new GUIContent("Get Crew",
                "Try to transfer required crew TO this workshop from other <i>non-workshop</i> parts of the vessel.");

        protected readonly GUIContent dismissCrewButton =
            new GUIContent("Dismiss Crew",
                "Try to transfer all crew FROM this workshop to other <i>non-workshop</i> parts of the vessel.");

        protected abstract void set_highlighted_task(T task);

        protected void set_highlighted_part(Part p)
        {
            if(Event.current.type == EventType.Repaint && Utils.MouseInLastElement())
                highlight_part = p;
        }

        protected void draw_task(T task)
        {
            var is_selected = selected_task != null && task.ID == selected_task.ID;
            if(task.Draw(is_selected ? Styles.normal_button : Styles.white))
            {
                if(is_selected)
                    selected_task = null;
                else
                    selected_task = task;
            }
            set_highlighted_task(task);
        }

        protected void disable_highlights()
        {
            if(highlighted_parts.Count > 0)
            {
                foreach(var p in highlighted_parts)
                {
                    if(p != null && (highlight_part == null || p != highlight_part))
                    {
                        p.SetHighlightDefault();
                    }
                }
                highlighted_parts.Clear();
            }
        }

        protected void clear_if_selected(T task)
        {
            if(task != null
               && selected_task != null
               && task.ID == selected_task.ID)
                selected_task = null;
        }

        private Vector2 queue_scroll = Vector2.zero;

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
                    draw_task(task);
                    if(GUILayout.Button(new GUIContent("^", "Move up"),
                        Styles.normal_button,
                        GUILayout.Width(25)))
                        up = task;
                    if(GUILayout.Button(new GUIContent("X", "Remove from Queue"),
                        Styles.danger_button,
                        GUILayout.Width(25)))
                        del = task;
                    GUILayout.EndHorizontal();
                }
                if(del != null)
                {
                    Queue.Remove(del);
                    clear_if_selected(del);
                }
                else if(up != null)
                    Queue.MoveUp(up);
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
        }
        #endregion
    }
}
