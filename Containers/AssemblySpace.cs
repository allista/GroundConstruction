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
    public class AssemblySpace : SerializableFiledsPartModule, IAssemblySpace, IAnimatedSpace, IContainerProducer
    {
        [KSPField] public string Title = "Assembly Space";
        [KSPField] public string AnimatorID = string.Empty;

        [KSPField(isPersistant = true)] 
        public string KitPart = "DIYKit";

        [KSPField(isPersistant = true)] 
        public VesselKit Kit = new VesselKit();

        [KSPField, SerializeField]
        public SpawnSpaceManager SpawnManager = new SpawnSpaceManager();
        VesselSpawner Spawner;
        MultiAnimator Animator;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Spawner = new VesselSpawner(part);
            SpawnManager.Init(part);
            SpawnManager.SetupSensor();
            if(!string.IsNullOrEmpty(AnimatorID))
                Animator = part.GetAnimator(AnimatorID);
            if(Animator != null)
                StartCoroutine(Utils.SlowUpdate(spawn_space_keeper));
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            Kit.Host = this;
        }

        void spawn_space_keeper()
        {
            if(Animator != null && !SpawnManager.SpawnSpaceEmpty)
                Animator.Open();
        }

        #region IAssemblySpace
        public string Name => Title;
        public bool Empty => !Kit && SpawnManager.SpawnSpaceEmpty;
        public bool Valid => isEnabled;
        public VesselKit GetKit(Guid id) => Kit.id == id ? Kit : null;
        public List<VesselKit> GetKits() => new List<VesselKit> { Kit };

        public float KitToSpaceRatio(VesselKit kit, string part_name)
        {
            if(!kit) return -1;
            var kit_part = kit.CreatePart(part_name, part.flagURL, false);
            if(kit_part == null) return -1;
            var kit_metric = new Metric(kit_part);
            DestroyImmediate(kit_part.gameObject);
            if(!SpawnManager.MetricFits(kit_metric)) return -1;
            return kit_metric.volume / SpawnManager.SpaceMetric.volume;
        }

        public void SetKit(VesselKit kit, string part_name)
        {
            Kit = kit;
            KitPart = part_name;
            Kit.Host = this;
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

        public bool Opened => Animator == null || Animator.State != AnimatorState.Closed;

        public bool SpawnAutomatically => false;

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
            if(Opened)
            {
                Utils.Message("Need to close assembly space first");
                Close();
                return;
            }
            var kit_ship = Kit.CreateShipConstruct(KitPart, part.flagURL);
            if(kit_ship != null)
            {
                Utils.SaveGame(Kit.Name+"-before_spawn");
                StartCoroutine(spawn_kit_vessel(kit_ship));
            }
        }

        public void SpawnEmptyContainer(string part_name)
        {
            if(Opened)
            {
                Utils.Message("Need to close assembly space first");
                Close();
                return;
            }
            var kit_ship = new VesselKit().CreateShipConstruct(part_name, part.flagURL);
            if(kit_ship != null)
            {
                var kit_metric = new Metric(kit_ship.Bounds(kit_ship.parts[0].localRoot.partTransform));
                if(!SpawnManager.MetricFits(kit_metric))
                {
                    Utils.Message("Container is too big for this assembly space");
                    return;
                }
                Utils.SaveGame(vessel.name+"-before_spawn_empty");
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
