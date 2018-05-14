//   GroundConstructionScenario.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new []
    {
        GameScenes.SPACECENTER,
        GameScenes.FLIGHT,
        GameScenes.TRACKSTATION,
    })]
    public class GroundConstructionScenario : ScenarioModule
    {
        static Globals GLB { get { return Globals.Instance; } }

        static SortedDictionary<Guid,WorkshopManager> Workshops = new SortedDictionary<Guid,WorkshopManager>();
        static SortedDictionary<string,Guid> DisplayOrder = new SortedDictionary<string,Guid>();
        static List<string> CelestialBodies = new List<string>();
        static string CelestialBodyTab = "";
        public static bool ShowDeployHint;
        double now = -1;

        public static void CheckinVessel(WorkshopManager workshop_manager)
        {
            if(workshop_manager.Vessel == null || workshop_manager.Empty) return;
            Workshops[workshop_manager.VesselID] = workshop_manager;
            remove_display_entry(workshop_manager.VesselID);
            DisplayOrder[workshop_manager.DisplayID] = workshop_manager.VesselID;
        }

        public static void CheckoutVessel(WorkshopManager workshop_manager)
        {
            if(workshop_manager.Vessel == null) return;
            Workshops.Remove(workshop_manager.VesselID);
            DisplayOrder.Remove(workshop_manager.DisplayID);
        }

        public static void CheckoutVessel(Guid vesselID)
        {
            Workshops.Remove(vesselID);
            remove_display_entry(vesselID);
        }

        static void remove_display_entry(Guid vesselID)
        {
            var del = DisplayOrder.FirstOrDefault(i => i.Value == vesselID);
            if(!string.IsNullOrEmpty(del.Key)) DisplayOrder.Remove(del.Key);
        }

        static bool reckeck_workshops()
        {
            if(FlightGlobals.Vessels != null && FlightGlobals.Vessels.Count > 0)
            {
                var tab_valid = false;
                var bodies = new HashSet<string>();
                var del = new List<Guid>();
                foreach(var workshop in Workshops)
                {
                    if(workshop.Value != null &&
                       workshop.Value.Vessel != null &&
                       workshop.Value.VesselID == workshop.Key &&
                       !workshop.Value.Empty)
                    {
                        if(workshop.Value.IsLanded)
                        {
                            var cb = workshop.Value.CB;
                            bodies.Add(cb);
                            tab_valid |= cb == CelestialBodyTab;
                        }
                    }
                    else del.Add(workshop.Key);
                }
                del.ForEach(CheckoutVessel);
                CelestialBodies.Clear();
                CelestialBodies.AddRange(bodies);
                CelestialBodies.Sort();
                if(!tab_valid && CelestialBodies.Count > 0)
                    CelestialBodyTab = CelestialBodies[0];
                return true;
            }
            return false;
        }

        // Analysis disable once FunctionNeverReturns
        IEnumerator<YieldInstruction> slow_update()
        {
            while(true)
            {
                reckeck_workshops();
                var finished = false;
                now = Planetarium.GetUniversalTime();
                foreach(var workshop in Workshops.Values)
                    finished = workshop.CheckETA(now) || finished;
                if(finished) TimeWarp.SetRate(0, !HighLogic.LoadedSceneIsFlight);
                yield return new WaitForSeconds(1);
            }
        }

        public override void OnAwake()
        {
            base.OnAwake();
            StartCoroutine(slow_update());
        }

        void OnDestroy()
        {
            Utils.LockIfMouseOver(LockName, WindowPos, false);
        }

        void Update()
        {
            if(switchto != null)
            {
                if(!switchto.SwitchTo())
                    CheckoutVessel(switchto);
                switchto = null;
            }
        }

        #region GUI
        const float width = 500;
        const float height = 120;
        const float cb_width = 60;
        const float workshops_width = width-cb_width-10;

        static bool show_window;
        public static void ShowWindow(bool show) { show_window = show; }
        public static void ToggleWindow() { show_window = !show_window; }

        WorkshopManager switchto = null;

        Vector2 workshops_scroll = Vector2.zero;
        Vector2 cb_scroll = Vector2.zero;
        Rect WindowPos = new Rect(Screen.width-width-100, 0, Screen.width/4, Screen.height/4);
        void main_window(int WindowID)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            if(CelestialBodies.Count > 0)
            {
                GUILayout.BeginVertical();
                cb_scroll = GUILayout.BeginScrollView(cb_scroll, GUILayout.Height(height), GUILayout.Width(cb_width+10));
                foreach(var cb in CelestialBodies)
                {
                    if(GUILayout.Button(new GUIContent(cb, "Show workshops on "+cb),
                                        CelestialBodyTab == cb? Styles.green : Styles.yellow,
                                        GUILayout.Width(cb_width)))
                        CelestialBodyTab = cb;
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUILayout.BeginVertical(Styles.white);
                workshops_scroll = GUILayout.BeginScrollView(workshops_scroll, GUILayout.Height(height), GUILayout.Width(workshops_width));
                foreach(var item in DisplayOrder.Values)
                {
                    var info = Workshops[item];
                    if(!info.IsLanded || info.CB != CelestialBodyTab) continue;
                    GUILayout.BeginHorizontal();
                    info.Draw();
                    if(info.IsActive)
                        GUILayout.Label(new GUIContent("◉", "This is the active vessel"),
                                        Styles.grey, GUILayout.ExpandWidth(false));
                    else if(GUILayout.Button(new GUIContent("◉", "Switch to this workshop"),
                                             Styles.enabled_button, GUILayout.ExpandWidth(false)))
                        switchto = info;
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
            else GUILayout.Label("No Ground Workshops", Styles.white, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Utils.ButtonSwitch("Show Deploy Hints", ref ShowDeployHint,
                               "Draw visual cues to help position a DIY Kit",
                               GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("Close", Styles.close_button, GUILayout.ExpandWidth(false)))
                show_window = false;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUIWindowBase.TooltipsAndDragWindow();
        }

        const string LockName = "GroundConstructionScenario";
        void OnGUI()
        {
            if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
            if(show_window && GUIWindowBase.HUD_enabled)
            {
                Styles.Init();
                Utils.LockIfMouseOver(LockName, WindowPos);
                WindowPos = GUILayout.Window(GetInstanceID(),
                                             WindowPos, main_window, "Ground Workshops",
                                             GUILayout.Width(width),
                                             GUILayout.Height(height)).clampToScreen();
            }
            else Utils.LockIfMouseOver(LockName, WindowPos, false);
        }
        #endregion
    }
}

