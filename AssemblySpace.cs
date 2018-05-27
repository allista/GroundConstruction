//   AssemblySpace.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri
using System;
using System.Collections.Generic;
using AT_Utils;
using UnityEngine;

namespace GroundConstruction
{
    public class AssemblySpace : SerializableFiledsPartModule, IAssemblySpace
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
            if(!string.IsNullOrEmpty(AnimatorID))
                Animator = part.GetAnimator(AnimatorID);
            if(SpawnManager.Space != null)
            {
                var space_collider = SpawnManager.Space.GetComponent<Collider>();
                if(space_collider == null || !space_collider.isTrigger)
                {
                    this.Log("Adding a Sensor collider to the SpawnSpaceMesh: {}", SpawnManager.Space);
                    var collider = SpawnManager.Space.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = SpawnManager.Space.sharedMesh;
                    collider.convex = true;
					collider.isTrigger = true;
                    space_collider = collider;
                }
                space_collider.enabled = true;
            }
        }

        #region IAssemblySpace
        public string Name => Title;

        public bool Empty => !Kit && SpawnSpaceReady;

        public void EnableControls(bool enable = true) { }

        public VesselKit GetKit(Guid id) => Kit.id == id ? Kit : null;

        public List<VesselKit> GetKits() => new List<VesselKit> { Kit };

        public float KitToSpaceRatio(VesselKit kit)
        {
            if(!kit) return -1;
            var kit_part = create_kit_part(kit);
            if(kit_part == null) return -1;
            var kit_metric = new Metric(kit_part);
            DestroyImmediate(kit_part.gameObject);
            if(!SpawnManager.MetricFits(kit_metric)) return -1;
            return 1 - kit_metric.volume / SpawnManager.SpaceMetric.volume;
        }

        public void SetKit(VesselKit kit)
        {
            this.Kit = kit;
            if(Animator != null)
                Animator.Close();
        }

        public void ShowUI(bool enable = true) { }

        public void SpawnKit()
        {
            if(!Kit) return;
            this.Log("Spawning kit: {}\nReqs: {}", Kit, Kit.RemainingRequirements());//debug
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
            var kit_part = create_kit_part(Kit);
            if(kit_part != null)
                StartCoroutine(spawn_kit_vessel(kit_part));
        }

        Part create_kit_part(VesselKit kit)
        {
            var part_info = PartLoader.getPartInfoByName(KitPart);
            if(part_info == null)
            {
                Utils.Message("No such part: {0}", KitPart);
                return null;
            }
            var kit_part = Instantiate(part_info.partPrefab);
            kit_part.gameObject.SetActive(true);
            kit_part.partInfo = part_info;
            kit_part.name = part_info.name;
            kit_part.flagURL = part.flagURL;
            kit_part.persistentId = FlightGlobals.GetUniquepersistentId();
            FlightGlobals.PersistentLoadedPartIds.Remove(kit_part.persistentId);
            kit_part.transform.position = Vector3.zero;
            kit_part.attPos0 = Vector3.zero;
            kit_part.transform.rotation = Quaternion.identity;
            kit_part.attRotation = Quaternion.identity;
            kit_part.attRotation0 = Quaternion.identity;
            kit_part.partTransform = kit_part.transform;
            kit_part.orgPos = kit_part.transform.root.InverseTransformPoint(kit_part.transform.position);
            kit_part.orgRot = Quaternion.Inverse(kit_part.transform.root.rotation) * kit_part.transform.rotation;
            kit_part.packed = true;
            kit_part.InitializeModules();
            //load kit into ConstructionKit module
            var kit_module = kit_part.FindModuleImplementing<ModuleConstructionKit>();
            if(kit_module == null)
            {
                Utils.Message("{0} has no ConstructionKit MODULE", KitPart);
                Destroy(kit_part);
                return null;
            }
            kit_module.StoreKit(kit);
            return kit_part;
        }

        IEnumerator<YieldInstruction> spawn_kit_vessel(Part kit_part)
        {
            //create ship construct
            var ship = new ShipConstruct("DIY Kit: "+Kit.Name, "", kit_part);
            ship.rotation = Quaternion.identity;
            ship.missionFlag = kit_part.flagURL;
            //var ship_node = ship.SaveShip();
            //ship.Unload();
            //ship.LoadShip(ship_node, ship.persistentId);
            this.Log("Spawnin ShipConstruct: {}", ship.SaveShip());//debug
            //spawn the ship construct
            var bounds = ship.Bounds(ship.Parts[0].localRoot.transform);
            yield return
                StartCoroutine(Spawner
                               .SpawnShipConstruct(ship,
                                                   SpawnManager.GetSpawnTransform(bounds),
                                                   SpawnManager.GetSpawnOffset(bounds) - bounds.center,
                                                   Vector3.zero,
                                                   on_vessel_launched: vsl => Kit = new VesselKit()));
            if(Animator != null)
                Animator.Open();
        }
        #endregion

        RealTimer spawn_space_check = new RealTimer();

        protected bool SpawnSpaceReady => 
        !spawn_space_check.Started || spawn_space_check.TimePassed;

        void OnTriggerStay(Collider col)
        {
            if(col != null && col.attachedRigidbody != null &&
               (!spawn_space_check.Started || 
                spawn_space_check.Remaining < spawn_space_check.Period/2))
            {
                if(col.CompareTag("Untagged"))
                {
                    var p = col.attachedRigidbody.GetComponent<Part>();
                    if(p != null && p.vessel != null && p.vessel != vessel)
                    {
                        spawn_space_check.Restart();
                        this.Log("Something is inside a trigger: {}", p);//debug
                    }
                }
            }
        }
    }
}
