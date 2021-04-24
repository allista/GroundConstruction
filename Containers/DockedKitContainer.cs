//   DockedKitContainer.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AT_Utils;
using AT_Utils.UI;

namespace GroundConstruction
{
    public class DockedKitContainer : DeployableKitContainer
    {
        [KSPField, SerializeField] public SpawnSpaceManager SpawnManager = new SpawnSpaceManager();

        [KSPField] public string ConstructionNode = "bottom";
        protected AttachNode construction_node;
        protected ModuleDockingNode construction_port;
        protected AttachNode recipient_node;

        [KSPField(guiActive = true, guiName = "Kit Dockable")]
        public bool DockableDisplay;

        [KSPField(isPersistant = true)] public int ConstructDockingNode = -1;
        private ConstructDockingNode construct_docking_node;

        [KSPField(isPersistant = true,
            guiName = "Wield Docking Port",
            guiActive = false,
            guiActiveEditor = false,
            guiActiveUnfocused = true,
            unfocusedRange = 50)]
        [UI_Toggle(scene = UI_Scene.Flight, enabledText = "Yes", disabledText = "No")]
        public bool WieldDockingPort;

        private Bounds get_deployed_bounds()
        {
            var b = ConstructDockingNode >= 0 && kit.DockingPossible
                ? kit.GetBoundsForDocking(ConstructDockingNode)
                : kit.ShipMetric.bounds;
            return new Bounds(b.center, Vector3.Max(b.size, Size));
        }

        private ConstructionWorkshop connected_construction_ws;
        private AssemblyWorkshop connected_assembly_ws;

        private bool find_connected_workshops(Part next_part, HashSet<uint> visited)
        {
#if DEBUG
            this.Log($"Searching for CWS and AWS in {next_part.GetID()}");
#endif
            if(next_part == null || visited.Contains(next_part.persistentId))
                return false;
            visited.Add(next_part.persistentId);
            if(connected_construction_ws == null)
                connected_construction_ws =
                    next_part.FindModuleImplementing<ConstructionWorkshop>();
            if(connected_assembly_ws == null)
                connected_assembly_ws =
                    next_part.FindModuleImplementing<AssemblyWorkshop>();
#if DEBUG
            this.Log($"Found so far: CWS: {connected_construction_ws.GetID()}, AWS: {connected_assembly_ws.GetID()}");
#endif
            if(connected_construction_ws != null && connected_assembly_ws != null)
                return true;
            foreach(var d in next_part.FindModulesImplementing<ModuleDockingNode>())
            {
                var docked_part = vessel[d.dockedPartUId];
                if(find_connected_workshops(docked_part, visited))
                    return true;
            }
            if(find_connected_workshops(next_part.parent, visited))
                return true;
            for(int i = 0, len = next_part.children.Count; i < len; i++)
            {
                if(find_connected_workshops(next_part.children[i], visited))
                    return true;
            }
            return false;
        }

        private void find_connected_workshops()
        {
            connected_assembly_ws = null;
            connected_construction_ws = null;
            var visited_parts = new HashSet<uint> { part.persistentId };
            if(construction_port != null)
            {
                var docked_part = vessel[construction_port.dockedPartUId];
                if(find_connected_workshops(docked_part, visited_parts))
                    return;
            }
            if(construction_node != null)
                find_connected_workshops(construction_node.attachedPart, visited_parts);
        }

        private void onVesselModified(Vessel vsl)
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

        public override void OnStart(StartState startState)
        {
            base.OnStart(startState);
            SpawnManager.Init(part);
            if(!string.IsNullOrEmpty(ConstructionNode))
            {
                construction_node = part.FindAttachNode(ConstructionNode);
                foreach(var port in part.FindModulesImplementing<ModuleDockingNode>())
                    if(port.nodeTransformName == ConstructionNode
                       || port.referenceAttachNode == ConstructionNode)
                    {
                        construction_port = port;
                        break;
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
            if(vessel != null)
                StartCoroutine(CallbackUtil.DelayedCallback(1, find_connected_workshops));
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
                WieldDockingPort = false;
            }
            var wieldField = Fields[nameof(WieldDockingPort)];
            wieldField.guiActive = wieldField.guiActiveEditor = DockableDisplay;
        }

        #region Deployment
        protected override bool ValidAssemblySpace => connected_assembly_ws != null;
        protected override bool ValidConstructionSpace => connected_construction_ws != null;

        public override bool CanConstruct(VesselKit vessel_kit) => !vessel_kit.HasLaunchClamps;

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
            var warning = StringBuilderCache.Acquire();
            warning.AppendLine("Deployment cannot be undone.");
            if(kit && kit.DockingPossible)
            {
                if(ConstructDockingNode >= 0)
                    warning.AppendLine(
                        $"You have chosen to <b>{Colors.Warning.Tag("dock the vessel")}</b> after construction.");
                else
                    warning.AppendLine(
                        $"You have chosen to <b>{Colors.Warning.Tag("launch the vessel")}</b> after construction.");
                warning.AppendLine("You cannot change it after deployment.");
            }
            warning.AppendLine("Are you sure?");
            deploymentWarning = warning.ToStringAndRelease().Trim();
            base.Deploy();
        }

        protected override Vector3 get_point_of_growth() =>
            part.partTransform.TransformPoint(construction_node.position);

        protected override Transform get_deploy_transform_unrotated(
            Vector3 size,
            out Vector3 spawn_offset
        ) =>
            SpawnManager.GetSpawnTransform(size, out spawn_offset);

        protected override Vector3 get_deployed_size() => get_deployed_bounds().size;

        protected override void update_kit_hull_mesh(
            Transform deployT,
            Vector3 deployed_size,
            Vector3 spawn_offset
        )
        {
            if(construct_docking_node != null)
                spawn_offset -= construct_docking_node.DockingOffset;
            base.update_kit_hull_mesh(deployT, deployed_size, spawn_offset);
        }

        private Part get_construction_part()
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

        private static readonly GUIContent launch_label = new GUIContent("Launch", "Launch the vessel.");

        private static readonly GUIContent docked_label = new GUIContent("Dock",
            "Dock the constructed vessel to the main vessel after launch.");

        private static readonly GUIContent wield_label = new GUIContent("Wield",
            "Wield the construct with the construction docking port (if any).");

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
                    if(Utils.ButtonSwitch(launch_label,
                        ConstructDockingNode < 0,
                        GUILayout.ExpandWidth(false)))
                    {
                        ConstructDockingNode = -1;
                        construct_docking_node = null;
                    }
                    if(Utils.ButtonSwitch(docked_label,
                        ConstructDockingNode >= 0,
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
                {
                    GUILayout.Label($"Dock via: {construct_docking_node}",
                        Styles.enabled,
                        GUILayout.ExpandWidth(false));
                    Utils.ButtonSwitch(wield_label,
                        ref WieldDockingPort,
                        GUILayout.ExpandWidth(false));
                }
                else
                    GUILayout.Label(launch_label,
                        Styles.enabled,
                        GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
                if(state == DeploymentState.IDLE && ConstructDockingNode >= 0)
                {
                    GUILayout.BeginHorizontal(Styles.white);
                    GUILayout.Label("Dock via:");
                    GUILayout.FlexibleSpace();
                    var choice = Utils.LeftRightChooser(construct_docking_node.ToString());
                    if(choice != 0)
                    {
                        ConstructDockingNode =
                            (ConstructDockingNode + choice) % kit.DockingNodes.Count;
                        if(ConstructDockingNode < 0)
                            ConstructDockingNode = kit.DockingNodes.Count - 1;
                        construct_docking_node = kit.DockingNodes[ConstructDockingNode];
                        update_part_events();
                        create_deploy_hint_mesh();
                    }
                    Utils.ButtonSwitch(wield_label,
                        ref WieldDockingPort,
                        GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
        }

        [KSPEvent(guiName = "After construction",
            guiActiveUnfocused = true,
            unfocusedRange = 10,
            guiActive = true,
            guiActiveEditor = true)]
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
        private AttachNode find_closest_node(
            IEnumerable<Part> parts,
            Vector3 world_pos,
            Vector3 world_fwd,
            out Vector3 world_delta,
            bool free
        )
        {
            world_delta = Vector3.zero;
            var best_dist = float.MaxValue;
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
                            best_node = n;
                            best_dist = dist;
                            world_delta = delta;
                        }
                    }
                }
            }
            if(free
               && best_node != null
               && best_node.attachedPart != null
               && best_node.attachedPart != part)
                best_node = null;
            return best_node;
        }

        private void update_recipient_node(Part construction_part)
        {
            var construction_node_pos =
                part.partTransform.TransformPoint(construction_node.position);
            var construction_node_fwd = part.partTransform
                .TransformDirection(construction_node.orientation)
                .normalized;
            // handle port wielding
            if(WieldDockingPort && construction_part.HasModuleImplementing<ModuleDockingNode>())
            {
                recipient_node = find_closest_node(construction_part.AllAttachedParts().Where(p => p != part),
                    construction_node_pos,
                    -construction_node_fwd,
                    out _,
                    false);
                return;
            }
            recipient_node = construction_part.FindAttachNodeByPart(part);
            if(recipient_node != null)
                return;
            recipient_node = find_closest_node(new[] { construction_part },
                construction_node_pos,
                -construction_node_fwd,
                out _,
                true);
        }

        protected override void on_vessel_launched(Vessel vsl)
        {
            base.on_vessel_launched(vsl);
            if(recipient_node != null)
            {
                var recipient_part = recipient_node.owner;
                var construction_part = get_construction_part();
                var dockingWithConstructionPart = recipient_part == construction_part;
                var reciprocal_part = dockingWithConstructionPart
                    ? part
                    : recipient_node.attachedPart;
                var reciprocal_node = dockingWithConstructionPart
                    ? construction_node
                    : reciprocal_part.FindAttachNodeByPart(recipient_part);
                if(reciprocal_node == null)
                {
                    Utils.Message(
                        "No suitable attachment node found in \"{0}\" to dock it to the {1}",
                        vsl.GetDisplayName(),
                        recipient_part.Title());
                    return;
                }
                var reciprocal_node_pos =
                    reciprocal_part.partTransform.TransformPoint(reciprocal_node.position);
                var docking_node = kit.GetDockingNode(vsl, ConstructDockingNode);
                if(docking_node == null)
                {
                    Utils.Message(
                        "No suitable attachment node found in \"{0}\" to dock it to the {1}",
                        vsl.GetDisplayName(),
                        recipient_part.Title());
                    return;
                }
                var docking_offset =
                    docking_node.owner.partTransform.TransformPoint(docking_node.position)
                    - reciprocal_node_pos;
                FXMonger.Explode(part, reciprocal_node_pos, 0);
                var docking_part = docking_node.owner;
                this.Log("Docking {} to {}", docking_part.GetID(), recipient_part.GetID());
                // vessels' position and rotation
                recipient_part.vessel.SetPosition(recipient_part.vessel.transform.position,
                    true);
                recipient_part.vessel.SetRotation(recipient_part.vessel.transform.rotation);
                docking_part.vessel.SetPosition(
                    docking_part.vessel.transform.position - docking_offset,
                    true);
                docking_part.vessel.SetRotation(docking_part.vessel.transform.rotation);
                recipient_part.vessel.IgnoreGForces(10);
                docking_part.vessel.IgnoreGForces(10);
                if(recipient_part == part.parent)
                    part.decouple();
                else if(recipient_part == construction_part.parent)
                    construction_part.decouple();
                else
                    recipient_part.decouple();
                if(!dockingWithConstructionPart)
                {
                    FXMonger.Explode(construction_part, construction_part.transform.position, 0);
                    construction_part.Die();
                }
                recipient_node.attachedPart = docking_part;
                recipient_node.attachedPartId = docking_part.flightID;
                docking_node.attachedPart = recipient_part;
                docking_node.attachedPartId = recipient_part.flightID;
                docking_part.Couple(recipient_part);
                // manage docking ports, if any
                foreach(var port in recipient_part.FindModulesImplementing<ModuleDockingNode>())
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
                        port.dockedPartUId = recipient_part.persistentId;
                        port.fsm.StartFSM(port.st_preattached);
                        break;
                    }
                }
                // add fuel lookups
                recipient_part.fuelLookupTargets.Add(docking_part);
                docking_part.fuelLookupTargets.Add(recipient_part);
                GameEvents.onPartFuelLookupStateChange.Fire(
                    new GameEvents.HostedFromToAction<bool, Part>(true,
                        docking_part,
                        recipient_part));
                // activate current vessel again vessel
                FlightGlobals.ForceSetActiveVessel(recipient_part.vessel);
                FlightInputHandler.SetNeutralControls();
                GameEvents.onVesselWasModified.Fire(recipient_part.vessel);
                recipient_node = null;
                this.Log("Docked {} to {}, new vessel {}",
                    docking_part,
                    recipient_part,
                    recipient_part.vessel.GetID());
            }
        }

        protected override IEnumerator launch(ShipConstruct construct)
        {
            var bounds = get_deployed_bounds();
            var docking_offset = construct_docking_node?.DockingOffset ?? Vector3.zero;
            var spawn_transform = get_deploy_transform(bounds.size, out var spawn_offset);
            vessel_spawner.SpawnShipConstruct(construct,
                spawn_transform,
                spawn_offset
                - bounds.center
                - docking_offset
                + construct.Parts[0].localRoot.transform.position,
                Vector3.zero,
                null,
                on_vessel_loaded,
                null,
                on_vessel_launched);
            yield return vessel_spawner.WaitForLaunch;
        }

        public override void Launch()
        {
            if(ConstructDockingNode >= 0)
            {
                var construction_part = get_construction_part();
                update_recipient_node(construction_part);
                if(recipient_node == null)
                {
                    Utils.Message("Cannot attach the construction to {0}", construction_part.name);
                    return;
                }
            }
            base.Launch();
        }
    }
}
