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
using AT_Utils.UI;

namespace GroundConstruction
{
    public class DockedKitContainer : DeployableKitContainer
    {
        [KSPField, SerializeField]
        public SpawnSpaceManager SpawnManager = new SpawnSpaceManager();

        [KSPField]
        public string ConstructionNode = "bottom";
        protected AttachNode construction_node;
        protected ModuleDockingNode construction_port;
        protected AttachNode recipient_node;

        [KSPField(guiActive = true, guiName = "Kit Dockable")]
        public bool DockableDisplay;

        [KSPField(isPersistant = true)]
        public int ConstructDockingNode = -1;
        ConstructDockingNode construct_docking_node;

        Bounds get_deployed_bounds() =>
        ConstructDockingNode >= 0 && kit.DockingPossible
            ? kit.GetBoundsForDocking(ConstructDockingNode)
            : kit.ShipMetric.bounds;

        private ConstructionWorkshop connected_construction_ws;
        private AssemblyWorkshop connected_assembly_ws;

        bool find_connected_workshops(AttachNode through_node, HashSet<uint> visited)
        {
            var next_part = through_node.attachedPart;
            if(next_part == null || visited.Contains(next_part.persistentId))
                return false;
            visited.Add(next_part.persistentId);
            if(connected_construction_ws == null)
                connected_construction_ws =
                    next_part.FindModuleImplementing<ConstructionWorkshop>();
            if(connected_assembly_ws == null)
                connected_assembly_ws =
                    next_part.FindModuleImplementing<AssemblyWorkshop>();
            if(connected_construction_ws != null && connected_assembly_ws != null)
                return true;
            for(int i = 0, len = next_part.attachNodes.Count; i < len; i++)
            {
                var an = next_part.attachNodes[i];
                if(an.attachedPart == null || visited.Contains(an.attachedPart.persistentId))
                    continue;
                if(find_connected_workshops(an, visited))
                    return true;
            }
            return false;
        }

        void find_connected_workshops()
        {
            connected_assembly_ws = null;
            connected_construction_ws = null;
            if(construction_node != null)
                find_connected_workshops(construction_node,
                    new HashSet<uint> { part.persistentId });
        }

        void onVesselModified(Vessel vsl)
        {
            if(vsl != null && vsl == vessel)
                find_connected_workshops();
        }

        public override void OnAwake()
        {
            base.OnAwake();
            GameEvents.onVesselWasModified.Add(onVesselModified);
        }

        protected override void OnDestroy()
        {
            GameEvents.onVesselWasModified.Remove(onVesselModified);
            base.OnDestroy();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            SpawnManager.Init(part);
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
            if(kit && ConstructDockingNode >= 0)
                construct_docking_node = kit.DockingNodes[ConstructDockingNode];
            find_connected_workshops();
        }

        protected override void update_part_events()
        {
            base.update_part_events();
            var evt = Events[nameof(ToggleDockedConstruction)];
            if(construct_docking_node != null)
            {
                evt.guiName = "Dock via " + construct_docking_node;
            }
            else
                evt.guiName = "Launch after construction";
        }

        protected override void update_part_info()
        {
            base.update_part_info();
            DockableDisplay = kit && kit.DockingPossible;
            if(!DockableDisplay)
            {
                ConstructDockingNode = -1;
                construct_docking_node = null;
            }
        }

        #region Deployment
        protected override bool ValidAssemblySpace => connected_assembly_ws != null;
        protected override bool ValidConstructionSpace => connected_construction_ws != null;

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

        public override void Deploy()
        {
            warning.Message = "Deployment cannot be undone.";
            if(kit && kit.DockingPossible)
            {
                if(ConstructDockingNode >= 0)
                    warning.Message += Colors.Warning.Tag("\nYou have chosen to <b>dock the vessel</b> after construction.");
                else
                    warning.Message += Colors.Warning.Tag("\nYou have chosen to <b>launch the vessel</b> after construction.");
                warning.Message += "\nYou cannot change it after deployment.";
            }
            warning.Message += "\nAre you sure?";
            base.Deploy();
        }

        protected override Vector3 get_point_of_growth() => 
            part.partTransform.TransformPoint(construction_node.position);

        protected override Transform get_deploy_transform_unrotated(Vector3 size, out Vector3 spawn_offset) =>
        SpawnManager.GetSpawnTransform(size, out spawn_offset);

        protected override Vector3 get_deployed_size() => get_deployed_bounds().size;

        protected override void update_kit_hull_mesh(Transform deployT,
            Vector3 deployed_size, Vector3 spawn_offset)
        {
            if(construct_docking_node != null)
                spawn_offset -= construct_docking_node.DockingOffset;
            base.update_kit_hull_mesh(deployT, deployed_size, spawn_offset);
        }

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

        protected override IEnumerable prepare_deployment()
        {
            update_part_info();
            foreach(var i in base.prepare_deployment())
                yield return i;
        }

        static readonly GUIContent launch_label = new GUIContent("Launch", "Launch the vessel.");
        static readonly GUIContent docked_label = new GUIContent("Dock", "Dock the constructed vessel to the main vessel after launch.");
        public override void DrawOptions()
        {
            GUILayout.BeginVertical();
            base.DrawOptions();
            if(kit.DockingPossible)
            {
                GUILayout.BeginHorizontal(Styles.white);
                GUILayout.Label("After construction:");
                GUILayout.FlexibleSpace();
                if(state == DeploymentState.IDLE)
                {
                    var old_value = ConstructDockingNode;
                    if(Utils.ButtonSwitch(launch_label, ConstructDockingNode < 0,
                                          GUILayout.ExpandWidth(false)))
                    {
                        ConstructDockingNode = -1;
                        construct_docking_node = null;
                    }
                    if(Utils.ButtonSwitch(docked_label, ConstructDockingNode >= 0,
                                          GUILayout.ExpandWidth(false)))
                    {
                        ConstructDockingNode = 0;
                        construct_docking_node = kit.DockingNodes[0];
                    }
                    if(ConstructDockingNode != old_value)
                    {
                        update_part_events();
                        create_deploy_hint_mesh();
                    }
                }
                else if(ConstructDockingNode >= 0)
                    GUILayout.Label("Dock " + construct_docking_node,
                                    Styles.enabled, GUILayout.ExpandWidth(false));
                else
                    GUILayout.Label(launch_label,
                                    Styles.enabled, GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
                if(state == DeploymentState.IDLE && ConstructDockingNode >= 0)
                {
                    GUILayout.BeginHorizontal(Styles.white);
                    GUILayout.Label("Dock via:");
                    GUILayout.FlexibleSpace();
                    var choice = Utils.LeftRightChooser(construct_docking_node.ToString());
                    if(choice != 0)
                    {
                        ConstructDockingNode = (ConstructDockingNode + choice) % kit.DockingNodes.Count;
                        if(ConstructDockingNode < 0) ConstructDockingNode = kit.DockingNodes.Count - 1;
                        construct_docking_node = kit.DockingNodes[ConstructDockingNode];
                        update_part_events();
                        create_deploy_hint_mesh();
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
        }

        [KSPEvent(guiName = "After construction",
                  guiActiveUnfocused = true, unfocusedRange = 10,
                  guiActive = true, guiActiveEditor = true)]
        public void ToggleDockedConstruction()
        {
            if(state == DeploymentState.IDLE && kit && kit.DockingPossible)
            {
                ConstructDockingNode += 1;
                if(ConstructDockingNode < kit.DockingNodes.Count)
                    construct_docking_node = kit.DockingNodes[ConstructDockingNode];
                else
                {
                    ConstructDockingNode = -1;
                    construct_docking_node = null;
                }
                update_part_events();
                create_deploy_hint_mesh();
            }
        }
        #endregion

        //TODO: extract this method somewhere to use both here and in VesselKit
        AttachNode find_closest_free_node(IEnumerable<Part> parts,
                                          Vector3 world_pos, Vector3 world_fwd,
                                          out Vector3 world_delta)
        {
            world_delta = Vector3.zero;
            var best_dist = float.MaxValue;
            Part best_part = null;
            AttachNode best_node = null;
            foreach(var p in parts)
            {
                foreach(var n in p.attachNodes)
                {
                    var orientation = p.partTransform.TransformDirection(n.orientation).normalized;
                    if(Vector3.Dot(world_fwd, orientation) > GLB.MaxDockingCos)
                    {
                        var delta = p.partTransform.TransformPoint(n.position) - world_pos;
                        var dist = delta.sqrMagnitude;
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
            if(best_node != null
               && best_node.attachedPart != null
               && best_node.attachedPart != part)
                best_node = null;
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
                var construction_part = recipient_node.owner;
                var docking_node = kit.GetDockingNode(vsl, ConstructDockingNode);
                if(docking_node == null)
                {
                    Utils.Message("No suitable attachment node found in \"{0}\" to dock it to the {1}",
                                  vsl.GetDisplayName(), construction_part.Title());
                    return;
                }
                var docking_offset = docking_node.owner.partTransform.TransformPoint(docking_node.position)
                                         - construction_node_pos;
                FXMonger.Explode(part, construction_node_pos, 0);
                var docking_part = docking_node.owner;
                this.Log("Docking {} to {}", docking_part.GetID(), construction_part.GetID());
                // vessels' position and rotation
                construction_part.vessel.SetPosition(construction_part.vessel.transform.position, true);
                construction_part.vessel.SetRotation(construction_part.vessel.transform.rotation);
                docking_part.vessel.SetPosition(docking_part.vessel.transform.position - docking_offset, true);
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
                // manage docking ports, if any
                foreach(var port in construction_part.FindModulesImplementing<ModuleDockingNode>())
                {
                    if(port.referenceNode == recipient_node)
                    {
                        port.dockedPartUId = docking_part.persistentId;
                        port.fsm.StartFSM(port.st_preattached);
                        break;
                    }
                }
                foreach(var port in docking_part.FindModulesImplementing<ModuleDockingNode>())
                {
                    if(port.referenceNode == docking_node)
                    {
                        port.dockedPartUId = construction_part.persistentId;
                        port.fsm.StartFSM(port.st_preattached);
                        break;
                    }
                }
                // add fuel lookups
                construction_part.fuelLookupTargets.Add(docking_part);
                docking_part.fuelLookupTargets.Add(construction_part);
                GameEvents.onPartFuelLookupStateChange.Fire(new GameEvents.HostedFromToAction<bool, Part>(true, docking_part, construction_part));
                FlightGlobals.ForceSetActiveVessel(construction_part.vessel);
                FlightInputHandler.SetNeutralControls();
                GameEvents.onVesselWasModified.Fire(construction_part.vessel);
                recipient_node = null;
                this.Log("Docked {} to {}, new vessel {}", 
                         docking_part, construction_part, construction_part.vessel.GetID());
            }
        }

        protected override IEnumerator<YieldInstruction> launch(ShipConstruct construct)
        {
            var bounds = get_deployed_bounds();
            var docking_offset = construct_docking_node?.DockingOffset ?? Vector3.zero;
            var spawn_transform = get_deploy_transform(bounds.size, out var spawn_offset);
            yield return
                StartCoroutine(vessel_spawner
                               .SpawnShipConstruct(construct,
                                                   spawn_transform,
                                                   spawn_offset
                                                   - bounds.center
                                                   - docking_offset
                                                   + construct.Parts[0].localRoot.transform.position,
                                                   Vector3.zero,
                                                   null,
                                                   on_vessel_loaded,
                                                   null,
                                                   on_vessel_launched));
        }

        public override void Launch()
        {
            if(ConstructDockingNode >= 0)
            {
                var recipient_part = get_construction_part();
                update_recipient_node(recipient_part);
                if(recipient_node == null)
                {
                    Utils.Message("Cannot attach the construction to {0}", recipient_part.name);
                    return;
                }
            }
            base.Launch();
        }
    }
}
