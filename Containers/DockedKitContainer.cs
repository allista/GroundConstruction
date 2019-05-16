//   DockedKitContainer.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
    public class DockedKitContainer : DeployableKitContainer
    {
        [KSPField, SerializeField]
        public SpawnSpaceManager SpawnManager = new SpawnSpaceManager();

        [KSPField]
        public string ConstructionNode = "bottom";

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            SpawnManager.Init(part);
            SpawnManager.SetupSensor();
        }

        #region Deployment
        public override bool CanConstruct(VesselKit kit) => !kit.HasLaunchClamps;

        protected override bool can_deploy()
        {
            if(!base.can_deploy())
                return false;
            if(vessel.angularVelocity.sqrMagnitude > GLB.DeployMaxAV)
            {
                Utils.Message("Cannot deploy the kit while the vessel is rotating.");
                return false;
            }
            return true;
        }

        protected override Transform get_deploy_transform() =>
        SpawnManager.GetSpawnTransform() ?? part.transform;

        protected override Vector3 get_deployed_offset() => SpawnManager.GetSpawnOffset(Size);

        protected override Vector3 get_deployed_size() => kit.ShipMetric.size;

        Part get_construction_part()
        {
            //try to find part connected though the construction node
            Part cpart = null;
            var cnode = part.FindAttachNode(ConstructionNode);
            if(cnode != null)
                cpart = cnode.attachedPart;
            if(cpart == null)
            {
                foreach(var port in part.FindModulesImplementing<ModuleDockingNode>())
                {
                    if(port.nodeTransformName == ConstructionNode
                       || port.referenceAttachNode == ConstructionNode)
                    {
                        cpart = vessel[port.dockedPartUId];
                        break;
                    }
                }
            }
            return cpart;
        }

        protected override IEnumerable prepare_deployment()
        {
            foreach(var i in base.prepare_deployment())
                yield return i;
            var cpart = get_construction_part();
            //decouple all parts but the one on the construction node
            if(part.parent != null && part.parent != cpart)
            {
                part.decouple(2);
                yield return null;
            }
            while(part.children.Count > 0)
            {
                var child = part.children[0];
                if(child != cpart)
                {
                    child.decouple(2);
                    yield return null;
                }
            }
        }
        #endregion

        protected override void on_vessel_launched(Vessel vsl)
        {
            base.on_vessel_launched(vsl);
            var construction_node = part.FindAttachNode(ConstructionNode);
            this.Log("ConstructionNode {}: {}", ConstructionNode, construction_node);//debug
            if(construction_node == null)
                return;
            var construction_node_pos = part.partTransform.TransformPoint(construction_node.position);
            var construction_node_fwd = part.partTransform.TransformDirection(construction_node.orientation).normalized;
            var best_dist = float.MaxValue;
            var docking_delta = Vector3.zero;
            Part docking_part = null;
            AttachNode docking_node = null;
            vsl.Log("Parts: {}", vsl.Parts);
            foreach(var p in vsl.Parts)
            {
                p.Log("Examining part of the launched vessel");//debug
                foreach(var n in p.attachNodes)
                {
                    p.Log("Examining attach node: {}", n.id);//debug
                    if(n.attachedPart == null)
                    {
                        var orientation = p.partTransform.TransformDirection(n.orientation);
                        p.Log("{}.orientation: {} => {}", n.id, n.orientation, orientation);//debug
                        p.Log("{} vs {} cos: {}", n.id, ConstructionNode, Vector3.Dot(construction_node_fwd, orientation));//debug
                        if(Vector3.Dot(construction_node_fwd, orientation) > 0.9)
                        {
                            var delta = p.partTransform.TransformPoint(n.position) - construction_node_pos;
                            var dist = delta.sqrMagnitude;
                            p.Log("{}.dist: {}", n.id, dist);//debug
                            if(docking_node == null || dist < best_dist)
                            {
                                docking_part = p;
                                docking_node = n;
                                docking_delta = delta;
                                best_dist = dist;
                            }
                        }
                    }
                }
            }
            this.Log("Best dist: {}, AttachNode: {}", best_dist, docking_node.id);//debug
            var construction_part = get_construction_part();
            if(construction_part == null)
                return;
            var recepient_node = construction_part.FindAttachNodeByPart(part);
            construction_part.Log("Constructed from: {}", recepient_node?.id);//debug
            if(recepient_node == null)
                return;
            this.Log("Docking {} to {}", docking_part.GetID(), construction_part.GetID());//debug
            var old_vessel = construction_part.vessel;
            // reset vessels' position and rotation
            construction_part.vessel.SetPosition(construction_part.vessel.transform.position, true);
            construction_part.vessel.SetRotation(construction_part.vessel.transform.rotation);
            docking_part.vessel.SetPosition(docking_part.vessel.transform.position - docking_delta, true);
            docking_part.vessel.SetRotation(docking_part.vessel.transform.rotation);
            construction_part.vessel.IgnoreGForces(10);
            docking_part.vessel.IgnoreGForces(10);
            if(construction_part == part.parent)
                part.decouple();
            else
                construction_part.decouple();
            recepient_node.attachedPart = docking_part;
            recepient_node.attachedPartId = docking_part.flightID;
            docking_node.attachedPart = construction_part;
            docking_node.attachedPartId = construction_part.flightID;
            docking_part.Couple(construction_part);
            // add fuel lookups
            construction_part.fuelLookupTargets.Add(docking_part);
            docking_part.fuelLookupTargets.Add(construction_part);
            GameEvents.onPartFuelLookupStateChange.Fire(new GameEvents.HostedFromToAction<bool, Part>(true, docking_part, construction_part));
            FlightGlobals.ForceSetActiveVessel(construction_part.vessel);
            FlightInputHandler.SetNeutralControls();
            GameEvents.onVesselWasModified.Fire(construction_part.vessel);
            this.Log("Docked {} to {}, new vessel {}", docking_part, construction_part, construction_part.vessel.GetID());//debug
        }

        protected override IEnumerator<YieldInstruction> launch(ShipConstruct construct)
        {
            var bounds = new Metric(construct, world_space: true).bounds;
            yield return
                StartCoroutine(vessel_spawner
                               .SpawnShipConstruct(construct,
                                                   SpawnManager.GetSpawnTransform(bounds),
                                                   SpawnManager.GetSpawnOffset(bounds)
                                                   - bounds.center
                                                   + construct.Parts[0].localRoot.transform.position,
                                                   Vector3.zero,
                                                   null,
                                                   on_vessel_loaded,
                                                   null,
                                                   on_vessel_launched));
        }

#if DEBUG
        void OnRenderObject()
        {
            if(vessel == null) return;
            var T = SpawnManager.GetSpawnTransform();
            if(T != null)
            {
                var pos = T.position + T.TransformDirection(SpawnManager.GetSpawnOffset(Size));
                Utils.GLVec(pos, T.up, Color.green);
                Utils.GLVec(pos, T.forward, Color.blue);
                Utils.GLVec(pos, T.right, Color.red);
            }
            if(part.attachJoint != null)
            {
                var j = part.attachJoint;
                if(j.Host != null)
                    Utils.GLDrawPoint(j.Host.transform.TransformPoint(j.HostAnchor), Color.magenta);
            }
        }
#endif
    }
}
