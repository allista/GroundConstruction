//   DeployableModel.cs
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
    public abstract class DeployableModel : SerializableFiledsPartModule, IDeployable
    {
        class DockAnchor
        {
            public Vector3 vesselAnchor, localAnchor;

            public DockAnchor(Part p, Vector3 anchor, Vector3 scale)
            {
                vesselAnchor = p.vessel.transform.InverseTransformPoint(anchor);
                localAnchor = Vector3.Scale(p.transform.InverseTransformPoint(anchor),
                                            scale.Inverse());
            }

            public override string ToString()
            {
                return Utils.Format("local {}, vessel {}", localAnchor, vesselAnchor);
            }
        }

        protected static Globals GLB { get { return Globals.Instance; } }

        protected Transform model;

        protected MeshFilter deploy_hint_mesh;
        protected static readonly Color deploy_hint_color = new Color(0, 1, 0, 0.25f);

        public abstract string Name { get; }
        [KSPField] public string MetricMesh = string.Empty;
        [KSPField] public Vector3 MinSize = new Vector3(0.5f, 0.5f, 0.5f);

        public Vector3 OrigScale;
        public Vector3 OrigSize;
        public Vector3 OrigPartSize;
        public Vector3 PartCenter;
        [KSPField(isPersistant = true)] public Vector3 Size;
        [KSPField(isPersistant = true)] public Vector3 TargetSize;
        [KSPField(isPersistant = true)] public DeploymentState state;
        [KSPField(isPersistant = true)] public bool ShowDeployHint;
        bool just_started;

        protected SimpleWarning warning;

        Dictionary<Part, DockAnchor> dock_anchors = new Dictionary<Part, DockAnchor>();

        public DeploymentState State => state;

        Vector3 get_scale() => Vector3.Scale(Size, OrigSize.Inverse());

        void save_dock_anchor(Part p, Vector3 scale) =>
        dock_anchors[p] = new DockAnchor(part, find_dock_anchor(p), scale);

        Vector3 find_dock_anchor(Part docked)
        {
            foreach(var d in part.FindModulesImplementing<ModuleDockingNode>())
            {
                if(d.dockedPartUId == docked.flightID)
                    return d.nodeTransform.position;
            }
            foreach(var d in docked.FindModulesImplementing<ModuleDockingNode>())
            {
                if(d.dockedPartUId == part.flightID)
                    return d.nodeTransform.position;
            }
            var j = part.parent == docked ? part.attachJoint : docked.attachJoint;
            return j.Host.transform.TransformPoint(j.HostAnchor);
        }

        void update_attach_node(AttachNode node, Vector3 scale, float scale_quad, HashSet<Part> updated_parts, bool update_parts)
        {
            node.position = Vector3.Scale(node.originalPosition, scale);
            //update node breaking forces
            node.breakingForce *= scale_quad;
            node.breakingTorque *= scale_quad;
            //move parts
            if(update_parts && node.attachedPart != null)
            {
                //this.Log("Updating attach node: {}, attached {}: {}", 
                //         node.id, node.attachedPartId, node.attachedPart);//debug
                part.UpdateAttachedPartPos(node);
                updated_parts.Add(node.attachedPart);
            }
        }

        void update_other_node(AttachNode other, Vector3 rel_scale, HashSet<Part> updated_parts)
        {
            var cur_pos = part.transform
                              .InverseTransformPoint(other.owner.transform.position
                                                     + other.owner.transform.TransformDirection(other.position));
            var new_pos = Vector3.Scale(cur_pos, rel_scale);
            //other.owner.Log("Updating other node {}, delta {}", other.id, new_pos - cur_pos);//debug
            part.UpdateAttachedPartPos(other.owner, part.transform.TransformDirection(new_pos - cur_pos));
            updated_parts.Add(other.owner);
        }

        void update_docked_part(Part docked, Vector3 scale, HashSet<Part> updated_parts)
        {
            DockAnchor anchor;
            if(dock_anchors.TryGetValue(docked, out anchor))
            {
                var new_pos = part.transform.TransformPoint(Vector3.Scale(anchor.localAnchor, scale));
                var delta = new_pos - vessel.transform.TransformPoint(anchor.vesselAnchor);
                //docked.Log("Updating docked part: scale {}, anchor {}, new local {}, dpos {}",
                //           scale, anchor, Vector3.Scale(anchor.localAnchor, scale), delta);//debug
                part.UpdateAttachedPartPos(docked, delta);
                updated_parts.Add(docked);
            }
        }

        protected void update_model(bool update_parts)
        {
            //rescale part
            var scale = get_scale();
            var local_scale = Vector3.Scale(OrigScale, scale);
            var rel_scale = Vector3.Scale(local_scale, model.localScale.Inverse());
            model.localScale = local_scale;
            model.hasChanged = true;
            part.transform.hasChanged = true;
            create_deploy_hint_mesh();
            //update attach nodes
            var updated_parts = new HashSet<Part>();
            var scale_quad = rel_scale.sqrMagnitude;
            part.attachNodes.ForEach(n => update_attach_node(n, scale, scale_quad, updated_parts, update_parts));
            //update this surface attach node
            if(part.srfAttachNode != null)
                update_attach_node(part.srfAttachNode, scale, scale_quad, updated_parts, update_parts);
            //update parts
            if(update_parts)
            {
                //updated docked parent
                if(part.parent != null && !updated_parts.Contains(part.parent))
                    update_docked_part(part.parent, scale, updated_parts);
                for(int i = 0, count = part.children.Count; i < count; i++)
                {
                    var child = part.children[i];
                    //surface attached children
                    if(child.srfAttachNode != null && child.srfAttachNode.attachedPart == part)
                        update_other_node(child.srfAttachNode, rel_scale, updated_parts);
                    //docked children
                    else if(!updated_parts.Contains(child))
                        update_docked_part(child, scale, updated_parts);
                }
                if(vessel != null)
                    vessel.IgnoreGForces(10);
            }
        }

        protected YieldInstruction slow_resize()
        {
            if(resize_coro != null)
                StopCoroutine(resize_coro);
            resize_coro = _resize();
            return StartCoroutine(resize_coro);
        }

        protected virtual IEnumerable<YieldInstruction> prepare_resize()
        {
            part.BreakConnectedCompoundParts();
            yield return null;
        }

        private int scenery_mask;
        bool servos_locked;

        void change_servos_lock(bool is_locked)
        {
            GameEvents.onRoboticPartLockChanging.Fire(part, servos_locked);
            if(vessel != null)
                vessel.CycleAllAutoStrut();
            servos_locked = is_locked;
            GameEvents.onRoboticPartLockChanged.Fire(part, servos_locked);
        }
        
        IEnumerator<YieldInstruction> resize_coro;
        IEnumerator<YieldInstruction> _resize()
        {
            if(Size == TargetSize)
                yield break;
            foreach(var i in prepare_resize())
                yield return i;
            var start = Size;
            var time = 0f;
            var speed = Mathf.Min(GLB.MaxDeploymentMomentum
                                  / part.TotalMass()
                                  / Mathf.Abs((TargetSize - Size).MaxComponentF()),
                                  1 / GLB.MinDeploymentTime);
            var up = vessel.up;
            var scale = get_scale();
            if(vessel.LandedOrSplashed)
            {
                RaycastHit hit;
                if(Physics.Raycast(part.partTransform.position, -up, out hit,
                                   Mathf.Max(vessel.heightFromTerrain * 2, 1), scenery_mask))
                    up = hit.normal;
            }
            dock_anchors.Clear();
            if(part.parent != null && part.attachJoint != null)
                save_dock_anchor(part.parent, scale);
            foreach(var child in part.children)
            {
                if(child.attachJoint != null)
                    save_dock_anchor(child, scale);
            }
            change_servos_lock(false);
            GameEvents.onRoboticPartLockChanged.Fire(part, servos_locked);
            yield return null;
            while(time < 1)
            {
                var old_size = Size;
                time += speed * TimeWarp.fixedDeltaTime;
                Size = Vector3.Lerp(start, TargetSize, time);
                update_model(true);
                GameEvents.onActiveJointNeedUpdate.Fire(vessel);
                if(vessel.LandedOrSplashed && part.GroundContact)
                {
                    FlightGlobals.overrideOrbit = true;
                    vessel.SetPosition(vessel.vesselTransform.position + up * (Size - old_size).y);
                }
                yield return new WaitForFixedUpdate();
            }
            change_servos_lock(true);
            if(FlightGlobals.overrideOrbit)
                FlightGlobals.overrideOrbit = false;
            Size = TargetSize;
        }

        public virtual bool IsJointUnlocked()
        {
            return !servos_locked;
        }

        public override void OnAwake()
        {
            base.OnAwake();
            scenery_mask = Utils.GetLayer("Local Scenery");
            warning = gameObject.AddComponent<SimpleWarning>();
            warning.Message = "Deployment cannot be undone.\nAre you sure?";
            warning.yesCallback = start_deployment;
            model = part.transform.Find("model");
            //add deploy hints
            var obj = new GameObject("DeployHintsMesh", typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(part.transform);
            deploy_hint_mesh = obj.GetComponent<MeshFilter>();
            deploy_hint_mesh.mesh = new Mesh();
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.material = Utils.no_z_material;
            renderer.material.color = deploy_hint_color;
            renderer.enabled = true;
            obj.SetActive(false);
        }

        protected virtual void OnDestroy()
        {
            Destroy(deploy_hint_mesh.gameObject);
            Destroy(warning);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            just_started = true;
            StartCoroutine(CallbackUtil.DelayedCallback(1, create_deploy_hint_mesh));
            if(State == DeploymentState.DEPLOYING)
                StartCoroutine(deploy());
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if(OrigSize.IsZero() || OrigScale.IsZero() || OrigPartSize.IsZero())
            {
                var part_metric = new Metric(part);
                var metric = default(Metric);
                if(!string.IsNullOrEmpty(MetricMesh))
                {
                    var metric_mesh = part.FindModelComponent<MeshFilter>(MetricMesh);
                    if(metric_mesh != null)
                        metric = new Metric(metric_mesh, part.transform);
                    else
                        this.Log("[WARNING] MeshMetric: no such MeshFilter: {}", MetricMesh);
                }
                if(metric.Empty)
                    metric = part_metric;
                OrigSize = metric.size;
                OrigScale = model.localScale;
                OrigPartSize = part_metric.size;
                PartCenter = part_metric.center.Local2Local(part.partTransform, model);
            }
            if(Size.IsZero())
                Size = OrigSize;
            update_model(false);
            //deprecated config conversion
            if(node.HasValue("Deploying"))
            {
                state = DeploymentState.IDLE;
                var val = node.GetValue("Deploying");
                if(bool.TryParse(val, out bool _deploy) && _deploy)
                    state = DeploymentState.DEPLOYING;
                else
                {
                    val = node.GetValue("Deployed");
                    if(bool.TryParse(val, out _deploy) && _deploy)
                        state = DeploymentState.DEPLOYED;
                }
            }
        }

        protected virtual void Update()
        {
            if(HighLogic.LoadedSceneIsEditor)
            {
                if(just_started)
                {
                    update_model(false);
                    just_started = false;
                }
            }
            show_deploy_hint();
        }

        [KSPEvent(guiName = "Show Deployment Hint", guiActive = true, guiActiveEditor = true,
                  guiActiveUncommand = true, guiActiveUnfocused = true, unfocusedRange = 300,
                  active = true)]
        public void ShowDeploymentHintEvent() => ShowDeployHint = !ShowDeployHint;

        #region Deployment
        protected abstract Vector3 get_deployed_size();
        protected abstract Transform get_deploy_transform(Vector3 size, out Vector3 spawn_offset);

        protected virtual void update_deploy_hint(Transform deployT,
            Vector3 deployed_size, Vector3 spawn_offset)
        {
            if(deployT != null)
            {
                var T = deploy_hint_mesh.gameObject.transform;
                T.position = deployT.position;
                T.rotation = deployT.rotation;
            }
        }

        protected void update_deploy_hint()
        {
            var size = get_deployed_size();
            if (!size.IsZero())
            {
                var deployT = get_deploy_transform(size, out var spawn_offset);
                update_deploy_hint(deployT, size, spawn_offset);
            }
        }

        protected virtual void show_deploy_hint(bool show) =>
            deploy_hint_mesh.gameObject.SetActive(show);

        protected void show_deploy_hint() =>
            show_deploy_hint(GroundConstructionScenario.ShowDeployHint || ShowDeployHint);

        protected Vector3 metric_to_part_scale =>
        Vector3.Scale(OrigPartSize, OrigSize.Inverse());

        protected Vector3 get_deployed_part_size() =>
        Vector3.Scale(get_deployed_size(), metric_to_part_scale);

        protected abstract Vector3 get_point_of_growth();

        protected Bounds get_deployed_part_bounds(bool to_model_space)
        {
            var T = get_deploy_transform(Vector3.zero, out _);
            if(T != null)
            {
                var depl_size = get_deployed_part_size();
                if (!depl_size.IsZero())
                {
                    var part_size = Vector3.Scale(Size, metric_to_part_scale).Local2LocalDir(model, T).AbsComponents();
                    var part_center = model.TransformPoint(PartCenter);
                    var growth_point = get_point_of_growth();
                    var scale = Vector3.Scale(depl_size, part_size.Inverse());
                    var growth = Vector3.Scale(T.InverseTransformDirection(part_center - growth_point), scale);
                    var center = T.InverseTransformPointUnscaled(growth_point+T.TransformDirection(growth));
                    if(to_model_space)
                        return new Bounds(center.Local2LocalDir(T, model),
                            depl_size.Local2LocalDir(T, model).AbsComponents());
                    return new Bounds(center, depl_size);    
                }
            }
            return default(Bounds);
        }

        protected virtual void create_deploy_hint_mesh()
        {
            var bounds = get_deployed_part_bounds(false);
            if(bounds.size.IsZero()) return;
            var corners = Utils.BoundCorners(bounds);
            var size = bounds.size;
            var mesh = deploy_hint_mesh.mesh;
            mesh.vertices = new[]{
                corners[0], //left-bottom-back
                corners[1], //left-bottom-front
                corners[2], //left-top-back
                corners[3], //left-top-front
                corners[4], //right-bottom-back
                corners[5], //right-bottom-front
                corners[6], //right-top-back
                corners[7], //right-top-front
                // front arrow
                corners[1]+Vector3.right*size.x/4,
                corners[1]+Vector3.right*size.x/2+Vector3.forward*size.z/2,
                corners[5]-Vector3.right*size.x/4,
            };
            mesh.triangles = new[] {
                0, 1, 2, 2, 1, 3, //left
                3, 1, 7, 7, 1, 5, //front
                5, 4, 7, 7, 4, 6, //right
                6, 4, 2, 2, 4, 0, //back
                2, 6, 3, 3, 6, 7, //top
                0, 4, 1, 1, 4, 5, //bottom
                8, 9, 10 //arrow
            };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            update_deploy_hint();
        }

        protected abstract bool can_deploy();

        bool same_vessel_collision_if_deployed()
        {
            var B = get_deployed_part_bounds(true);
            for(int i = 0, vesselPartsCount = vessel.Parts.Count; i < vesselPartsCount; i++)
            {
                var p = vessel.Parts[i];
                if(p == part) continue;
                if(p.parent == part || part.parent == p) continue;
                var pM = new Metric(p, true, true);
                for(int j = 0, pMhullPointsCount = pM.hull.Points.Count; j < pMhullPointsCount; j++)
                {
                    var c = pM.hull.Points[j];
                    var lc = model.InverseTransformDirection(c - model.position);
                    if(B.Contains(lc))
                    {
                        p.HighlightAlways(Colors.Danger.color);
                        StartCoroutine(CallbackUtil.DelayedCallback(3f, () => p?.SetHighlightDefault()));
                        ShowDeployHint = true;
                        return true;
                    }
                }
            }
            return false;
        }

        protected virtual IEnumerable prepare_deployment()
        {
            yield return null;
        }

        protected abstract IEnumerable finalize_deployment();

        IEnumerator<YieldInstruction> deploy()
        {
            foreach(var _ in prepare_deployment()) yield return null;
            var deployT = get_deploy_transform(Vector3.zero, out _);
            TargetSize = get_deployed_size();
            TargetSize = TargetSize.Local2LocalDir(deployT, model).AbsComponents();
            yield return slow_resize();
            foreach(var _ in finalize_deployment()) yield return null;
            state = DeploymentState.DEPLOYED;
        }

        protected void start_deployment()
        {
            Utils.SaveGame(Name + "-before_deployment");
            state = DeploymentState.DEPLOYING;
            StartCoroutine(deploy());
        }

        public virtual void Deploy()
        {
            if(can_deploy())
            {
                if(same_vessel_collision_if_deployed())
                {
                    warning.Show(Name + Colors.Warning.Tag(" <b>will intersect other parts of the vessel</b>") +
                        " if deployed.\nYou may proceed with the deployment if you are sure the constructed vessel " +
                        "will not collide with anything when launched.\n" +
                        Colors.Danger.Tag("Start the deployment?"),
                    () => warning.Show(true));
                }
                else
                    warning.Show(true);
            }
        }
        #endregion

        protected void update_unfocusedRange(params string[] events)
        {
            var range = Size.magnitude + 1;
            for(int i = 0, len = events.Length; i < len; i++)
            {
                var ename = events[i];
                var evt = Events[ename];
                if(evt == null) continue;
                evt.unfocusedRange = range;
            }
        }
    }
}

