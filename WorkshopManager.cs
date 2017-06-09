//   WorkshopManager.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
    public class WorkshopManager : VesselModule
    {
        public Dictionary<uint, GroundWorkshop> Workshops = new Dictionary<uint, GroundWorkshop>();
        public Dictionary<uint, ProtoGroundWorkshop> ProtoWorkshops = new Dictionary<uint, ProtoGroundWorkshop>();
        public SortedDictionary<string,uint> DisplayOrder = new SortedDictionary<string,uint>();

        public bool IsActive { get { return FlightGlobals.ActiveVessel != null && vessel.id == FlightGlobals.ActiveVessel.id; } }
        public string VesselName { get { return vessel.vesselName; } }
        public Guid VesselID { get { return vessel.id; } }
        public string CB { get { return FlightGlobals.Bodies[vessel.protoVessel.orbitSnapShot.ReferenceBodyIndex].bodyName; } }
        public bool Empty { get { return ProtoWorkshops.Count == 0; } }
        public bool IsLanded { get { return vessel.Landed; } }
        public string DisplayID { get { return vessel.vesselName+vessel.id; } }

        void add_protoworkshop(ProtoGroundWorkshop info)
        {
            ProtoWorkshops[info.id] = info;
            DisplayOrder[info.PartName] = info.id;
        }

        void add_workshop(GroundWorkshop workshop)
        {
            var info = new ProtoGroundWorkshop(workshop);
            Workshops[info.id] = workshop;
            add_protoworkshop(info);
        }

        void remove_workshop(GroundWorkshop workshop)
        {
            Workshops.Remove(workshop.part.flightID);
            ProtoGroundWorkshop info;
            if(ProtoWorkshops.TryGetValue(workshop.part.flightID, out info))
            {
                ProtoWorkshops.Remove(info.id);
                DisplayOrder.Remove(info.PartName);
            }
        }

        void update_and_checkin(Vessel vsl)
        {
            if(vsl == vessel && vessel != null)
            {
                if(vessel.loaded)
                {
                    Workshops.Clear();
                    ProtoWorkshops.Clear();
                    DisplayOrder.Clear();
                    foreach(var p in vessel.Parts)
                    {
                        var ws = p.Modules.GetModule<GroundWorkshop>();
                        if(ws != null && ws.isEnabled && ws.Efficiency > 0)
                            add_workshop(ws);
                    }
                }
                else
                {
                    foreach(var pp in vessel.protoVessel.protoPartSnapshots)
                    {
                        var pm = pp.FindModule(typeof(GroundWorkshop).Name);
                        if(pm == null) continue;
                        add_protoworkshop(new ProtoGroundWorkshop(vessel.id, vessel.vesselName, 
                                                                  pp.flightID, pp.partInfo.title, 
                                                                  pm.moduleValues));
                    }
                }
                if(!Empty)
                    GroundConstructionScenario.CheckinVessel(this);
                show_window &= ProtoWorkshops.Count > 1;
            }
        }

        protected override void OnStart()
        {
            base.OnStart();
            update_and_checkin(vessel);
            LockName = vessel.id.ToString();
        }

        protected override void OnAwake()
        {
            base.OnAwake();
            GameEvents.onGameStateSave.Add(onGameStateSave);
            GameEvents.onVesselCrewWasModified.Add(update_and_checkin);
            GameEvents.onVesselWasModified.Add(onVesselNullName);
            GameEvents.onVesselRename.Add(onVesselRename);
        }

        void OnDestroy()
        {
            GameEvents.onGameStateSave.Remove(onGameStateSave);
            GameEvents.onVesselCrewWasModified.Remove(update_and_checkin);
            GameEvents.onVesselWasModified.Remove(onVesselNullName);
            GameEvents.onVesselRename.Remove(onVesselRename);
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            update_and_checkin(vessel);
            node.AddValue("WindowPos", new Vector4(WindowPos.x, WindowPos.y, WindowPos.width, WindowPos.height));
            var workshops = new PersistentList<ProtoGroundWorkshop>(ProtoWorkshops.Values);
            workshops.Save(node.AddNode("Workshops"));
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            ProtoWorkshops.Clear();
            DisplayOrder.Clear();
            var wpos = node.GetValue("WindowPos");
            if(wpos != null)
            {
                var vpos = ConfigNode.ParseVector4(wpos);
                WindowPos = new Rect(vpos.x, vpos.y, vpos.z, vpos.w);
            }
            var wnode = node.GetNode("Workshops");
            if(wnode != null)
            {
                var workshops = new PersistentList<ProtoGroundWorkshop>();
                workshops.Load(wnode);
                workshops.ForEach(add_protoworkshop);
            }
        }

        #region events
        public override void OnLoadVessel()
        {
            update_and_checkin(vessel);
        }

        public override void OnUnloadVessel()
        {
            update_and_checkin(vessel);
        }

        void onGameStateSave(ConfigNode node)
        {
            update_and_checkin(vessel);
        }

        void onVesselRename(GameEvents.HostedFromToAction<Vessel,string> data)
        {
            update_and_checkin(data.host);
        }

        IEnumerator update_and_checkin_coroutine(Vessel vsl)
        {
            if(vsl == null || vsl != vessel) yield break;
            while(string.IsNullOrEmpty(vsl.vesselName)) yield return null;
            update_and_checkin(vsl);
        }

        void onVesselNullName(Vessel vsl)
        {
            if(isActiveAndEnabled)
                StartCoroutine(update_and_checkin_coroutine(vsl));
        }
        #endregion

        public void CheckinWorkshop(GroundWorkshop workshop)
        {
            if(workshop.vessel == null || workshop.part == null || workshop.vessel != vessel) return;
            add_workshop(workshop);
        }

        public void CheckoutWorkshop(GroundWorkshop workshop)
        {
            if(workshop.vessel == null || workshop.part == null || workshop.vessel != vessel) return;
            remove_workshop(workshop);
        }

        public bool CheckETA(double now)
        {
            var finished = false;
            foreach(var ws in ProtoWorkshops.Values)
                finished = ws.CheckETA(now) || finished;
            return finished;
        }

        #region UI
        public bool SwitchTo()
        {
            var vsl = FlightGlobals.FindVessel(vessel.id);
            if(vsl == null) 
            {
                Utils.Message("{0} was not found in the game", VesselName);
                return false;
            }
            if(HighLogic.LoadedSceneIsFlight) 
                FlightGlobals.SetActiveVessel(vsl);
            else
            {
                GamePersistence.SaveGame(HighLogic.CurrentGame.Updated(), "persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
                FlightDriver.StartAndFocusVessel("persistent", FlightGlobals.Vessels.IndexOf(vsl));
            }
            return true;
        }

        void focusCB()
        {
            var cb = FlightGlobals.Bodies.Find(body => body.bodyName == CB);
            if(cb != null) toMapView(cb.MapObject);
        }

        void focusVessel()
        {
            var vsl = FlightGlobals.FindVessel(vessel.id);
            if(vsl != null) toMapView(vsl.mapObject);
        }

        static void toMapView(MapObject target)
        {
            if(target == null) goto end;
            if(HighLogic.LoadedSceneIsFlight)
            { 
                if(!MapView.MapIsEnabled) 
                    MapView.EnterMapView(); 
            }
            if(HighLogic.LoadedSceneIsFlight ||
               HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                PlanetariumCamera.fetch.SetTarget(target);
            else goto end;
            return;
            end: Utils.Message("Go to Tracking Station to do this.");
        }

        public void Draw()
        {
            GUILayout.BeginVertical();
            if(IsActive) 
            {
                if(ProtoWorkshops.Count == 1)
                    GUILayout.Label(VesselName, Styles.white, GUILayout.ExpandWidth(true));
                else if(GUILayout.Button(new GUIContent(VesselName, "Press to open Workshop Manager"), 
                                         Styles.white, GUILayout.ExpandWidth(true)))
                    show_window = true;
            }
            else if(GUILayout.Button(new GUIContent(VesselName, "Press to focus on the Map"), 
                                     Styles.white, GUILayout.ExpandWidth(true)))
                focusVessel();
            foreach(var item in DisplayOrder) 
                ProtoWorkshops[item.Value].Draw();
            GUILayout.EndVertical();
        }

        void ManagerWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GroundWorkshop.KitInfo sync_kit = null;
            foreach(var item in DisplayOrder) 
            {
                GUILayout.BeginHorizontal();
                ProtoWorkshops[item.Value].Draw();
                var kit = Workshops[item.Value].KitUnderConstruction;
                if(!kit.Valid) 
                    GUILayout.Label(new GUIContent("⇶", "Construct this Kit using all workshops"), 
                                    Styles.grey, GUILayout.Width(25));
                else if(GUILayout.Button(new GUIContent("⇶", "Construct this Kit using all workshops"), 
                                         Styles.enabled_button, GUILayout.Width(25)))
                    sync_kit = kit;
                GUILayout.EndHorizontal();
            }
            if(sync_kit != null && sync_kit.Valid)
                Workshops.Values.ForEach(ws => ws.ConstructThisKit(sync_kit));
            GUILayout.EndVertical();
            GUIWindowBase.TooltipsAndDragWindow();
        }

        [KSPField(isPersistant = true)] public bool show_window;
        const float width = 550;
        const float height = 60;
        string LockName = ""; //inited OnStart
        Rect WindowPos = new Rect((Screen.width-width)/2, Screen.height/4, width, height*4);

        void OnGUI()
        {
            if(Time.timeSinceLevelLoad < 3) return;
            if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
            if(show_window && GUIWindowBase.HUD_enabled && vessel.isActiveVessel && ProtoWorkshops.Count > 1)
            {
                Styles.Init();
                Utils.LockIfMouseOver(LockName, WindowPos);
                WindowPos = GUILayout.Window(GetInstanceID(), 
                                             WindowPos, ManagerWindow, vessel.vesselName,
                                             GUILayout.Width(width),
                                             GUILayout.Height(height)).clampToScreen();
            }
            else
                Utils.LockIfMouseOver(LockName, WindowPos, false);
        }
        #endregion
    }
}

