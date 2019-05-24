//   GCEditorGUI.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2019 Allis Tauri

using System;
using UnityEngine;
using AT_Utils;
using AT_Utils.UI;
using KSP.UI.Screens;
using System.Collections.Generic;

namespace GroundConstruction
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class GCEditorGUI : AddonWindowBase<GCEditorGUI>
    {
        VesselKit AssembleKit;
        VesselKit ConstructionKit;
        DockingNodeList DockingNodes => AssembleKit?.DockingNodes;
        bool update;

        bool highlight_all;
        ConstructDockingNode highlight_node = null;
        Dictionary<uint, Part> highlighted_parts = new Dictionary<uint, Part>();

        bool all_highlighted =>
        highlighted_parts.Count > 0
        && AssembleKit != null
            && highlighted_parts.Count == AssembleKit.DockingNodes.Count;

        public override void Awake()
        {
            base.Awake();
            Title = "Global Construction Info";
            width = 400;
            height = 200;
            WindowPos = new Rect(Screen.width / 2 - width / 2, Screen.height / 2 - height / 2, width, height);
            GameEvents.onEditorShipModified.Add(OnShipModified);
            GameEvents.onEditorLoad.Add(OnShipLoad);
            GameEvents.onEditorRestart.Add(Restart);
            GameEvents.onEditorStarted.Add(Started);
            Show(false);
        }

        public override void OnDestroy()
        {
            disable_highlights();
            GameEvents.onEditorShipModified.Remove(OnShipModified);
            GameEvents.onEditorLoad.Remove(OnShipLoad);
            GameEvents.onEditorRestart.Remove(Restart);
            GameEvents.onEditorStarted.Remove(Started);
            base.OnDestroy();
        }

        void Started()
        {
            update = true;
        }

        void Restart()
        {
            update = true;
        }

        void OnShipLoad(ShipConstruct ship, CraftBrowserDialog.LoadType load_type)
        {
            update = true;
        }

        void OnShipModified(ShipConstruct ship)
        {
            update = true;
        }

        void Update()
        {
            var ship = EditorLogic.fetch?.ship;
            if(update)
            {
                highlight_all = all_highlighted;
                disable_highlights();
                if(ship != null)
                {
                    AssembleKit = new VesselKit(null, ship, false, true);
                    ConstructionKit = new VesselKit(null, ship, true, true);
                }
                update = false;
            }
            if(highlight_node != null)
            {
                Part part;
                if(highlighted_parts.TryGetValue(highlight_node.PartId, out part))
                {
                    part.SetHighlightDefault();
                    highlighted_parts.Remove(highlight_node.PartId);
                }
                else if(ship != null)
                {
                    part = ship.Parts.GetPartByCraftID(highlight_node.PartId);
                    if(part != null)
                        highlighted_parts.Add(part.craftID, part);
                }
                highlight_node = null;
            }
            if(highlight_all)
            {
                if(all_highlighted)
                    disable_highlights();
                else if(ship != null)
                {
                    foreach(var n in DockingNodes)
                    {
                        var part = ship.Parts.GetPartByCraftID(n.PartId);
                        if(part != null && !highlighted_parts.ContainsKey(part.craftID))
                            highlighted_parts.Add(part.craftID, part);
                    }
                }
                highlight_all = false;
            }
            if(highlighted_parts.Count > 0)
            {
                foreach(var p in highlighted_parts.Values)
                    p.HighlightAlways(Colors.Active.color);
            }
        }

        void disable_highlights()
        {
            foreach(var p in highlighted_parts.Values)
                p.SetHighlightDefault();
            highlighted_parts.Clear();
        }

        Vector2 scroll;
        void attach_nodes_pane()
        {
            GUILayout.BeginVertical(Styles.white);
            if(AssembleKit != null)
            {
                GUILayout.Label("Assembly requirements", Styles.label, GUILayout.ExpandWidth(true));
                AssembleKit.Draw();
            }
            if(ConstructionKit != null)
            {
                GUILayout.Label("Construction requirements", Styles.label, GUILayout.ExpandWidth(true));
                ConstructionKit.Draw();
            }
            GUILayout.Label("Attach nodes for docked construction", Styles.label, GUILayout.ExpandWidth(true));
            if(GUILayout.Button("Highight all nodes",
                                all_highlighted ? Styles.enabled_button : Styles.active_button,
                                GUILayout.ExpandWidth(true)))
                highlight_all = true;
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(150));
            foreach(var n in DockingNodes)
            {
                if(GUILayout.Button(n.ToString(),
                                    highlighted_parts.ContainsKey(n.PartId)? Styles.active : Styles.white, 
                                    GUILayout.ExpandWidth(true)))
                    highlight_node = n;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        void draw(int windowId)
        {
            attach_nodes_pane();
            TooltipsAndDragWindow();
        }

        protected override void draw_gui()
        {
            LockControls();
            WindowPos =
                GUILayout.Window(GetInstanceID(),
                                 WindowPos,
                                 draw,
                                 Title,
                                 GUILayout.Width(width),
                                 GUILayout.Height(height)).clampToScreen();
        }
    }
}
