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
    public class AssemblySpace : PartModule, IAssemblySpace
    {
        [KSPField] public string KitPart = "DIYKit";
        [KSPField] public SpawnSpaceManager SpawnManager = new SpawnSpaceManager();

        [KSPField(isPersistant = true)] public VesselKit kit = new VesselKit();

        #region IAssemblySpace
        public bool Empty => !kit;

        public void EnableControls(bool enable = true) {}

        public VesselKit GetKit(Guid id) => kit.id == id ? kit : null;

        public List<VesselKit> GetKits() => new List<VesselKit> { kit };

        public float KitToSpaceRatio(VesselKit kit)
        {
            if(!kit || !SpawnManager.MetricFits(kit.ShipMetric)) return -1;
            return 1-kit.ShipMetric.volume/SpawnManager.SpaceMetric.volume;
        }

        public void SetKit(VesselKit kit) => this.kit = kit;

        public void ShowUI(bool enable = true) {}

        public void SpawnKit()
        {
            if(!kit) return;
            if(spawning)
            {
                Utils.Message("In progress...");
                return;
            }
            if(kit.StageComplete(DIYKit.ASSEMBLY)) 
            {
                Utils.Message("Kit is not assembled yet");
                return;
            }
            var part_info = PartLoader.getPartInfoByName(KitPart);
            if(part_info == null)
            {
                Utils.Message("No such part: {0}", KitPart);
                return;
            }
            StartCoroutine(spawn_kit_vessel(part_info));
        }

        bool spawning;
        Vessel spawned_vessel;

   //     IEnumerator<YieldInstruction> spawn_kit_vessel1(AvailablePart part_info)
   //     {
   //         spawning = true;
   //         //create part
   //         var kit_part = Instantiate(part_info.partPrefab);
   //         kit_part.partInfo = part_info;
   //         kit_part.persistentId = FlightGlobals.GetUniquepersistentId();
			//kit_part.craftID = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
        //    //load kit into ConstructionKit module
        //    var kit_module = kit_part.FindModuleImplementing<ModuleConstructionKit>();
        //    if(kit_module == null)
        //    {
        //        Utils.Message("{0} has no ConstructionKit MODULE", KitPart);
        //        Destroy(kit_part);
        //        spawning = false;
        //        yield break;
        //    }
        //    kit_module.StoreKit(kit);
        //    //create part node
        //    var proto_part = new ProtoPartSnapshot(kit_part, null);
        //    var part_node = new ConfigNode("PART");
        //    proto_part.Save(part_node);
        //    //create proto vessel
        //    var vessel_node = ProtoVessel.CreateVesselNode(kit.Name, VesselType.Lander, 
        //                                                   new Orbit(vessel.orbit), 0, 
        //                                                   new[] {part_node});
        //    var proto_vessel = new ProtoVessel(vessel_node, HighLogic.CurrentGame);
        //    //spawn
        //    foreach(var i in SpawnManager.SpawnProtoVessel(proto_vessel))
        //        yield return i;
        //    spawning = false;
        //    kit = new VesselKit();
        //}

        IEnumerator<YieldInstruction> spawn_kit_vessel(AvailablePart part_info)
        {
            spawning = true;
            //create part
            var kit_part = Instantiate(part_info.partPrefab);
            kit_part.partInfo = part_info;
            kit_part.persistentId = FlightGlobals.GetUniquepersistentId();
            kit_part.gameObject.SetActive(true);
            FlightGlobals.PersistentLoadedPartIds.Remove(kit_part.persistentId);
            //load kit into ConstructionKit module
            var kit_module = kit_part.FindModuleImplementing<ModuleConstructionKit>();
            if(kit_module == null)
            {
                Utils.Message("{0} has no ConstructionKit MODULE", KitPart);
                Destroy(kit_part);
                spawning = false;
                yield break;
            }
            kit_module.StoreKit(kit);
            //create ship construct
            var ship = new ShipConstruct(kit.Name, "", kit_part);
            //position ship construct
			var spawn_transform = SpawnManager.GetSpawnTransform(kit.ShipMetric);
            FlightCameraOverride.AnchorForSeconds(FlightCameraOverride.Mode.Hold, 
                                                  FlightGlobals.ActiveVessel.transform, 1);
            if(FlightGlobals.ready)
                FloatingOrigin.SetOffset(spawn_transform.position);
            float angle;
            Vector3 axis;
            spawn_transform.rotation.ToAngleAxis(out angle, out axis);
            var root = ship.parts[0].localRoot.transform;
            var offset = (
                spawn_transform.position - 
                ship.Bounds().center + 
                spawn_transform.TransformDirection(SpawnManager.GetSpawnOffset(kit.ShipMetric)));
            root.Translate(offset, Space.World);
            root.RotateAround(spawn_transform.position, axis, angle);
            //initialize new vessel
            ShipConstruction.AssembleForLaunch(ship, 
                                               vessel.landedAt, vessel.displaylandedAt, 
                                               part.flagURL, 
                                               FlightDriver.FlightStateCache,
                                               new VesselCrewManifest());
            spawned_vessel = FlightGlobals.Vessels[FlightGlobals.Vessels.Count - 1];
            spawned_vessel.situation = vessel.situation;
            spawned_vessel.skipGroundPositioning = true;

			spawned_vessel = null;
            spawning = false;
            kit = new VesselKit();
        }
        #endregion
    }
}
