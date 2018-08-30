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

namespace GroundConstruction
{
    public abstract class DeployableModel : SerializableFiledsPartModule, IDeployable
    {
        protected static Globals GLB { get { return Globals.Instance; } }

        protected Transform model;

        protected MeshFilter deploy_hint_mesh;
        protected static readonly Color deploy_hint_color = new Color(0, 1, 0, 0.25f);
        
        [KSPField] public string MetricMesh = string.Empty;
        [KSPField] public Vector3 MinSize = new Vector3(0.5f, 0.5f, 0.5f);

        public Vector3 OrigScale;
        public Vector3 OrigSize;
        public Vector3 OrigPartSize;
        [KSPField(isPersistant = true)] public Vector3 Size;
        [KSPField(isPersistant = true)] public Vector3 TargetSize;
        [KSPField(isPersistant = true)] public DeplyomentState state;
        bool just_started;

        public DeplyomentState State { get { return state; } }

        void update_attach_node(AttachNode node, Vector3 scale, float scale_quad, HashSet<Part> updated_parts, bool update_parts)
        {
            node.position = Vector3.Scale(node.originalPosition, scale);
            //update node breaking forces
            node.breakingForce *= scale_quad;
            node.breakingTorque *= scale_quad;
            //move parts
            if(update_parts && node.attachedPart != null)
            {
                this.Log("Updating attach node: {}, attached {}: {}", 
                         node.id, node.attachedPartId, node.attachedPart);//debug
                part.UpdateAttachedPartPos(node, true);
                updated_parts.Add(node.attachedPart);
            }
        }

        void update_other_node(AttachNode other, Vector3 rel_scale, HashSet<Part> updated_parts)
        {
            var cur_pos = part.transform
                              .InverseTransformPoint(other.owner.transform.position 
                                                     + other.owner.transform.TransformDirection(other.position));
            var new_pos = Vector3.Scale(cur_pos, rel_scale);
            other.owner.Log("Updating node {}, delta {}", other.id, new_pos - cur_pos);//debug
            part.UpdateAttachedPartPosProportional(other.owner, part.transform.TransformDirection(new_pos - cur_pos));
            updated_parts.Add(other.owner);
        }

        void update_docked_part(Part docked, Vector3 rel_scale, HashSet<Part> updated_parts)
        {
            PartJoint j = null;
            if(docked == part.parent && part.attachJoint != null)
                j = part.attachJoint;
            else if(docked.parent == part && docked.attachJoint != null)
                j = docked.attachJoint;
            if(j != null)
            {
                var cur_pos = j.Host == part? 
                           j.HostAnchor : j.HostAnchor.Local2Local(j.Host.transform, part.transform);
                var new_pos = Vector3.Scale(cur_pos, rel_scale);
                docked.Log("Updating docked, delta {}", new_pos - cur_pos);//debug
                part.UpdateAttachedPartPosProportional(docked, part.transform.TransformDirection(new_pos - cur_pos));
                updated_parts.Add(docked);
            }
        }

        protected void update_model(bool update_parts)
        {
            //rescale part
            var scale = Vector3.Scale(Size, OrigSize.Inverse());
            var local_scale = Vector3.Scale(OrigScale, scale);
            var rel_scale = Vector3.Scale(local_scale, model.localScale.Inverse());
            model.localScale = local_scale;
            model.hasChanged = true;
            part.transform.hasChanged = true;
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
                    update_docked_part(part.parent, rel_scale, updated_parts);
                for(int i = 0, count = part.children.Count; i < count; i++)
                {
                    var child = part.children[i];
                    //surface attached children
                    if(child.srfAttachNode != null && child.srfAttachNode.attachedPart == part)
                        update_other_node(child.srfAttachNode, rel_scale, updated_parts);
                    //docked children
                    else if(!updated_parts.Contains(child))
                        update_docked_part(child, rel_scale, updated_parts);
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

        static readonly int scenery_mask = (1 << LayerMask.NameToLayer("Local Scenery"));
        IEnumerator<YieldInstruction> resize_coro;
        IEnumerator<YieldInstruction> _resize()
        {
            if(Size == TargetSize)
                yield break;
            var start = Size;
            var time = 0f;
            var speed = Mathf.Min(GLB.DeploymentSpeed / Mathf.Abs((TargetSize-Size).MaxComponentF()),
                                  1 / GLB.MinDeploymentTime);
            var up = vessel.up;
            if(vessel.LandedOrSplashed)
            {
                RaycastHit hit;
                if(Physics.Raycast(part.partTransform.position, -up, out hit, 
                                   Mathf.Max(vessel.heightFromTerrain*2, 1), scenery_mask))
                    up = hit.normal;
            }
            var restore_joints = new List<Part>();
            if(part.parent != null && part.attachJoint != null)
            {
                part.Log("attachJoint: hostA {}, targetA {}", part.attachJoint.HostAnchor, part.attachJoint.TgtAnchor);
                part.attachJoint.DestroyJoint();
                part.ResetJoints();
                restore_joints.Add(part);
            }
            foreach(var child in part.children)
            {
                if(child.attachJoint != null)
                {
                    child.attachJoint.DestroyJoint();
                    child.ResetJoints();
                    restore_joints.Add(child);
                }
            }
            GameEvents.onActiveJointNeedUpdate.Fire(vessel);
            while(time <= 1)
            {
                var old_size = Size;
                time += speed * TimeWarp.fixedDeltaTime;
                Size = Vector3.Lerp(start, TargetSize, time);
                FlightGlobals.overrideOrbit = true;
                update_model(true);
                if(vessel.LandedOrSplashed && part.GroundContact)
                    vessel.SetPosition(vessel.vesselTransform.position+up*(Size-old_size).y);
                //this.Log("deployment time: {}, size {}", time, Size);//debug
                yield return new WaitForFixedUpdate();
                if(restore_joints.Count > 0)
                    yield return new WaitForFixedUpdate();
            }
            restore_joints.ForEach(p => { p.CreateAttachJoint(p.attachMode); p.ResetJoints(); });
            if(part.attachJoint != null)
                part.Log("attachJoint hostA {}, targetA {}", part.attachJoint.HostAnchor, part.attachJoint.TgtAnchor);
            GameEvents.onActiveJointNeedUpdate.Fire(vessel);
            yield return new WaitForFixedUpdate();
            FlightGlobals.overrideOrbit = false;
            Size = TargetSize;
        }

        public override void OnAwake()
        {
            base.OnAwake();
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
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            just_started = true;
            create_deploy_hint_mesh();
            if(State == DeplyomentState.DEPLOYING)
                StartCoroutine(deploy());
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if(Size.IsZero())
                Size = MinSize;
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
                this.Log("metric {}\npart metric {}", metric, part_metric);//debug
                if(metric.Empty)
                    metric = part_metric;
                OrigSize = metric.size;
                OrigScale = model.localScale;
                OrigPartSize = part_metric.size;
            }
            update_model(false);
            //deprecated config conversion
            if(node.HasValue("Deploying"))
            {
                state = DeplyomentState.IDLE;
                var val = node.GetValue("Deploying");
                if(bool.TryParse(val, out bool _deploy) && _deploy)
                    state = DeplyomentState.DEPLOYING;
                else
                {
                    val = node.GetValue("Deployed");
                    if(bool.TryParse(val, out _deploy) && _deploy)
                        state = DeplyomentState.DEPLOYED;
                }
            }
            this.Log("OnLoad: Size {}, OrigSize {}, PartSize {}, OrigModelScale {}", 
                     Size, OrigSize, OrigPartSize, OrigScale);//debug
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
            if(HighLogic.LoadedSceneIsFlight)
            {
                if(GroundConstructionScenario.ShowDeployHint)
                    update_deploy_hint();
                else
                    deploy_hint_mesh.gameObject.SetActive(false);
            }
        }

        #region Deployment
        protected abstract Vector3 get_deployed_size();
        protected abstract Vector3 get_deployed_offset();
        protected abstract Transform get_deploy_transform();

        protected virtual void update_deploy_hint()
        {
            var T = get_deploy_transform();
            if(T != null)
            {
                var fwd_T = deploy_hint_mesh.gameObject.transform;
                fwd_T.position = T.position+T.TransformDirection(get_deployed_offset());
                fwd_T.rotation = T.rotation;
                deploy_hint_mesh.gameObject.SetActive(true);
            }
        }

        protected virtual void create_deploy_hint_mesh()
        {
            var scale = Vector3.Scale(OrigPartSize, OrigSize.Inverse());
            var size = Vector3.Scale(get_deployed_size(), scale)/2;
            var mesh = deploy_hint_mesh.mesh;
            mesh.vertices = new[]{
                // bottom
                -Vector3.right*size.x-Vector3.forward*size.z, // 0 - lb
                -Vector3.right*size.x+Vector3.forward*size.z, // 1 - lf
                Vector3.right*size.x+Vector3.forward*size.z,  // 2 - rf
                Vector3.right*size.x-Vector3.forward*size.z,  // 3 - rb
                // front arrow
                -Vector3.right*size.x/4+Vector3.forward*size.z,
                Vector3.forward*size.z*1.5f,
                Vector3.right*size.x/4+Vector3.forward*size.z,
                // top
                -Vector3.right*size.x-Vector3.forward*size.z+Vector3.up*size.y*2, // 7  - lbt
                -Vector3.right*size.x+Vector3.forward*size.z+Vector3.up*size.y*2, // 8  - lft
                Vector3.right*size.x+Vector3.forward*size.z+Vector3.up*size.y*2,  // 9  - rft
                Vector3.right*size.x-Vector3.forward*size.z+Vector3.up*size.y*2   // 10 - rbt
            };
            mesh.triangles = new[] { 
                0, 1, 2, 2, 3, 0, 
                4, 5, 6, 
                0, 7, 1, 1, 7, 8,
                1, 8, 2, 2, 8, 9,
                2, 9, 3, 3, 9, 10,
                3, 10, 0, 0, 10, 7,
                7, 8, 9, 9, 10, 7
            };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        public abstract string Name { get; }

        protected abstract bool can_deploy();

        protected virtual IEnumerable prepare_deployment()
        {
            part.BreakConnectedCompoundParts();
            yield return null;
        }

        protected abstract IEnumerable finalize_deployment();

        IEnumerator<YieldInstruction> deploy()
        {
            foreach(var _ in prepare_deployment()) yield return null;
            var deployT = get_deploy_transform();
            TargetSize = get_deployed_size();
            TargetSize = TargetSize.Local2LocalDir(deployT, model).AbsComponents();
            yield return slow_resize();
            foreach(var _ in finalize_deployment()) yield return null;
            state = DeplyomentState.DEPLOYED;
        }

        public virtual void Deploy()
        {
            if(!can_deploy()) return;
            Utils.SaveGame(Name + "-before_deployment");
            state = DeplyomentState.DEPLOYING;
            StartCoroutine(deploy());
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

