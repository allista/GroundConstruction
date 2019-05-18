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
    public class DockedKitContainer : DeployableKitContainer, IDockingConstructionSpace
    {
        [KSPField, SerializeField]
        public SpawnSpaceManager SpawnManager = new SpawnSpaceManager();

        [KSPField]
        public string ConstructionNode = "bottom";
        protected AttachNode construction_node;
        protected ModuleDockingNode construction_port;
        protected AttachNode recipient_node;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            SpawnManager.Init(part);
            SpawnManager.SetupSensor();
            if(!string.IsNullOrEmpty(ConstructionNode))
            {
                construction_node = part.FindAttachNode(ConstructionNode);
                foreach(var port in part.FindModulesImplementing<ModuleDockingNode>())
                {
                    if(port.nodeTransformName == ConstructionNode
                       || port.referenceAttachNode == ConstructionNode)
                    {
                        construction_port = port;
                        break;
                    }
                }
            }
            if(construction_node == null)
            {
                this.Log("ERROR: unable to find construction AttachNode with id: {}",
                    ConstructionNode);
                this.EnableModule(false);
                return;
            }
            Events["LaunchAndDockEvent"].active = Events["LaunchEvent"].active;
            update_unfocusedRange("LaunchAndDock");
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
            var cpart = construction_node.attachedPart;
            if(cpart == null && construction_port != null)
                cpart = vessel[construction_port.dockedPartUId];
            return cpart;
        }

        protected override IEnumerable<YieldInstruction> prepare_resize()
        {
            foreach(var i in base.prepare_resize())
                yield return i;
            if(vessel != null)
            {
                var cpart = get_construction_part();
                //undock docking ports, if any
                foreach(var port in part.FindModulesImplementing<ModuleDockingNode>())
                {
                    if(port != construction_port)
                    {
                        port.DecoupleAction(null);
                        port.UndockAction(null);
                        yield return null;
                    }
                }
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
        }

        protected override IEnumerable finalize_deployment()
        {
            foreach(var i in base.finalize_deployment()) yield return i;
            update_unfocusedRange("LaunchAndDock");
        }
        #endregion

        AttachNode find_closest_free_node(IEnumerable<Part> parts,
                                          Vector3 world_pos, Vector3 world_fwd,
                                          out Vector3 world_delta)
        {
            world_delta = Vector3.zero;
            var best_dist = float.MaxValue;
            Part best_part = null;
            AttachNode best_node = null;
            this.Log("Searching for closest attach node to: {}, fwd {}", world_pos, world_fwd);//debug
            foreach(var p in parts)
            {
                foreach(var n in p.attachNodes)
                {
                    p.Log("Examining attach node: {}", n.id);//debug
                    var orientation = p.partTransform.TransformDirection(n.orientation).normalized;
                    p.Log("{}.orientation: {} => {}", n.id, n.orientation, orientation);//debug
                    p.Log("{} vs {} cos: {}", n.id, ConstructionNode, Vector3.Dot(world_fwd, orientation));//debug
                    if(Vector3.Dot(world_fwd, orientation) > GLB.MaxDockingCos)
                    {
                        var delta = p.partTransform.TransformPoint(n.position) - world_pos;
                        var dist = delta.sqrMagnitude;
                        p.Log("{}.dist: {}", n.id, dist);//debug
                        if(best_node == null || dist < best_dist)
                        {
                            best_part = p;
                            best_node = n;
                            best_dist = dist;
                            world_delta = delta;
                        }
                    }
                }
            }
            this.Log("Part: {}, Best dist: {}, AttachNode: {}, occupied: {}",
                     best_part, best_dist, best_node?.id, best_node?.attachedPart != null);//debug
            if(best_node != null)
            {
                if(best_node.attachedPart != null && best_node.attachedPart != part)
                    best_node = null;
            }
            return best_node;
        }

        void update_recipient_node(Part construction_part)
        {
            var construction_node_pos = part.partTransform.TransformPoint(construction_node.position);
            var construction_node_fwd = part.partTransform.TransformDirection(construction_node.orientation).normalized;
            recipient_node = construction_part.FindAttachNodeByPart(part);
            if(recipient_node == null)
            {
                Vector3 _delta;
                recipient_node = find_closest_free_node(new[] { construction_part },
                                                        construction_node_pos,
                                                        -construction_node_fwd,
                                                        out _delta);
            }
        }

        protected override void on_vessel_launched(Vessel vsl)
        {
            base.on_vessel_launched(vsl);
            if(recipient_node != null)
            {
                var construction_node_pos = part.partTransform.TransformPoint(construction_node.position);
                var construction_node_fwd = part.partTransform.TransformDirection(construction_node.orientation).normalized;
                var construction_part = recipient_node.owner;
                Vector3 docking_delta;
                var docking_node = find_closest_free_node(vsl.Parts,
                                                          construction_node_pos,
                                                          construction_node_fwd,
                                                          out docking_delta);
                if(docking_node == null)
                {
                    Utils.Message("No suitable attachment node found in \"{0}\" to dock it to the {1}",
                                  vsl.GetDisplayName(), construction_part.Title());
                    return;
                }
                var spawn_transform = SpawnManager.GetSpawnTransform();
                var spawn_offset = spawn_transform.position
                    + spawn_transform.TransformDirection(SpawnManager.GetSpawnOffset(Size))
                    - construction_node_pos;
                this.Log("spawn offset: {}\ndocking delta: {}\ndelta: {}",
                         spawn_offset, docking_delta, docking_delta - spawn_offset);//debug
                if((docking_delta - spawn_offset).magnitude > GLB.MaxDockingDist)
                {
                    Utils.Message("Attachment node is too far away from the {0}",
                                  construction_part.Title());
                    return;
                }
                this.Log("docking node: area {}, xfeed {}\nrecipient node: area {}, xfeed {}",
                    docking_node.contactArea, docking_node.ResourceXFeed,
                    recipient_node.contactArea, recipient_node.ResourceXFeed);//debug
                FXMonger.Explode(part, construction_node_pos, 0);
                var docking_part = docking_node.owner;
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
                recipient_node.attachedPart = docking_part;
                recipient_node.attachedPartId = docking_part.flightID;
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
                recipient_node = null;
                this.Log("Docked {} to {}, new vessel {}", docking_part, construction_part, construction_part.vessel.GetID());//debug
            }
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

        public virtual void LaunchAndDock()
        {
            var recipient_part = get_construction_part();
            update_recipient_node(recipient_part);
            if(recipient_node != null)
                Launch();
            else
                Utils.Message("Cannot attach the construction to {0}", recipient_part.name);
        }

        [KSPEvent(guiName = "Launch and Dock",
#if DEBUG
                  guiActive = true,
#endif
                  guiActiveUnfocused = true, unfocusedRange = 10, active = false)]
        public void LaunchAndDockEvent() => LaunchAndDock();

        public override void EnableControls(bool enable = true)
        {
            base.EnableControls(enable);
            Events["LaunchAndDockEvent"].active = enable;
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
