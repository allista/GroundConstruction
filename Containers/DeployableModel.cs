﻿//   DeployableModel.cs
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
    public abstract class DeployableModel : SerializableFiledsPartModule, IDeployable
    {
        protected static Globals GLB { get { return Globals.Instance; } }

        protected Transform model;

        protected MeshFilter deploy_hint_mesh;
        protected static readonly Color deploy_hint_color = new Color(0, 1, 0, 0.25f);
        
        [KSPField(isPersistant = true)] public Vector3 OrigScale;
        [KSPField(isPersistant = true)] public Vector3 OrigSize;
        [KSPField(isPersistant = true)] public Vector3 Size;

        [KSPField(isPersistant = true)] public float DeploymentTime;
        [KSPField(isPersistant = true)] public float DeployingSpeed;

        [KSPField(isPersistant = true)] public DeplyomentState state;

        public DeplyomentState State { get { return state; } }

        protected void update_model(bool initial)
        {
            //rescale part
            var scale = Vector3.Scale(Size, OrigSize.Inverse());
            var local_scale = Vector3.Scale(OrigScale, scale);
            var rel_scale = Vector3.Scale(local_scale, model.localScale.Inverse());
            model.localScale = local_scale;
            model.hasChanged = true;
            part.transform.hasChanged = true;
            //update attach nodes and attached parts
            var scale_quad = rel_scale.sqrMagnitude;
            for(int i = 0, count = part.attachNodes.Count; i < count; i++)
            {
                //update node position
                var node = part.attachNodes[i];
                node.position = Vector3.Scale(node.originalPosition, scale);
                part.UpdateAttachedPartPos(node);
                //update node breaking forces
                node.breakingForce *= scale_quad;
                node.breakingTorque *= scale_quad;
            }
            //update this surface attach node
            if(part.srfAttachNode != null)
            {
                Vector3 old_position = part.srfAttachNode.position;
                part.srfAttachNode.position = Vector3.Scale(part.srfAttachNode.originalPosition, scale);
                //don't move the part at start, its position is persistant
                if(!initial)
                {
                    Vector3 d_pos = part.transform.TransformDirection(part.srfAttachNode.position - old_position);
                    part.transform.position -= d_pos;
                }
            }
            //no need to update surface attached parts on start
            //as their positions are persistant; less calculations
            if(initial) return;
            //update parts that are surface attached to this
            for(int i = 0, count = part.children.Count; i < count; i++)
            {
                var child = part.children[i];
                if(child.srfAttachNode != null && child.srfAttachNode.attachedPart == part)
                {
                    Vector3 attachedPosition = child.transform.localPosition + child.transform.localRotation * child.srfAttachNode.position;
                    Vector3 targetPosition = Vector3.Scale(attachedPosition, rel_scale);
                    child.transform.Translate(targetPosition - attachedPosition, part.transform);
                }
            }
        }

        public override void OnAwake()
        {
            base.OnAwake();
            //get the original model, its size and localScale
            model = part.transform.Find("model");
            var metric = new Metric(part);
            OrigSize = metric.size;
            OrigScale = model.localScale;
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
            create_deploy_hint_mesh();
            if(State == DeplyomentState.DEPLOYING)
                StartCoroutine(deploy());
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if(!Size.IsZero())
                update_model(true);
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
        }

        protected virtual void Update()
        {
            if(HighLogic.LoadedSceneIsEditor)
            {
                if(!Size.IsZero() && model.localScale == OrigScale)
                    update_model(true);
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
        protected abstract Transform get_deploy_transform();

        protected virtual void update_deploy_hint()
        {
            var T = get_deploy_transform();
            if(T != null)
            {
                var fwd_T = deploy_hint_mesh.gameObject.transform;
                fwd_T.position = T.position;
                fwd_T.rotation = T.rotation;
                deploy_hint_mesh.gameObject.SetActive(true);
            }
        }

        protected virtual void create_deploy_hint_mesh()
        {
            var size = get_deployed_size();
            var mesh = deploy_hint_mesh.mesh;
            mesh.vertices = new[]{
                // bottom
                -Vector3.right*size.x-Vector3.forward*size.z,
                -Vector3.right*size.x+Vector3.forward*size.z,
                Vector3.right*size.x+Vector3.forward*size.z,
                Vector3.right*size.x-Vector3.forward*size.z,
                // front arrow
                -Vector3.right*size.x/4+Vector3.forward*size.z,
                Vector3.forward*size.z*1.5f,
                Vector3.right*size.x/4+Vector3.forward*size.z
                //// front
                //            -Vector3.right*size.x+Vector3.forward*size.z,
                //            Vector3.right*size.x+Vector3.forward*size.z,
                //// right
                //            Vector3.right*size.x+Vector3.forward*size.z,
                //            Vector3.right*size.x-Vector3.forward*size.z,
                //// back
                //            -Vector3.right*size.x-Vector3.forward*size.z,
                //            Vector3.right*size.x-Vector3.forward*size.z,
                //// left
                //            -Vector3.right*size.x-Vector3.forward*size.z,
                //            -Vector3.right*size.x+Vector3.forward*size.z,
                //// top
                //-Vector3.right*size.x-Vector3.forward*size.z,
                //-Vector3.right*size.x+Vector3.forward*size.z,
                //Vector3.right*size.x+Vector3.forward*size.z,
                //Vector3.right*size.x-Vector3.forward*size.z,
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 3, 0, 4, 5, 6 };
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

        ActionDamper message_damper = new ActionDamper(1);
        IEnumerator<YieldInstruction> deploy()
        {
            foreach(var _ in prepare_deployment()) yield return null;
            var deployT = get_deploy_transform();
            var start = Size;
            var start_time = DeploymentTime;
            var time_span = 1-start_time;
            var end = get_deployed_size();
            end = model.InverseTransformDirection(deployT.TransformDirection(end)).AbsComponents();
            //resize the kit gradually
            var rot = vessel.vesselTransform.rotation;
            while(DeploymentTime <= 1)
            {
                DeploymentTime += DeployingSpeed * TimeWarp.fixedDeltaTime;
                var old_size = Size;
                Size = Vector3.Lerp(start, end, (DeploymentTime - start_time)/time_span);
                update_model(false);
                FlightGlobals.overrideOrbit = true;
                vessel.SetRotation(rot);
                vessel.SetPosition(vessel.vesselTransform.position+deployT.up*(Size-old_size).y);
                vessel.IgnoreGForces(10);
                yield return new WaitForFixedUpdate();
            }
            FlightGlobals.overrideOrbit = false;
            DeploymentTime = 1;
            Size = end;
            foreach(var _ in finalize_deployment()) yield return null;
            state = DeplyomentState.DEPLOYED;
        }

        public virtual void Deploy()
        {
            if(!can_deploy()) return;
            DeployingSpeed = Mathf.Min(GLB.DeploymentSpeed / get_deployed_size().MaxComponentF(), 1 / GLB.MinDeploymentTime);
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

