//   ProtoWorkshopBase.cs
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
    public class ProtoWorkshop : VesselInfo
    {
        public enum Status { IDLE, ACTIVE, COMPLETE };

        [Persistent] public uint id;
        [Persistent] public string VesselName;
        [Persistent] public string PartName;
        [Persistent] public string TaskName;
        [Persistent] public string Stage;
        [Persistent] public Status State;
        [Persistent] public double EndUT;
        [Persistent] public string ETA;
        [Persistent] public string Workforce;
        [Persistent] public WorkshopType workshopType;
        [Persistent] public bool isOperable;

        public ProtoWorkshop() { }
        public ProtoWorkshop(WorkshopBase workshop)
        {
            VesselName = workshop.vessel.name;
            vesselID = workshop.vessel.id;
            workshopType = workshop.workshopType;
            isOperable = workshop.isOperable;
            id = workshop.part.flightID;
            PartName = workshop.part.partInfo.title;
            Workforce = workshop.Workforce_Display;
            Stage = workshop.Stage_Display;
            State = Status.IDLE;
            EndUT = workshop.EndUT;
            var task = workshop.GetCurrentTask();
            if(task != null && task.Valid)
                update(task.Name);
        }

        public ProtoWorkshop(Guid vID, string vesselName,
                             uint flightID, string partName,
                             ConfigNode protoModuleValues)
        {
            vesselID = vID;
            VesselName = vesselName;
            id = flightID;
            PartName = partName;
            State = Status.IDLE;
            Workforce = protoModuleValues.GetValue("Workforce_Display");
            var val = protoModuleValues.GetValue("EndUT");
            if(string.IsNullOrEmpty(val) || !double.TryParse(val, out EndUT))
                EndUT = -1;
            var task_name = protoModuleValues.GetValue("CurrentTaskName");
            if(task_name != null) update(task_name);
        }

        void update(string task_name)
        {
            State = Status.ACTIVE;
            TaskName = task_name;
            if(EndUT > 0 && EndUT < Planetarium.GetUniversalTime())
                State = Status.COMPLETE;
        }

        public WorkshopBase GetWorkshop(Vessel vsl)
        {
            if(vsl == null || !vsl.loaded) return null;
            var part = vsl[id];
            if(part == null) return null;
            return part.Modules.GetModule<WorkshopBase>();
        }

        public WorkshopBase GetWorkshop()
        { return GetWorkshop(GetVessel()); }

        public void ToggleConstructionWindow()
        {
            if(IsActive)
            {
                var workshop = GetWorkshop();
                if(workshop != null)
                    workshop.ToggleWindow();
            }
        }

        public bool CheckETA(double now)
        {
            if(State != Status.ACTIVE) return false;
            if(EndUT > 0 && EndUT < now)
            {
                Utils.Message(10, "Engineers at '{0}' should have completed the work on the '{1}' by now.",
                              VesselName, TaskName);
                State = Status.COMPLETE;
                EndUT = -1;
                return true;
            }
            ETA = EndUT > 0 ? $"Time left: {Utils.formatTimeDelta(EndUT - now)}" : "Stalled...";
            return false;
        }

        public static readonly GUIContent WarpToButton = new GUIContent("▶▶", "Warp to the end of the work.");

        public void Draw()
        {
            GUILayout.BeginHorizontal();
            var style = Styles.white;
            GUIContent status = null;
            var tooltip = "\nPress to open Workshop Window";
            if(State == Status.IDLE)
            {
                if(IsActive)
                {
                    tooltip = Stage + "\n" + Workforce + tooltip;
                    status = new GUIContent(PartName + ": Idle", tooltip);
                }
            }
            else
            {
                tooltip = Stage + "\n" + PartName + (IsActive ? tooltip : "");
                if(State == Status.ACTIVE)
                {
                    style = EndUT > 0 ? Styles.active : Styles.danger;
                    status = new GUIContent($"{TaskName}: {ETA}", tooltip);
                }
                else
                {
                    style = Styles.enabled;
                    status = new GUIContent(TaskName + ": Complete", tooltip);
                }
            }
            if(status != null)
            {
                if(GUILayout.Button(status, style, GUILayout.ExpandWidth(true)))
                    ToggleConstructionWindow();
                if(EndUT > 0 &&
                   TimeWarp.fetch != null &&
                   GUILayout.Button(WarpToButton, Styles.enabled_button, GUILayout.ExpandWidth(false)))
                    TimeWarp.fetch.WarpTo(EndUT);
            }
            GUILayout.EndHorizontal();
        }

        //deprecated config coversion
        public override void Load(ConfigNode node)
        {
            workshopType = WorkshopType.GROUND;
            isOperable = true;
            base.Load(node);
            var kit_name = node.GetValue("KitName");
            if(kit_name != null)
                TaskName = kit_name;
        }
    }
}

