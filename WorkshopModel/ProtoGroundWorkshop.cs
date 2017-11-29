//   ProtoGroundWorkshop.cs
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
    public class ProtoGroundWorkshop : VesselInfo
    {
        public enum Status { IDLE, ACTIVE, COMPLETE };

        [Persistent] public uint   id;
        [Persistent] public string VesselName;
        [Persistent] public string PartName;
        [Persistent] public string KitName;
        [Persistent] public Status State;
        [Persistent] public double EndUT;
        [Persistent] public string ETA;
        [Persistent] public string Workforce;

        public ProtoGroundWorkshop() {}
        public ProtoGroundWorkshop(GroundWorkshop workshop)
        {
            VesselName = workshop.vessel.name;
            vesselID = workshop.vessel.id;
            id = workshop.part.flightID;
            PartName = workshop.part.partInfo.title;
            Workforce = workshop.Workforce_Display;
            State = Status.IDLE;
            EndUT = workshop.EndUT;
            if(workshop.KitUnderConstruction.Valid)
                update_from_kit(workshop.KitUnderConstruction);
        }

        public ProtoGroundWorkshop(Guid vID, string vesselName,
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
            var node = protoModuleValues.GetNode("KitUnderConstruction");
            if(node != null)
            {
                var kit = ConfigNodeObject.FromConfig<GroundWorkshop.KitInfo>(node);
                if(kit.Valid) update_from_kit(kit);
            }
        }

        void update_from_kit(GroundWorkshop.KitInfo kit)
        {
            State = Status.ACTIVE;
            KitName = kit.KitName;
            if(EndUT > 0 && EndUT < Planetarium.GetUniversalTime())
                State = Status.COMPLETE;
        }

        public GroundWorkshop GetWorkshop(Vessel vsl)
        {
            if(vsl == null || !vsl.loaded) return null;
            var part = vsl[id];
            if(part == null) return null;
            return part.Modules.GetModule<GroundWorkshop>();
        }

        public GroundWorkshop GetWorkshop()
        { return GetWorkshop(GetVessel()); }

        public void ToggleConstructionWindow()
        {
            if(IsActive)
            {
                var workshop = GetWorkshop();
                if(workshop != null)
                    workshop.ToggleConstructionWindow();
            }
        }

        public bool CheckETA(double now)
        {
            if(State != ProtoGroundWorkshop.Status.ACTIVE) return false;
            if(EndUT > 0 && EndUT < now)
            {
                Utils.Message(10, "Engineers at '{0}' should have assembled the '{1}' by now.",
                              VesselName, KitName);
                State = ProtoGroundWorkshop.Status.COMPLETE;
                EndUT = -1;
                return true;
            }
            ETA = EndUT > 0?
                "Time left: "+KSPUtil.PrintTimeCompact(EndUT-now, false) : "Stalled...";
            return false;
        }

        public static readonly GUIContent WarpToButton = new GUIContent("▶▶", "Warp to the end of construction.");

        public void Draw()
        {
            GUILayout.BeginHorizontal();
            var style = Styles.white;
            GUIContent status = null;
            var tooltip = "\nPress to open Construction Window";
            if(State == Status.IDLE)
            {
                if(IsActive)
                {
                    tooltip = Workforce+tooltip;
                    status = new GUIContent(PartName+": Idle", tooltip);
                }
            }
            else
            {
                tooltip = PartName+(IsActive? tooltip : "");
                if(State == Status.ACTIVE)
                {
                    style = EndUT > 0? Styles.yellow : Styles.red;
                    status = new GUIContent(KitName+": "+ETA, tooltip);
                }
                else
                {
                    style = Styles.green;
                    status = new GUIContent(KitName+": Complete", tooltip);
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

        public override string ToString()
        {
            if(State == Status.COMPLETE)
                return Utils.Format("\"{}:{}\" assembled \"{}\".", VesselName, PartName, KitName);
            if(State == Status.ACTIVE)
                return Utils.Format("\"{}:{}\" is building \"{}\". {}", VesselName, PartName, KitName, ETA);
            return Utils.Format("\"{}:{}\" is idle.", VesselName, PartName);
        }
    }
}

