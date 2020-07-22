//   OneTimeResourceConverter.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri

using System.Collections.Generic;
using UnityEngine;
using AT_Utils;
using AT_Utils.UI;

namespace GroundConstruction
{
    public class OneTimeResourceConverter : PartModule
    {
        [KSPField] public string ConvertFrom = "";
        PartResourceDefinition old_res;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            old_res = PartResourceLibrary.Instance.GetDefinition(ConvertFrom);
            if(!HighLogic.LoadedSceneIsFlight ||
               old_res == null || old_res.name == Globals.Instance.ConstructionResource.name)
                this.EnableModule(false);
            else
            {
                this.EnableModule(true);
                part.UpdatePartMenu();
            }
        }

        Vessel to_convert;
        IEnumerator<YieldInstruction> convert_vessel(Vessel vsl)
        {
            yield return null;
            var GLB = Globals.Instance;
            var ratio = old_res.density / GLB.ConstructionResource.def.density;
            foreach(var p in vsl.Parts)
            {
                var res = p.Resources.Get(old_res.id);
                if(res == null) continue;
                var tanks = p.Modules.GetModules<ModuleSwitchableTank>();
                var tank = tanks.Find(t => t.Resource != null && t.Resource.resourceName == old_res.name);
                if(tank == null)
                {
                    var new_amount = res.amount * ratio;
                    var new_max = res.maxAmount * ratio;
                    if(new_amount > new_max) new_amount = new_max;
                    var existing_res = p.Resources.Get(GLB.ConstructionResource.id);
                    //if there's already new resource in this part, transfer to it as much mass as possible
                    if(existing_res != null)
                    {
                        var space = existing_res.maxAmount - existing_res.amount;
                        if(space > new_amount) existing_res.amount += new_amount;
                        else
                        {
                            existing_res.amount = existing_res.maxAmount;
                            new_amount -= space;
                        }
                        res.amount = new_amount / ratio;
                        if(res.amount.Equals(0))
                            p.RemoveResource(res);
                    }
                    else //convert all by mass, then remove resource
                    {
                        p.Resources.Add(
                            GLB.ConstructionResource.name,
                            new_amount,
                            new_max,
                            res.flowState,
                            res.isTweakable,
                            res.hideFlow,
                            res.isVisible,
                            res.flowMode
                        );
                        p.RemoveResource(res);
                    }
                }
                else
                {
                    var new_amount = tank.Resource.amount * ratio;
                    var existing_tank = tanks.Find(t => t.Resource != null && t.Resource.resourceName == GLB.ConstructionResource.name);
                    //if there's already such a tank, transfer new resource into it, as much as possible
                    if(existing_tank != null)
                    {
                        var space = existing_tank.MaxAmount - existing_tank.Amount;
                        if(space > new_amount) existing_tank.Amount = existing_tank.Amount + new_amount;
                        else
                        {
                            existing_tank.Amount = existing_tank.MaxAmount;
                            new_amount -= space;
                        }
                        tank.Amount = new_amount / ratio;
                    }
                    else //convert tank type and tank resource
                    {
                        var old_amount = tank.Amount;
                        if(tank.ForceSwitchResource(GLB.ConstructionResource.name))
                            tank.Amount = new_amount;
                        else //if failed, try to switch back and revert
                        {
                            Utils.Message("WARNING: Unable to switch the SwitchableTank in the '{0}' to the new resource.", p.name);
                            if(tank.ForceSwitchResource(res.resourceName))
                                tank.Amount = old_amount;
                            else Utils.Message("ERROR: Unable to switch the SwitchableTank in the '{0}' back to its original resource.", p.name);
                        }
                    }
                }
            }
        }

        [KSPEvent(guiName = "Old GC Resources", guiActive = true, active = true)]
        public void Convert() { show_window = true; }

        bool show_window;
        const float width = 350;
        const float height = 150;
        Rect WindowPos = new Rect((Screen.width - width) / 2, Screen.height / 4, width, height * 4);
        Vector2 vessels_scroll = Vector2.zero;

        void main_window(int WindowID)
        {
            GUILayout.BeginVertical();
            vessels_scroll = GUILayout.BeginScrollView(vessels_scroll, Styles.white);
            foreach(var vsl in FlightGlobals.VesselsLoaded)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(vsl.vesselName, Styles.white, GUILayout.ExpandWidth(true));
                if(GUILayout.Button("Convert", Styles.danger_button, GUILayout.ExpandWidth(false)))
                {
                    if(to_convert == null)
                    {
                        to_convert = vsl;
                        DialogFactory.Danger(
                            string.Format(
                                "This will convert '{0}' resource into '{1}' by mass in every part that contains it.\n"
                                + "<color=red><b>This cannot be undone!</b></color>\n"
                                + "It is best that you <b>save the game</b> before doing this.\n"
                                + "Are you sure you wish to continue?",
                                old_res.name,
                                Globals.Instance.ConstructionResource.name),
                            () => StartCoroutine(convert_vessel(to_convert)),
                            onClose: () => to_convert = null,
                            context: this
                        );
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if(GUILayout.Button("Close", Styles.close_button, GUILayout.ExpandWidth(true)))
                show_window = false;
            GUILayout.EndVertical();
            GUIWindowBase.TooltipsAndDragWindow();
        }

        void OnGUI()
        {
            if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
            if(show_window && GUIWindowBase.HUD_enabled && vessel.isActiveVessel)
            {
                Styles.Init();
                WindowPos = GUILayout.Window(GetInstanceID(),
                                             WindowPos, main_window,
                                             "Convert old GC resources in nearby vessels",
                                             GUILayout.Width(width),
                                             GUILayout.Height(height)).clampToScreen();
            }
        }
    }
}

