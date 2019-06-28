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
    public class AssemblySpace : SerializableFiledsPartModule, IAssemblySpace, IAnimatedSpace, IContainerProducer, IConstructionSpace
    {
        [KSPField] public string Title = "Assembly Space";
        [KSPField] public string AnimatorID = string.Empty;

        [KSPField(isPersistant = true)]
        public string KitPart = "DIYKit";

        [KSPField(isPersistant = true)]
        public VesselKit Kit = new VesselKit();

        [KSPField, SerializeField]
        public SpawnSpaceManager SpawnManager = new SpawnSpaceManager();
        VesselSpawner vessel_spawner;
        MultiAnimator animator;
        bool can_construct_in_situ;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            vessel_spawner = new VesselSpawner(part);
            SpawnManager.Init(part);
            SpawnManager.SetupSensor();
            if(!string.IsNullOrEmpty(AnimatorID))
                animator = part.GetAnimator(AnimatorID);
            if(animator != null)
                StartCoroutine(Utils.SlowUpdate(spawn_space_keeper));
            if(Kit && !Kit.Empty)
                can_construct_in_situ = CanConstruct(Kit);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            Kit.Host = this;
        }

        void spawn_space_keeper()
        {
            if(animator != null && !SpawnManager.SpawnSpaceEmpty)
                animator.Open();
        }

        #region IAssemblySpace
        public string Name => Title;
        public bool Empty => !Kit && SpawnManager.SpawnSpaceEmpty;
        public bool Valid => isEnabled;
        public VesselKit GetKit(Guid id) => Kit.id == id ? Kit : null;
        public List<VesselKit> GetKits() => new List<VesselKit> { Kit };

        public bool CheckKit(VesselKit kit, string part_name, out float kit2space_ratio)
        {
            kit2space_ratio = -1;
            if(!kit) return false;
            var kit_part = kit.CreatePart(part_name, part.flagURL, false);
            if(kit_part == null) return false;
            var kit_metric = new Metric(kit_part);
            var kit_module = kit_part.FindModuleImplementing<DeployableKitContainer>();
            var can_construct = kit_module != null && kit_module.CanConstruct(kit);
            DestroyImmediate(kit_part.gameObject);
            if(!can_construct) return false;
            kit2space_ratio = kit_metric.volume / SpawnManager.SpaceMetric.volume;
            return SpawnManager.MetricFits(kit_metric);
        }

        public void SetKit(VesselKit kit, string part_name)
        {
            if(kit != null)
            {
                Kit = kit;
                KitPart = part_name;
                Kit.Host = this;
                can_construct_in_situ = CanConstruct(Kit);
                Close();
            }
            else
                Kit = new VesselKit();
        }

        public void Open()
        {
            if(animator != null)
                animator.Open();
        }

        public void Close()
        {
            if(animator != null)
                animator.Close();
        }

        public bool Opened => animator == null || animator.State != AnimatorState.Closed;

        public bool SpawnAutomatically => false;

        public void SpawnKit()
        {
            if(!Kit) return;
            //this.Log("Spawning kit: {}\nReqs: {}", Kit, Kit.RemainingRequirements());//debug
            if(vessel_spawner.LaunchInProgress)
            {
                Utils.Message("In progress...");
                return;
            }
            if(!Kit.StageComplete(DIYKit.ASSEMBLY))
            {
                Utils.Message("The kit is not yet assembled");
                return;
            }
            if(Kit.StageStarted(DIYKit.CONSTRUCTION))
            {
                Utils.Message("Kit construction is already started");
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
                Utils.SaveGame(Kit.Name + "-before_spawn");
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
                Utils.SaveGame(vessel.name + "-before_spawn_empty");
                StartCoroutine(spawn_kit_vessel(kit_ship));
            }
        }

        IEnumerator<YieldInstruction> spawn_kit_vessel(ShipConstruct kit_ship)
        {
            //spawn the ship construct
            var bounds = kit_ship.Bounds(kit_ship.Parts[0].localRoot.transform);
            var spawn_transform = SpawnManager.GetSpawnTransform(bounds, out var offset);
            yield return
                StartCoroutine(vessel_spawner
                               .SpawnShipConstruct(kit_ship,
                                                   spawn_transform,
                                                   offset - bounds.center,
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

        #region IConstructionSpace
        public bool CanConstruct(VesselKit kit) =>
        (!kit.HasLaunchClamps
         && SpawnManager != null
         && SpawnManager.MetricFits(kit.ShipMetric));

        bool IConstructionSpace.Valid => isEnabled && can_construct_in_situ;
        
        public void Launch()
        {
            if(vessel_spawner == null || vessel_spawner.LaunchInProgress)
                return;
            if(Empty)
            {
                Utils.Message("Nothing to launch: container is empty.");
                return;
            }
            if(vessel.packed)
            {
                Utils.Message("Cannot launch from a packed construction kit.");
                return;
            }
            if(!Kit.Complete)
            {
                Utils.Message("Construction is not complete yet.");
                return;
            }
            if(!Kit.BlueprintComplete())
            {
                Utils.Message("Something whent wrong. Not all parts were properly constructed.");
                return;
            }
            StartCoroutine(launch_complete_construct());
        }

        IEnumerator<YieldInstruction> launch_complete_construct()
        {
            if(!HighLogic.LoadedSceneIsFlight) yield break;
            while(!FlightGlobals.ready) yield return null;
            vessel_spawner.BeginLaunch();
            yield return null;
            //save the game
            Utils.SaveGame(Kit.Name + "-before_launch");
            yield return null;
            //load ship construct and launch it
            var construct = Kit.LoadConstruct();
            if(construct == null)
            {
                Utils.Log("Unable to load ShipConstruct {}. " +
                          "This usually means that some parts are missing " +
                          "or some modules failed to initialize.", Kit.Name);
                Utils.Message("Something whent wrong. Constructed ship cannot be launched.");
                vessel_spawner.AbortLaunch();
                yield break;
            }
            var bounds = new Metric(construct, world_space: true).bounds;
            var spawn_transform = SpawnManager.GetSpawnTransform(bounds, out var offset);
            yield return
                StartCoroutine(vessel_spawner
                               .SpawnShipConstruct(construct,
                                                   spawn_transform,
                                                   offset
                                                   - bounds.center
                                                   + construct.Parts[0].localRoot.transform.position,
                                                   Vector3.zero,
                                                   null,
                                                   null,
                                                   null,
                                                   Kit.TransferCrewToKit));
            Kit = new VesselKit();
            Open();
        }
        #endregion
    }
}
