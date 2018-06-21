//   AssemblySpace.cs
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
    public class AssemblySpace : SerializableFiledsPartModule, IAssemblySpace, IAnimatedSpace
    {
        [KSPField] public string Title = "Assembly Space";
        [KSPField] public string KitPart = "DIYKit";
        [KSPField] public string AnimatorID = string.Empty;

        [KSPField, SerializeField]
        public SpawnSpaceManager SpawnManager = new SpawnSpaceManager();
        VesselSpawner Spawner;
        MultiAnimator Animator;

        [KSPField(isPersistant = true)] public VesselKit Kit = new VesselKit();

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Spawner = new VesselSpawner(part);
            SpawnManager.Init(part);
            SpawnManager.SetupSensor();
            if(!string.IsNullOrEmpty(AnimatorID))
                Animator = part.GetAnimator(AnimatorID);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            Kit.Host = this;
        }

        #region IAssemblySpace
        public string Name => Title;

        public bool Empty => !Kit && SpawnManager.SpawnSpaceEmpty;

        public VesselKit GetKit(Guid id) => Kit.id == id ? Kit : null;

        public List<VesselKit> GetKits() => new List<VesselKit> { Kit };

        public float KitToSpaceRatio(VesselKit kit)
        {
            if(!kit) return -1;
            var kit_part = kit.CreatePart(KitPart, part.flagURL, false);
            if(kit_part == null) return -1;
            var kit_metric = new Metric(kit_part);
            DestroyImmediate(kit_part.gameObject);
            if(!SpawnManager.MetricFits(kit_metric)) return -1;
            return 1 - kit_metric.volume / SpawnManager.SpaceMetric.volume;
        }

        public void SetKit(VesselKit kit)
        {
            Kit = kit;
            Close();
        }

        public void Open() 
        {
            if(Animator != null)
                Animator.Open();
        }

        public void Close()
        {
            if(Animator != null)
                Animator.Close();
        }

        public bool Opened => Animator == null || Animator.State == AnimatorState.Opened;

        public void SpawnKit()
        {
            if(!Kit) return;
            //this.Log("Spawning kit: {}\nReqs: {}", Kit, Kit.RemainingRequirements());//debug
            if(Spawner.LaunchInProgress)
            {
                Utils.Message("In progress...");
                return;
            }
            if(!Kit.StageComplete(DIYKit.ASSEMBLY))
            {
                Utils.Message("The kit is not yet assembled");
                return;
            }
            var kit_ship = Kit.CreateShipConstruct(KitPart, part.flagURL);
            if(kit_ship != null)
            {
                Utils.SaveGame(Kit.Name+"-before_spawn");
                StartCoroutine(spawn_kit_vessel(kit_ship));
            }
        }
      
        IEnumerator<YieldInstruction> spawn_kit_vessel(ShipConstruct kit_ship)
        {
            //spawn the ship construct
            var bounds = kit_ship.Bounds(kit_ship.Parts[0].localRoot.transform);
            yield return
                StartCoroutine(Spawner
                               .SpawnShipConstruct(kit_ship,
                                                   SpawnManager.GetSpawnTransform(bounds),
                                                   SpawnManager.GetSpawnOffset(bounds) - bounds.center,
                                                   Vector3.zero));
            Kit = new VesselKit();
            Open();
        }

        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) =>
        Kit.Valid ? Kit.Cost : 0;

        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.CONSTANTLY;

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) =>
        Kit.Valid ? Kit.Mass : 0;

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;
        #endregion
    }
}
