//   ModuleConstructionKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using AT_Utils;

namespace GroundConstruction
{
    public partial class ModuleConstructionKit : PartModule, IPartCostModifier, IPartMassModifier, IDeployableContainer
    {
        static Globals GLB { get { return Globals.Instance; } }

        Transform model;
        List<Transform> spawn_transforms;
        [KSPField] public string SpawnTransforms;

        MeshFilter deploy_hint_mesh;
        static readonly Color deploy_hint_color = new Color(0, 1, 0, 0.25f);

        TextureSwitcherServer texture_switcher;
        [KSPField] public string TextureVAB;
        [KSPField] public string TextureSPH;
        [KSPField(isPersistant = true)] public EditorFacility Facility;

        [KSPField(isPersistant = true)] public Vector3 OrigScale;
        [KSPField(isPersistant = true)] public Vector3 OrigSize;
        [KSPField(isPersistant = true)] public Vector3 Size;

        [KSPField(isPersistant = true)] public float DeploymentTime;
        [KSPField(isPersistant = true)] public float DeployingSpeed;

        [KSPField(isPersistant = true)] public ContainerDeplyomentState state;

        [KSPField(guiName = "Kit", guiActive = true, guiActiveEditor = true, isPersistant = true)]
        public string KitName = "None";
        SimpleTextEntry kitname_editor;
        ShipConstructLoader construct_loader;

        [KSPField(guiName = "Kit Mass", guiActive = true, guiActiveEditor = true, guiFormat = "0.0 t")]
        public float KitMass;

        [KSPField(guiName = "Kit Cost", guiActive = true, guiActiveEditor = true, guiFormat = "0.0 F")]
        public float KitCost;

        [KSPField(guiName = "Work required", guiActive = true, guiActiveEditor = true, guiFormat = "0.0 SKH")]
        public float KitWork;

        [KSPField(guiName = "Resources required", guiActive = true, guiActiveEditor = true, guiFormat = "0.0 u")]
        public float KitRes;

        [KSPField(isPersistant = true)] public VesselKit kit = new VesselKit();

        public VesselKit GetKit(Guid id) { return kit.id == id ? kit : null; }

        public List<VesselKit> GetKits() { return new List<VesselKit> { kit }; }

        public ContainerDeplyomentState State { get { return state; } }

        #region Anchor
        FixedJoint anchorJoint;
        GameObject anchor;

        void setup_ground_contact()
        {
            part.PermanentGroundContact = true;
            if(vessel != null) vessel.permanentGroundContact = true;
        }

        void dump_velocity()
        {
            if(vessel == null || !vessel.loaded) return;
            for(int i = 0, nparts = vessel.parts.Count; i < nparts; i++)
            {
                var r = vessel.parts[i].Rigidbody;
                if(r == null) continue;
                r.angularVelocity *= 0;
                r.velocity *= 0;
            }
        }

        void attach_anchor()
        {
            detach_anchor();
            dump_velocity();
            anchor = new GameObject("AnchorBody");
            var rb = anchor.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            anchor.transform.position = part.transform.position;
            anchor.transform.rotation = part.transform.rotation;
            anchorJoint = anchor.AddComponent<FixedJoint>();
            anchorJoint.breakForce = float.PositiveInfinity;
            anchorJoint.breakTorque = float.PositiveInfinity;
            anchorJoint.connectedBody = part.Rigidbody;
        }

        void detach_anchor()
        {
            if(anchor) Destroy(anchor);
            if(anchorJoint) Destroy(anchorJoint);
        }
        #endregion

        void update_texture()
        {
            if(texture_switcher == null ||
               Facility == EditorFacility.None) return;
            texture_switcher.SetTexture(Facility == EditorFacility.VAB ?
                                        TextureVAB : TextureSPH);
        }

        void update_part_info()
        {
            if(kit.Valid)
            {
                KitName = kit.Name;
                KitMass = kit.Mass;
                KitCost = kit.Cost;
                var rem = kit.RemainingRequirements();
				KitWork = (float)rem.work/3600;
                KitRes  = (float)rem.resource_amount;
            }
            else
            {
                KitName = "None";
                KitMass = 0;
                KitCost = 0;
                KitWork = 0;
                KitRes  = 0;
            }
        }

        void update_model(bool initial)
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

        Transform get_spawn_transform()
        {
            Transform minT = null;
            var alt = double.MaxValue;
            foreach(var T in spawn_transforms)
            {
                var t_alt = vessel.mainBody.GetAltitude(T.position) - vessel.mainBody.TerrainAltitude(T.position);
                if(t_alt < alt) { alt = t_alt; minT = T; }
            }
            return minT;
        }

        public override void OnAwake()
        {
            base.OnAwake();
            kitname_editor = gameObject.AddComponent<SimpleTextEntry>();
            construct_loader = gameObject.AddComponent<ShipConstructLoader>();
            construct_loader.process_construct = store_construct;
            //model = part.transform.Find("model");
            var obj = new GameObject("SpawnTransformFwdMesh", typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(part.transform);
            deploy_hint_mesh = obj.GetComponent<MeshFilter>();
            deploy_hint_mesh.mesh = new Mesh();
            var fwd_renderer = obj.GetComponent<MeshRenderer>();
            fwd_renderer.material = Utils.no_z_material;
            fwd_renderer.material.color = deploy_hint_color;
            fwd_renderer.enabled = true;
            obj.SetActive(false);
        }

        void OnDestroy()
        {
            detach_anchor();
            Destroy(deploy_hint_mesh.gameObject);
            Destroy(kitname_editor);
            Destroy(construct_loader);
        }

        void create_fwd_mesh()
        {
            var size = kit.ShipMetric.extents;
            var mesh = deploy_hint_mesh.mesh;
            mesh.vertices = new[]{
                -Vector3.right*size.x-Vector3.forward*size.z,
                -Vector3.right*size.x+Vector3.forward*size.z,
                Vector3.right*size.x+Vector3.forward*size.z,
                Vector3.right*size.x-Vector3.forward*size.z,

                -Vector3.right*size.x/4+Vector3.forward*size.z,
                Vector3.forward*size.z*1.5f,
                Vector3.right*size.x/4+Vector3.forward*size.z
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 3, 0, 4, 5, 6 };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        void update_deploy_hint()
        {
            deploy_hint_mesh.gameObject.SetActive(false);
            if(GroundConstructionScenario.ShowDeployHint)
            {
                var T = get_spawn_transform();
                if(T != null)
                {
                    var fwd_T = deploy_hint_mesh.gameObject.transform;
                    fwd_T.position = T.position;
                    fwd_T.rotation = T.rotation;
                    deploy_hint_mesh.gameObject.SetActive(true);
                }
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if(State == ContainerDeplyomentState.DEPLOYED) setup_ground_contact();
            Events["Deploy"].active = kit.Valid && State == ContainerDeplyomentState.IDLE;
            Events["Launch"].active = kit.Valid && State == ContainerDeplyomentState.DEPLOYED && kit.Complete;
            update_unfocusedRange("Deploy", "Launch");
            setup_constraint_fields();
            create_fwd_mesh();
            spawn_transforms = new List<Transform>();
            if(!string.IsNullOrEmpty(SpawnTransforms))
            {
                foreach(var t in Utils.ParseLine(SpawnTransforms, Utils.Whitespace))
                {
                    var transforms = part.FindModelTransforms(t);
                    if(transforms == null || transforms.Length == 0) continue;
                    spawn_transforms.AddRange(transforms);
                }
            }
            if(!string.IsNullOrEmpty(TextureVAB) && !string.IsNullOrEmpty(TextureSPH))
                texture_switcher = part.Modules.GetModule<TextureSwitcherServer>();
            StartCoroutine(Utils.SlowUpdate(update_part_info, 0.5f));
        }

        void OnPartPack() { detach_anchor(); }
        void OnPartUnpack()
        {
            if(state == ContainerDeplyomentState.DEPLOYED)
            {
                attach_anchor();
                setup_ground_contact();
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            var metric = new Metric(part);
            model = part.transform.Find("model");
            OrigSize = metric.size;
            OrigScale = model.localScale;
            kit.Host = this;
            if(kit.Valid)
            {
                update_model(true);
                update_part_info();
                if(KitName == "None")
                    KitName = kit.Name;
            }
            //deprecated config conversion
            if(node.HasValue("Deploying"))
            {
                state = ContainerDeplyomentState.IDLE;
                var val = node.GetValue("Deploying");
                if(bool.TryParse(val, out bool _deploy) && _deploy)
                    state = ContainerDeplyomentState.DEPLOYING;
                else
                {
                    val = node.GetValue("Deployed");
                    if(bool.TryParse(val, out _deploy) && _deploy)
                        state = ContainerDeplyomentState.DEPLOYED;
                }
            }
        }

        void Update()
        {
            if(HighLogic.LoadedSceneIsEditor && kit.Valid &&
               model.localScale == OrigScale)
                update_model(true);
            if(HighLogic.LoadedSceneIsFlight)
                update_deploy_hint();
            if(state == ContainerDeplyomentState.DEPLOYED)
            {
                setup_ground_contact();
                if(!anchor || !anchorJoint || !anchor.GetComponent<FixedJoint>())
                    attach_anchor();
                else dump_velocity();
            }
            else if(state == ContainerDeplyomentState.DEPLOYING)
            {
                if(deployment == null) deployment = deploy();
                if(!deployment.MoveNext()) deployment = null;
            }
        }

        #region Select Ship Construct
        [KSPEvent(guiName = "Select Vessel", guiActive = false, guiActiveEditor = true, active = true)]
        public void SelectVessel()
        {
            construct_loader.SelectVessel();
        }

        [KSPEvent(guiName = "Select Subassembly", guiActive = false, guiActiveEditor = true, active = true)]
        public void SelectSubassembly()
        {
            construct_loader.SelectSubassembly();
        }

        void store_construct(ShipConstruct construct)
        {
            kit = new VesselKit(this, construct);
            Facility = construct.shipFacility;
            update_part_info();
            update_texture();
            set_kit_size();
            update_constraint_controls();
            construct.Unload();
            this.Log("mass {}, cost {}, name {}, kit {}",
                     kit.Mass, kit.Cost, kit.Name, kit);//debug
        }
        #endregion

        #region Deployment
        bool can_deploy()
        {
            if(!kit.Valid)
            {
                Utils.Message("Cannot deploy: construction kit is empty.");
                return false;
            }
            if(vessel.packed)
            {
                Utils.Message("Cannot deploy a packed construction kit.");
                return false;
            }
            if(!vessel.Landed)
            {
                Utils.Message("Cannot deploy construction kit unless landed.");
                return false;
            }
            if(vessel.srfSpeed > GLB.DeployMaxSpeed)
            {
                Utils.Message("Cannot deploy construction kit while mooving.");
                return false;
            }
            if(vessel.angularVelocity.sqrMagnitude > GLB.DeployMaxAV)
            {
                Utils.Message("Cannot deploy construction kit while rotating.");
                return false;
            }
            return true;
        }

        IEnumerator decouple_attached_parts()
        {
            if(part.parent) part.decouple();
            yield return null;
            while(part.children.Count > 0)
            {
                part.children[0].decouple();
                yield return null;
            }
        }

        bool kit_is_settled
        {
            get
            {
                return vessel.srfSpeed < GLB.DeployMaxSpeed &&
                    vessel.angularVelocity.sqrMagnitude < GLB.DeployMaxAV;
            }
        }

        public bool Empty => kit;

        RealTimer settled_timer = new RealTimer(3);
        IEnumerable wait_for_ground_contact(string wait_message)
        {
            settled_timer.Reset();
            while(!settled_timer.RunIf(part.GroundContact && kit_is_settled))
            {
                if(!part.GroundContact)
                    message_damper.Run(() => Utils.Message(1, "{0} Kit: no ground contact!", kit.Name));
                else if(!kit_is_settled)
                    message_damper.Run(() => Utils.Message(1, "{0} Kit is moving...", kit.Name));
                else message_damper.Run(() => Utils.Message(1, "{0} {1:F1}s", wait_message, settled_timer.Remaining));
                yield return null;
            }
        }

        ActionDamper message_damper = new ActionDamper(1);
        IEnumerator deployment;
        IEnumerator deploy()
        {
            //decouple anything that is still attached to the Kit
            var decoupler = decouple_attached_parts();
            while(decoupler.MoveNext())
                yield return decoupler.Current;
            //check if the kit has GroundContact and is not mooving
            foreach(object w in wait_for_ground_contact(string.Format("Deploing {0} Kit in", kit.Name)))
                yield return w;
            //get the spawn transform and compute the resizing path
            var spawnT = get_spawn_transform() ?? part.transform;
            yield return null;
            var start = Size;
            var start_time = DeploymentTime;
            var start_local_size = Vector3.Scale(OrigScale, OrigSize.Inverse());
            var end = kit.ShipMetric.size;
            if(Facility == EditorFacility.SPH) end = new Vector3(end.x, end.z, end.y);
            end = model.InverseTransformDirection(spawnT.TransformDirection(end)).AbsComponents();
            //resize the kit gradually
            while(DeploymentTime < 1)
            {
                DeploymentTime += DeployingSpeed * TimeWarp.deltaTime;
                Size = Vector3.Lerp(start, end, DeploymentTime - start_time);
                model.localScale = Vector3.Scale(Size, start_local_size);
                model.hasChanged = true;
                part.transform.hasChanged = true;
                yield return null;
            }
            DeploymentTime = 1;
            Size = end;
            //setup anchor, permanent ground contact and unfocused ranges
            update_unfocusedRange("Launch");
            setup_ground_contact();
            foreach(object w in wait_for_ground_contact(string.Format("Fixing {0} Kit in", kit.Name)))
                yield return w;
            attach_anchor();
            Utils.Message(6, "{0} is deployed and fixed to the ground.", vessel.vesselName);
            state = ContainerDeplyomentState.DEPLOYED;
        }

        [KSPEvent(guiName = "Deploy",
#if DEBUG
                  guiActive = true,
#endif
                  guiActiveUnfocused = true, unfocusedRange = 10, active = true)]
        public void Deploy()
        {
            if(!can_deploy()) return;
            Events["Deploy"].active = false;
            DeployingSpeed = Mathf.Min(GLB.DeploymentSpeed / kit.ShipMetric.volume, 1 / GLB.MinDeploymentTime);
            Utils.SaveGame(kit.Name + "-before_deployment");
            state = ContainerDeplyomentState.DEPLOYING;
        }
        #endregion

        #region Launching
        [KSPEvent(guiName = "Launch",
#if DEBUG
                  guiActive = true,
#endif
                  guiActiveUnfocused = true, unfocusedRange = 10, active = false)]
        public void Launch()
        {
            if(!can_launch()) return;
            StartCoroutine(launch_complete_construct());
        }

        [KSPEvent(guiName = "Rename Kit", guiActive = true, guiActiveEditor = true,
                  guiActiveUnfocused = true, unfocusedRange = 10, active = true)]
        public void EditName()
        {
            kitname_editor.Text = KitName;
            kitname_editor.Toggle();
        }

        public void EnableLaunchControls(bool enable = true)
        {
            Events["Launch"].active = enable;
        }

        void update_unfocusedRange(params string[] events)
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

        bool can_launch()
        {
            if(launch_in_progress) return false;
            if(!kit.Valid)
            {
                Utils.Message("Nothing to launch: construction kit is empty.");
                return false;
            }
            if(vessel.packed)
            {
                Utils.Message("Cannot launch from a packed construction kit.");
                return false;
            }
            if(!vessel.Landed)
            {
                Utils.Message("Cannot launch constructed ship unless landed.");
                return false;
            }
            if(vessel.srfSpeed > GLB.DeployMaxSpeed)
            {
                Utils.Message("Cannot launch constructed ship while mooving.");
                return false;
            }
            if(vessel.angularVelocity.sqrMagnitude > GLB.DeployMaxAV)
            {
                Utils.Message("Cannot launch constructed ship while rotating.");
                return false;
            }
            if(!kit.Complete)
            {
                Utils.Message("The assembly is not complete yet.");
                return false;
            }
            return true;
        }

        public void PutShipToGround(ShipConstruct ship, Transform spawnPoint)
        {
            var partHeightQuery = new PartHeightQuery(float.MaxValue);
            int count = ship.parts.Count;
            for(int i = 0; i < count; i++)
            {
                var p = ship[i];
                partHeightQuery.lowestOnParts.Add(p, float.MaxValue);
                Collider[] componentsInChildren = p.GetComponentsInChildren<Collider>();
                int num = componentsInChildren.Length;
                for(int j = 0; j < num; j++)
                {
                    Collider collider = componentsInChildren[j];
                    if(collider.enabled && collider.gameObject.layer != 21)
                    {
                        partHeightQuery.lowestPoint = Mathf.Min(partHeightQuery.lowestPoint, collider.bounds.min.y);
                        partHeightQuery.lowestOnParts[p] = Mathf.Min(partHeightQuery.lowestOnParts[p], collider.bounds.min.y);
                    }
                }
            }
            for(int k = 0; k < count; k++)
                ship[k].SendMessage("OnPutToGround", partHeightQuery, SendMessageOptions.DontRequireReceiver);
            Utils.Log("putting ship to ground: " + partHeightQuery.lowestPoint);
            float angle;
            Vector3 axis;
            spawnPoint.rotation.ToAngleAxis(out angle, out axis);
            var root = ship.parts[0].localRoot.transform;
            var offset = spawnPoint.position;
            var CoG = ship.Bounds().center;
            offset -= new Vector3(CoG.x, partHeightQuery.lowestPoint, CoG.z);
            root.Translate(offset, Space.World);
            root.RotateAround(spawnPoint.position, axis, angle);
        }

        bool launch_in_progress;
        Vessel launched_vessel;
        IEnumerator<YieldInstruction> launch_complete_construct()
        {
            if(!HighLogic.LoadedSceneIsFlight) yield break;
            launch_in_progress = true;
            yield return null;
            while(!FlightGlobals.ready) yield return null;
            //check if all the parts were indeed constructed
            if(!kit.BlueprintComplete())
            {
                Utils.Message("Something whent wrong. Not all parts were properly constructed.");
                launch_in_progress = false;
                yield break;
            }
            //hide UI
            GameEvents.onHideUI.Fire();
            yield return null;
            //save the game
            Utils.SaveGame(kit.Name + "-before_launch");
            yield return null;
            //load ship construct and launch it
            var construct = kit.LoadConstruct();
            if(construct == null)
            {
                Utils.Log("PackedConstruct: unable to load ShipConstruct {}. " +
                          "This usually means that some parts are missing " +
                          "or some modules failed to initialize.", kit.Name);
                Utils.Message("Something whent wrong. Constructed ship cannot be launched.");
                GameEvents.onShowUI.Fire();
                launch_in_progress = false;
                yield break;
            }
            model.gameObject.SetActive(false);
            var launch_transform = get_spawn_transform();
            FlightCameraOverride.AnchorForSeconds(FlightCameraOverride.Mode.Hold, FlightGlobals.ActiveVessel.transform, 1);
            if(FlightGlobals.ready)
                FloatingOrigin.SetOffset(launch_transform.position);
            PutShipToGround(construct, launch_transform);
            ShipConstruction.AssembleForLaunch(construct,
                                               vessel.landedAt, vessel.displaylandedAt, part.flagURL,
                                               FlightDriver.FlightStateCache,
                                               new VesselCrewManifest());
            launched_vessel = FlightGlobals.Vessels[FlightGlobals.Vessels.Count - 1];
            StageManager.BeginFlight();
            while(!launched_vessel.loaded)
            {
                FlightCameraOverride.UpdateDurationSeconds(1);
                yield return new WaitForFixedUpdate();
            }
            FXMonger.Explode(part, part.partTransform.position, 0);
            while(launched_vessel.packed)
            {
                launched_vessel.precalc.isEasingGravity = true;
                launched_vessel.situation = Vessel.Situations.PRELAUNCH;
                stabilize_launched_vessel(0);
                FlightCameraOverride.UpdateDurationSeconds(1);
                yield return new WaitForFixedUpdate();
            }
            foreach(var n in stabilize_launched_vessel(GLB.EasingFrames))
            {
                FlightCameraOverride.UpdateDurationSeconds(1);
                yield return new WaitForFixedUpdate();
            }
            if(kit.CrewSource != null && kit.KitCrew != null && kit.KitCrew.Count > 0)
                CrewTransferBatch.moveCrew(kit.CrewSource, launched_vessel, kit.KitCrew);
            GameEvents.onShowUI.Fire();
            launch_in_progress = false;
            launched_vessel = null;
            vessel.Die();
        }

        void stabilize_launched_vessel(float mult)
        {
            launched_vessel.permanentGroundContact = true;
            for(int j = 0, nparts = launched_vessel.parts.Count; j < nparts; j++)
            {
                var p = launched_vessel.parts[j];
                var r = p.Rigidbody;
                r.angularVelocity *= mult;
                r.velocity *= mult;
            }
        }

        IEnumerable stabilize_launched_vessel(int frames)
        {
            if(launched_vessel == null) yield break;
            var step = 1f / frames;
            for(int i = 0; i < frames; i++)
            {
                stabilize_launched_vessel(step * i);
                yield return null;
            }
            launched_vessel.permanentGroundContact = false;
        }

        void OnGUI()
        {
            if(Event.current.type != EventType.Layout && 
               Event.current.type != EventType.Repaint) return;
            Styles.Init();
            if(launch_in_progress)
                GUI.Label(new Rect(Screen.width / 2 - 190, 30, 380, 70),
                          "<b><color=#FFD100><size=30>Launching. Please, wait...</size></color></b>",
                          Styles.rich_label);
            else
            {
                //load ship construct
                construct_loader.Draw();
                //rename the kit
                if(kitname_editor.Draw("Rename Kit") == SimpleDialog.Answer.Yes)
                    KitName = kit.Name = kitname_editor.Text;
            }
        }
        #endregion

        #region IPartCostModifier implementation
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        { return kit.Valid ? kit.Cost : 0; }

        public ModifierChangeWhen GetModuleCostChangeWhen()
        { return ModifierChangeWhen.CONSTANTLY; }
        #endregion

        #region IPartMassModifier implementation
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        { return kit.Valid ? kit.Mass : 0; }

        public ModifierChangeWhen GetModuleMassChangeWhen()
        { return ModifierChangeWhen.CONSTANTLY; }
        #endregion
#if DEBUG
        void OnRenderObject()
        {
            if(vessel == null) return;
            var T = get_spawn_transform();
            if(T != null)
            {
                Utils.GLVec(T.position, T.up, Color.green);
                Utils.GLVec(T.position, T.forward, Color.blue);
                Utils.GLVec(T.position, T.right, Color.red);
            }
            if(launched_vessel != null)
                Utils.GLDrawPoint(launched_vessel.vesselTransform.position, Color.magenta);
        }
#endif
    }
}

