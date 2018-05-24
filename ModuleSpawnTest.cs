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
            var offset = Vector3.Scale(SpawnOffset, construct.Bounds(construct.Parts[0].localRoot.transform).extents);
            StartCoroutine(Spawner.SpawnShipConstruct(construct, spawn_transform, offset, Vector3.zero));
        }

        [KSPEvent(guiName = "Select Vessel", guiActive = true, active = true)]
        public void SelectVessel()
        {
            construct_loader.SelectVessel();
        }

        [KSPEvent(guiName = "Select Subassembly", guiActive = true, active = true)]
        public void SelectSubassembly()
        {
            construct_loader.SelectSubassembly();
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
