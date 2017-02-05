//   GroundConstructionScenario.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
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

        static SortedDictionary<Guid,WorkshopVesselInfo> Workshops = new SortedDictionary<Guid,WorkshopVesselInfo>();
        static SortedDictionary<string,Guid> DisplayOrder = new SortedDictionary<string,Guid>();
		double now = -1;

        static void add_workshop(WorkshopInfo info)
        {
            WorkshopVesselInfo ws;
            if(Workshops.TryGetValue(info.vesselID, out ws))
                ws.Checkin(info);
            else
            {
                ws = new WorkshopVesselInfo(info);
                Workshops[info.vesselID] = ws;
                DisplayOrder[ws.DisplayID] = ws.vesselID;
            }
        }

        static bool remove_workshop(WorkshopInfo info)
        {
            WorkshopVesselInfo ws;
            if(Workshops.TryGetValue(info.vesselID, out ws))
            {
                ws.Remove(info.id);
                if(ws.Empty)
                {
                    Workshops.Remove(ws.vesselID);
                    DisplayOrder.Remove(ws.DisplayID);
                }
                return true;
            }
            return false;
        }

        static void remove_vessel(WorkshopVesselInfo info)
        {
            Workshops.Remove(info.vesselID);
            DisplayOrder.Remove(info.DisplayID);
        }

		public static void CheckinWorkshop(GroundWorkshop workshop)
		{
			if(workshop.part == null || workshop.vessel == null) return;
            add_workshop(new WorkshopInfo(workshop));
		}

		public static bool DeregisterWorkshop(GroundWorkshop workshop)
		{ 
            if(workshop.part == null || workshop.vessel == null) return false;
            return remove_workshop(new WorkshopInfo(workshop));
        }

		static bool reckeck_workshops()
		{
			if(FlightGlobals.Vessels != null && FlightGlobals.Vessels.Count > 0)
			{
                var del = new List<WorkshopVesselInfo>();
				foreach(var workshop in Workshops)
                { 
                    if(!workshop.Value.Recheck()) 
                        del.Add(workshop.Value); 
                }
                del.ForEach(remove_vessel);
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

		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			var workshops = new PersistentList<WorkshopInfo>();
            foreach(var ws in Workshops.Values) workshops.AddRange(ws.Parts);
			workshops.Save(node.AddNode("Workshops"));
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			Workshops.Clear();
            DisplayOrder.Clear();
			var wnode = node.GetNode("Workshops");
			if(wnode != null)
			{
				var workshops = new PersistentList<WorkshopInfo>();
				workshops.Load(wnode);
                workshops.ForEach(add_workshop);
			}
		}

		void Update()
		{
			if(switchto != null) 
			{
                if(!switchto.SwitchTo()) 
                    remove_vessel(switchto);
				switchto = null;
			}
		}

		#region GUI
		const float width = 500;
		const float height = 120;

		static bool show_window;
		public static void ShowWindow(bool show) { show_window = show; }
		public static void ToggleWindow() { show_window = !show_window; }

        WorkshopVesselInfo switchto = null;

		Vector2 workshops_scroll = Vector2.zero;
		Rect WindowPos = new Rect(Screen.width-width-100, 0, Screen.width/4, Screen.height/4);
		void main_window(int WindowID)
		{
			GUILayout.BeginVertical(Styles.white);
			if(Workshops.Count > 0)
			{
				workshops_scroll = GUILayout.BeginScrollView(workshops_scroll, GUILayout.Height(height), GUILayout.Width(width));
                foreach(var item in DisplayOrder.Values) 
				{
					var info = Workshops[item];
					GUILayout.BeginHorizontal();
					info.Draw();
                    if(info.IsActive)
                        GUILayout.Label(new GUIContent("▶▶", "This is the active vessel"), 
                                        Styles.grey, GUILayout.ExpandWidth(false));
                    else if(GUILayout.Button(new GUIContent("▶▶", "Switch to this workshop"), 
                                             Styles.enabled_button, GUILayout.ExpandWidth(false)))
						switchto = info;
					GUILayout.EndHorizontal();
				}
				GUILayout.EndScrollView();
				if(GUILayout.Button("Close", Styles.close_button, GUILayout.ExpandWidth(true)))
					show_window = false;
			}
			else GUILayout.Label("No Ground Workshops", Styles.white, GUILayout.ExpandWidth(true));
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

    public class NamedVesselInfo : VesselInfo
    {
        [Persistent] public string CB;
        [Persistent] public string VesselName;
    }

    public class WorkshopVesselInfo : NamedVesselInfo
    {
        SortedDictionary<uint,WorkshopInfo> WorkshopParts = new SortedDictionary<uint, WorkshopInfo>();
        public IEnumerable<WorkshopInfo> Parts { get { return WorkshopParts.Values; } }
        public bool Empty { get { return WorkshopParts.Count == 0; } }

        public string DisplayID { get; private set; }

        public WorkshopVesselInfo(WorkshopInfo workshop)
        {
            vesselID = workshop.vesselID;
            CB = workshop.CB;
            VesselName = workshop.VesselName;
            WorkshopParts[workshop.id] = workshop;
            DisplayID = CB+VesselName+vesselID;
        }

        public WorkshopVesselInfo(GroundWorkshop workshop)
            : this(new WorkshopInfo(workshop)) {}

        public bool Contains(WorkshopInfo info)
        { return WorkshopParts.ContainsKey(info.id); }

        public bool Checkin(WorkshopInfo info)
        {
            if(info.vesselID == vesselID)
            {
                WorkshopParts[info.id] = info;
                return true;
            }
            return false;
        }

        public bool Remove(uint workshopID)
        { return WorkshopParts.Remove(workshopID); }

        public bool CheckETA(double now)
        {
            var finished = false;
            foreach(var ws in WorkshopParts.Values)
                finished = ws.CheckETA(now) || finished;
            return finished;
        }

        public bool Recheck()
        { 
            var vsl = GetVessel();
            if(vsl == null) return false;
            if(vsl.loaded)
            {
                var del = new List<uint>();
                foreach(var ws in WorkshopParts)
                {
                    var workshop = ws.Value.GetWorkshop(vsl);
                    if(workshop != null && workshop.isEnabled && workshop.Efficiency > 0) continue;
                    del.Add(ws.Key);
                }
                del.ForEach(pid => WorkshopParts.Remove(pid));
                return WorkshopParts.Count > 0;
            }
            return true;
        }

        public bool SwitchTo()
        {
            var vsl = FlightGlobals.FindVessel(vesselID);
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
            var vsl = FlightGlobals.FindVessel(vesselID);
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
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(new GUIContent(CB, "Press to focus on the Map"), 
                                Styles.yellow, GUILayout.Width(60)))
                focusCB();
            GUILayout.BeginVertical();
            if(GUILayout.Button(new GUIContent(VesselName, "Press to focus on the Map"), 
                                Styles.white, GUILayout.ExpandWidth(true)))
                focusVessel();
            foreach(var ws in WorkshopParts) ws.Value.Draw();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }


    public class WorkshopInfo : NamedVesselInfo
    {
        public enum Status { IDLE, ACTIVE, COMPLETE };

        [Persistent] public uint   id;
        [Persistent] public string PartName;
        [Persistent] public string KitName;
        [Persistent] public Status State;
        [Persistent] public double EndUT;
        [Persistent] public string ETA;

        public WorkshopInfo() {}
        public WorkshopInfo(GroundWorkshop workshop) 
        {
            vesselID = workshop.vessel.id;
            id = workshop.part.flightID;
            CB = workshop.vessel.mainBody.bodyName;
            VesselName = workshop.vessel.vesselName;
            PartName = workshop.part.partInfo.title;
            State = Status.IDLE;
            EndUT = -1;
            if(workshop.KitUnderConstruction.Valid) 
            {
                State = Status.ACTIVE;
                KitName = workshop.KitUnderConstruction.KitName;
                if(workshop.ETA > 0 && workshop.LastUpdateTime > 0) 
                    EndUT = workshop.LastUpdateTime+workshop.ETA;
                if(EndUT > 0 && EndUT < Planetarium.GetUniversalTime())
                    State = Status.COMPLETE;
            }
        }

        //deprecated config conversion
        public override void Load(ConfigNode node)
        {
            base.Load(node);
            if(string.IsNullOrEmpty(VesselName) && node.HasValue("Name"))
                VesselName = node.GetValue("Name");
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
            if(!IsActive) return;
            var workshop = GetWorkshop();
            if(workshop != null) 
                workshop.ToggleConstructionWindow();
        }

        public bool CheckETA(double now)
        {
            if(State != WorkshopInfo.Status.ACTIVE) return false;
            if(EndUT > 0 && EndUT < now)
            {
                Utils.Message(10, "Engineers at '{0}' should have assembled the '{1}' by now.",
                              VesselName, KitName);
                State = WorkshopInfo.Status.COMPLETE;
                EndUT = -1;
                return true;
            }
            ETA = EndUT > 0?
                "Time left: "+KSPUtil.PrintTimeCompact(EndUT-now, false) : "Stalled...";
            return false;
        }

        public override string ToString()
        { 
            if(State == Status.COMPLETE)
                return string.Format("[{0}] \"{1}\" assembled \"{2}\".", CB, VesselName, KitName);
            if(State == Status.ACTIVE)
                return string.Format("[{0}] \"{1}\" is building \"{2}\". {3}", CB, VesselName, KitName, ETA);
            return string.Format("[{0}] \"{1}\" is idle.", CB, VesselName);
        }

        public void Draw()
        {
            var tooltip = PartName;
            var style = Styles.white;
            GUIContent status = null;
            if(IsActive)
                tooltip += "\nPress to open Construction Window";
            if(State == Status.COMPLETE)
            {
                style = Styles.green;
                status = new GUIContent(KitName+": Complete", tooltip);
            }
            else if(State == Status.ACTIVE)
            {
                style = EndUT > 0? Styles.yellow : Styles.red;
                status = new GUIContent(KitName+": "+ETA, tooltip);
            }
            if(status == null) return;
            if(GUILayout.Button(status, style, GUILayout.ExpandWidth(true)))
                ToggleConstructionWindow();
        }
    }
}

