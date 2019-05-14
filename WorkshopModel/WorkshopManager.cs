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
        public Dictionary<uint, WorkshopBase> Workshops = new Dictionary<uint, WorkshopBase>();
        public Dictionary<uint, ProtoWorkshop> ProtoWorkshops = new Dictionary<uint, ProtoWorkshop>();
        public SortedDictionary<string,uint> DisplayOrder = new SortedDictionary<string,uint>();

        public bool IsActive => FlightGlobals.ActiveVessel != null && vessel.id == FlightGlobals.ActiveVessel.id;
        public string VesselName => vessel.vesselName;
        public Guid VesselID => vessel.id;
        public string CB => FlightGlobals.Bodies[vessel.protoVessel.orbitSnapShot.ReferenceBodyIndex].bodyName;
        public bool Empty => ProtoWorkshops.Count == 0;
        public bool IsLanded => vessel.Landed;
        public string DisplayID => vessel.vesselName + vessel.id;

        public WorkshopType workshopTypes 
        { 
            get
            {
                WorkshopType types = WorkshopType.NONE;
                ProtoWorkshops.Values.ForEach(pw => types = types | pw.workshopType);
                return types;
            }
        }

        public bool isOperable
        {
            get
            {
                var status = string.Empty;
                return WorkshopBase.IsOperable(Vessel, workshopTypes, ref status);
            }
        }

        void add_protoworkshop(ProtoWorkshop info)
        {
            ProtoWorkshops[info.id] = info;
            DisplayOrder[info.PartName] = info.id;
        }

        void add_workshop(WorkshopBase workshop)
        {
            var info = new ProtoWorkshop(workshop);
            Workshops[info.id] = workshop;
            add_protoworkshop(info);
            workshop.Manager = this;
        }

        void remove_workshop(PartModule workshop)
        {
            Workshops.Remove(workshop.part.flightID);
            ProtoWorkshop info;
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
                        var ws = p.Modules.GetModule<WorkshopBase>();
                        if(ws != null && ws.isEnabled)
                            add_workshop(ws);
                    }
                }
                else
                {
                    foreach(var pp in vessel.protoVessel.protoPartSnapshots)
                    {
                        var pm = pp.FindModule(typeof(WorkshopBase).Name);
                        if(pm == null) continue;
                        add_protoworkshop(new ProtoWorkshop(vessel.id, vessel.vesselName,
                                                            pp.flightID, pp.partInfo.title,
                                                            pm.moduleValues));
                    }
                }
                if(!Empty)
                    GroundConstructionScenario.CheckinVessel(this);
//                this.Log("update_and_checkin.ProtoWorkshops: {}", ProtoWorkshops);//debug
            }
        }

        protected override void OnStart()
        {
            base.OnStart();
            update_and_checkin(vessel);
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
            var workshops = new PersistentList<ProtoWorkshop>(ProtoWorkshops.Values);
            workshops.Save(node.AddNode("Workshops"));
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            ProtoWorkshops.Clear();
            DisplayOrder.Clear();
            var wnode = node.GetNode("Workshops");
            if(wnode != null)
            {
                var workshops = new PersistentList<ProtoWorkshop>();
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

        public void CheckinWorkshop(WorkshopBase workshop)
        {
            if(workshop.vessel == null || workshop.part == null || workshop.vessel != vessel) return;
//            this.Log("Checked In:  {}:{}", workshop, workshop.part.flightID);//debug
            add_workshop(workshop);
        }

        public void CheckoutWorkshop(WorkshopBase workshop)
        {
            if(workshop.vessel == null || workshop.part == null || workshop.vessel != vessel) return;
//            this.Log("Checked Out: {}:{}", workshop, workshop.part.flightID);//debug
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
            if(HighLogic.LoadedSceneIsFlight)
                FlightGlobals.SetActiveVessel(vessel);
            else
            {
                GamePersistence.SaveGame(HighLogic.CurrentGame.Updated(), "persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
                FlightDriver.StartAndFocusVessel("persistent", FlightGlobals.Vessels.IndexOf(vessel));
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
            if(IsActive) GUILayout.Label(VesselName, Styles.enabled, GUILayout.ExpandWidth(true));
            else if(GUILayout.Button(new GUIContent(VesselName, "Press to focus on Map"),
                                     Styles.white, GUILayout.ExpandWidth(true)))
                focusVessel();
            IWorkshopTask sync_task = null;
            foreach(var item in DisplayOrder)
            {
                var pw = ProtoWorkshops[item.Value];
                if(!pw.isOperable) continue;
                GUILayout.BeginHorizontal();
                pw.Draw();
                if(IsActive && ProtoWorkshops.Count > 1)
                {
                    var task = Workshops[item.Value].GetCurrentTask();
                    if(!task.Valid)
                        GUILayout.Label(new GUIContent("⇶", "Workshop is idle"),
                                        Styles.inactive, GUILayout.Width(25));
                    else if(GUILayout.Button(new GUIContent("⇶", "Construct this Kit using all workshops"),
                                             Styles.enabled_button, GUILayout.Width(25)))
                        sync_task = task;
                }
                GUILayout.EndHorizontal();
            }
            if(sync_task != null && sync_task.Valid)
                Workshops.Values.ForEach(ws => ws.StartTask(sync_task));
            GUILayout.EndVertical();
        }
        #endregion

        public override string ToString()
        {
            return Utils.Format("{}, VesselID={}, DisplayID={}, IsActive={}, CB={}, Empty={}, IsLanded={}",
                                VesselName, VesselID, DisplayID, IsActive, CB, Empty, IsLanded);
        }
    }
}

