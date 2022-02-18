//   AssemblySpace.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
    public class AssemblySpace : SerializableFiledsPartModule, IAssemblySpace, IAnimatedSpace,
        IContainerProducer, IConstructionSpace
    {
        [KSPField] public string Title = "Assembly Space";
        [KSPField] public string AnimatorID = string.Empty;
        [KSPField] public string DamperID = string.Empty;

        [KSPField(isPersistant = true)] public string KitPart = "DIYKit";

        [KSPField(isPersistant = true)] public VesselKit Kit = new VesselKit();

        [KSPField, SerializeField] public SpawnSpaceManager SpawnManager = new SpawnSpaceManager();
        VesselSpawner vessel_spawner;
        IAnimator animator;
        private ATMagneticDamper damper;
        bool can_construct_in_situ;

        public override void OnAwake()
        {
            base.OnAwake();
            vessel_spawner = gameObject.AddComponent<VesselSpawner>();
        }

        private void OnDestroy()
        {
            Destroy(vessel_spawner);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            vessel_spawner.Init(part);
            SpawnManager.Init(part);
            SpawnManager.SetupSensor();
            animator = part.GetAnimator(AnimatorID);
            damper = ATMagneticDamper.GetDamper(part, DamperID);
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

        public bool CheckKit(VesselKit vessel_kit, string part_name, out float kit2space_ratio)
        {
            kit2space_ratio = -1;
            if(!vessel_kit)
                return false;
            var kit_part = vessel_kit.CreatePart(part_name, part.flagURL, false);
            if(kit_part == null)
                return false;
            var kit_metric = new Metric(kit_part);
            var kit_module = kit_part.FindModuleImplementing<DeployableKitContainer>();
            var can_construct = kit_module != null && kit_module.CanConstruct(vessel_kit);
            DestroyImmediate(kit_part.gameObject);
            if(!can_construct)
                return false;
            kit2space_ratio = kit_metric.volume / SpawnManager.SpaceMetric.volume;
            return SpawnManager.MetricFits(kit_metric);
        }

        public void SetKit(VesselKit vessel_kit, string part_name)
        {
            if(vessel_kit != null)
            {
                Kit = vessel_kit;
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

        public bool Opened =>
            animator == null || animator.GetAnimatorState() != AnimatorState.Closed;

        public void SpawnKit() => StartCoroutine(spawn_kit());

        public void SpawnEmptyContainer(string part_name) =>
            StartCoroutine(spawn_empty_container(part_name));

        IEnumerator<YieldInstruction> spawn_kit()
        {
            if(!Kit)
                yield break;
            //this.Log("Spawning kit: {}\nReqs: {}", Kit, Kit.RemainingRequirements());//debug
            if(vessel_spawner.LaunchInProgress)
            {
                Utils.Message("In progress...");
                yield break;
            }
            if(!Kit.StageComplete(DIYKit.ASSEMBLY))
            {
                Utils.Message("The kit is not yet assembled");
                yield break;
            }
            if(Kit.StageStarted(DIYKit.CONSTRUCTION))
            {
                Utils.Message("Kit construction is already started");
                yield break;
            }
            if(Opened)
            {
                Utils.Message("Need to close assembly space first");
                Close();
                yield break;
            }
            yield return new WaitForEndOfFrame();
            yield return new WaitForFixedUpdate();
            var kit_ship = Kit.CreateShipConstruct(KitPart, part.flagURL);
            if(kit_ship == null)
                yield break;
            GroundConstructionScenario.SaveGame(Kit.Name + "-before_spawn");
            yield return StartCoroutine(spawn_kit_vessel(kit_ship));
        }

        IEnumerator<YieldInstruction> spawn_empty_container(string part_name)
        {
            if(vessel_spawner.LaunchInProgress)
            {
                Utils.Message("In progress...");
                yield break;
            }
            if(Opened)
            {
                Utils.Message("Need to close assembly space first");
                Close();
                yield break;
            }
            yield return new WaitForEndOfFrame();
            yield return new WaitForFixedUpdate();
            var kit_ship = new VesselKit().CreateShipConstruct(part_name, part.flagURL);
            if(kit_ship == null)
                yield break;
            var kit_metric =
                new Metric(kit_ship.Bounds(kit_ship.parts[0].localRoot.partTransform));
            if(!SpawnManager.MetricFits(kit_metric))
            {
                kit_ship.Unload();
                Utils.Message("Container is too big for this assembly space");
                yield break;
            }
            PartKit.GetRequirements(kit_ship.Parts[0],
                out var assembly_reqs,
                out var construction_reqs);
            var need_ec = assembly_reqs.energy + construction_reqs.energy;
            if(!part.TryUseResource(Utils.ElectricCharge.id, need_ec))
            {
                Utils.Message("Not enough energy to make the container");
                kit_ship.Unload();
                yield break;
            }
            if(assembly_reqs
               && !part.TryUseResource(assembly_reqs.resource.id,
                   assembly_reqs.resource_amount))
            {
                Utils.Message("Not enough {0} to make the container",
                    assembly_reqs.resource.name);
                kit_ship.Unload();
                yield break;
            }
            if(construction_reqs
               && !part.TryUseResource(construction_reqs.resource.id,
                   construction_reqs.resource_amount))
            {
                Utils.Message("Not enough {0} to make the container",
                    construction_reqs.resource.name);
                kit_ship.Unload();
                yield break;
            }
            GroundConstructionScenario.SaveGame(vessel.name + "-before_spawn_empty");
            yield return StartCoroutine(spawn_kit_vessel(kit_ship));
        }

        private void enable_damper()
        {
            if(damper == null)
                return;
            damper.EnableDamper(true);
            damper.AttractorEnabled = true;
            damper.InvertAttractor = false;
        }

        IEnumerator spawn_kit_vessel(ShipConstruct kit_ship)
        {
            enable_damper();
            //spawn the ship construct
            var bounds = kit_ship.Bounds(kit_ship.Parts[0].localRoot.transform);
            var spawn_transform = SpawnManager.GetSpawnTransform(bounds, out var offset);
            vessel_spawner.SpawnShipConstruct(kit_ship,
                spawn_transform,
                offset - bounds.center,
                Vector3.zero);
            yield return vessel_spawner.WaitForLaunch;
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
        public bool CanConstruct(VesselKit vessel_kit) =>
            (!vessel_kit.HasLaunchClamps
             && SpawnManager != null
             && SpawnManager.MetricFits(vessel_kit.ShipMetric));

        bool IConstructionSpace.Valid => isEnabled && can_construct_in_situ;
        public bool ConstructionComplete => Kit && Kit.Complete;

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
                Utils.Message("Something went wrong. Not all parts were properly constructed.");
                return;
            }
            StartCoroutine(launch_complete_construct());
        }

        private IEnumerator launch_complete_construct()
        {
            if(!HighLogic.LoadedSceneIsFlight)
                yield break;
            while(!FlightGlobals.ready)
                yield return null;
            vessel_spawner.BeginLaunch();
            yield return null;
            //save the game
            GroundConstructionScenario.SaveGame(Kit.Name + "-before_launch");
            yield return null;
            yield return new WaitForFixedUpdate();
            //load ship construct and launch it
            var construct = Kit.LoadConstruct();
            if(construct == null)
            {
                Utils.Log("Unable to load ShipConstruct {}. "
                          + "This usually means that some parts are missing "
                          + "or some modules failed to initialize.",
                    Kit.Name);
                Utils.Message("Something went wrong. Constructed ship cannot be launched.");
                vessel_spawner.AbortLaunch();
                yield break;
            }
            enable_damper();
            var bounds = new Metric((IShipconstruct)construct, world_space: true).bounds;
            var spawn_transform = SpawnManager.GetSpawnTransform(bounds, out var spawn_offset);
            vessel_spawner.SpawnShipConstruct(construct,
                spawn_transform,
                spawn_offset
                - bounds.center
                + construct.Parts[0].localRoot.transform.position,
                Vector3.zero,
                null,
                null,
                null,
                Kit.TransferCrewToKit);
            yield return vessel_spawner.WaitForLaunch;
            Kit = new VesselKit();
            Open();
        }
        #endregion
    }
}
