//   ModuleSpawnTest.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri
using System;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
    public class ModuleSpawnTest : PartModule
    {
        [KSPField] public string SpawnTransform = string.Empty;
        [KSPField] public Vector3 SpawnOffset = Vector3.zero;

        Transform spawn_transform;
        VesselSpawner Spawner;
        ShipConstructLoader construct_loader;

        public override void OnAwake()
        {
            base.OnAwake();
            construct_loader = gameObject.AddComponent<ShipConstructLoader>();
            construct_loader.process_construct = process_construct;
            construct_loader.Show(false);
        }

        protected void OnDestroy()
        {
            Destroy(construct_loader);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Spawner = new VesselSpawner(part);
            if(!string.IsNullOrEmpty(SpawnTransform))
                spawn_transform = part.FindModelTransform(SpawnTransform);
        }

        void process_construct(ShipConstruct construct)
        {
            var bounds = construct.Bounds(construct.Parts[0].localRoot.transform);
            var offset = Vector3.Scale(SpawnOffset, bounds.extents) - bounds.center;
            StartCoroutine(Spawner.SpawnShipConstruct(construct, spawn_transform, offset, Vector3.up));
        }

        //called every frame while part collider is touching the trigger
        bool active;
        void OnTriggerStay(Collider col)
        {
            if(active && col != null && col.attachedRigidbody != null)
            {
                if(col.CompareTag("Untagged"))
                {
                    var p = col.attachedRigidbody.GetComponent<Part>();
                    if(p != null && p.vessel != null && p.vessel != vessel)
                        store_vessel(p.vessel);
                }
            }
        }

        Metric pv_metric;
        ProtoVessel proto_vessel;
        void store_vessel(Vessel vsl)
        {
            if(proto_vessel == null)
            {
                active = false;
                pv_metric = new Metric(vsl);
                proto_vessel = vsl.BackupVessel();
                if(FlightGlobals.ActiveVessel == vsl)
                {
                    FlightCameraOverride.AnchorForSeconds(FlightCameraOverride.Mode.Hold, vessel.transform, 1);
                    FlightGlobals.ForceSetActiveVessel(vessel);
                    FlightInputHandler.SetNeutralControls();
                }
                Events["ActivateVesselCapture"].guiName = "Eat a vessel";
                vsl.Die();
            }
        }

        [KSPEvent(guiName = "Select Vessel", guiActive = true, active = true)]
        public void SelectVessel()
        {
            if(!Spawner.LaunchInProgress)
                construct_loader.SelectVessel();
        }

        [KSPEvent(guiName = "Select Subassembly", guiActive = true, active = true)]
        public void SelectSubassembly()
        {
            if(!Spawner.LaunchInProgress)
                construct_loader.SelectSubassembly();
        }

        [KSPEvent(guiName = "Eat a vessel", guiActive = true, active = true)]
        public void ActivateVesselCapture()
        {
            active = true;
            Events["ActivateVesselCapture"].guiName = "Eating a vessel...";
        }

        [KSPEvent(guiName = "Spawn ProtoVessel", guiActive = true, active = true)]
        public void SpawnProtoVessel()
        {
            if(proto_vessel != null && !Spawner.LaunchInProgress)
            {
                var offset = Vector3.Scale(SpawnOffset, pv_metric.extents)-pv_metric.center;
                StartCoroutine(Spawner
                               .SpawnProtoVessel(proto_vessel, spawn_transform, offset, Vector3.up,
                                                 on_vessel_launched: vsl => proto_vessel = null));
            }
        }

        void OnGUI()
        {
            if(Event.current.type != EventType.Layout &&
               Event.current.type != EventType.Repaint) return;
            Styles.Init();
            construct_loader.Draw();
        }

        void OnRenderObject()
        {
            if(vessel == null) return;
            var T = spawn_transform;
            if(T != null)
            {
                Utils.GLVec(T.position, T.up, Color.green);
                Utils.GLVec(T.position, T.forward, Color.blue);
                Utils.GLVec(T.position, T.right, Color.red);
            }
        }
    }
}
