//   DeployableKitContainer.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri

using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
    public abstract partial class DeployableKitContainer : DeployableModel, IConstructionSpace,
        IAssemblySpace, IControllable, IConfigurable
    {
        public enum YRotation
        {
            Forward,
            Left,
            Backward,
            Right
        };

        [KSPField(isPersistant = true)] public YRotation yRotation = YRotation.Forward;

        [KSPField(isPersistant = true)] public EditorFacility Facility;

        [KSPField(guiName = "Kit", guiActive = true, guiActiveEditor = true, isPersistant = true)]
        public string KitName = "None";

        protected SimpleTextEntry kitname_editor;
        protected SimpleScrollView resource_manifest_view;
        protected ShipConstructLoader construct_loader;
        protected VesselSpawner vessel_spawner;

        protected MeshFilter kit_hull_mesh;
        static readonly Color kit_hull_color = new Color { r = 0, g = 1, b = 1, a = 0.25f };

        [KSPField(guiName = "Kit Mass",
            guiActive = true,
            guiActiveEditor = true,
            guiFormat = "0.0 t")]
        public float KitMass;

        [KSPField(guiName = "Kit Cost",
            guiActive = true,
            guiActiveEditor = true,
            guiFormat = "0.0 F")]
        public float KitCost;

        [KSPField(guiName = "Work required",
            guiActive = true,
            guiActiveEditor = true,
            guiFormat = "0.0 SKH")]
        public float KitWork;

        [KSPField(guiName = "Resources required",
            guiActive = true,
            guiActiveEditor = true,
            guiFormat = "0.0 u")]
        public float KitRes;

        [KSPField(isPersistant = true)] public VesselKit kit = new VesselKit();

        public VesselKit GetKit(Guid id)
        {
            return kit.id == id ? kit : null;
        }

        public List<VesselKit> GetKits()
        {
            return new List<VesselKit> { kit };
        }

        protected virtual void update_part_info()
        {
            if(Empty)
            {
                KitName = "None";
                KitMass = 0;
                KitCost = 0;
                KitWork = 0;
                KitRes = 0;
            }
            else
            {
                KitName = kit.Name;
                KitMass = kit.Mass;
                KitCost = kit.Cost;
                var rem = kit.RemainingRequirements();
                KitWork = (float)rem.work / 3600;
                KitRes = (float)rem.resource_amount;
            }
            update_part_events();
        }

        public override void OnAwake()
        {
            base.OnAwake();
            //vessel spawner
            vessel_spawner = gameObject.AddComponent<VesselSpawner>();
            //add UI components
            kitname_editor = gameObject.AddComponent<SimpleTextEntry>();
            kitname_editor.yesCallback = () =>
            {
                if(kit)
                    KitName = kit.Name = kitname_editor.Text;
            };
            resource_manifest_view = gameObject.AddComponent<SimpleScrollView>();
            construct_loader = gameObject.AddComponent<ShipConstructLoader>();
            construct_loader.process_construct = store_construct;
            //add kit hull mesh
            var obj = new GameObject("KitHullMesh", typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(gameObject.transform);
            kit_hull_mesh = obj.GetComponent<MeshFilter>();
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.material = Utils.no_z_material;
            renderer.material.color = kit_hull_color;
            renderer.enabled = true;
            obj.SetActive(false);
        }

        protected override void OnDestroy()
        {
            Destroy(vessel_spawner);
            Destroy(deploy_hint_mesh.gameObject);
            Destroy(kitname_editor);
            Destroy(resource_manifest_view);
            Destroy(construct_loader);
            base.OnDestroy();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            vessel_spawner.Init(part);
            Events["DeployEvent"].active = !Empty && State == DeploymentState.IDLE;
            Events["LaunchEvent"].active =
                !Empty && State == DeploymentState.DEPLOYED && kit.Complete;
            update_unfocusedRange("Deploy", "Launch");
            setup_constraint_fields();
            StartCoroutine(Utils.SlowUpdate(update_part_info, 0.5f));
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            kit.Host = this;
            update_part_info();
            if(KitName == "None")
                KitName = kit.Name;
        }

        #region IAssemblySpace
        bool IAssemblySpace.Valid =>
            isEnabled && (Empty || !kit.StageComplete(DIYKit.ASSEMBLY)) && ValidAssemblySpace;

        protected virtual bool ValidAssemblySpace => true;

        public bool CheckKit(VesselKit kit, string part_name, out float kit2space_ratio)
        {
            kit2space_ratio = -1;
            if(kit && CanConstruct(kit))
            {
                kit2space_ratio = 1;
                return true;
            }
            return false;
        }

        public void SetKit(VesselKit kit, string part_name)
        {
            if(kit != null)
            {
                StoreKit(kit, true);
                kit.Host = this;
            }
            else
                RemoveKit(true);
        }

        public void SpawnKit()
        {
            if(!kit)
                return;
            if(!kit.StageComplete(DIYKit.ASSEMBLY))
            {
                Utils.Message("The kit is not yet assembled");
                return;
            }
        }
        #endregion

        [KSPEvent(guiName = "Rename Kit",
            guiActive = true,
            guiActiveEditor = true,
            guiActiveUncommand = true,
            guiActiveUnfocused = true,
            unfocusedRange = 300,
            active = true)]
        public void EditName()
        {
            if(kit)
            {
                kitname_editor.Text = kit.Name;
                kitname_editor.Toggle();
            }
        }

        [KSPEvent(guiName = "Show Required Resources",
            guiActive = true,
            guiActiveEditor = true,
            guiActiveUncommand = true,
            guiActiveUnfocused = true,
            unfocusedRange = 300,
            active = false)]
        public void ShowResources()
        {
            if(kit && kit.AdditionalResources.Count > 0)
            {
                resource_manifest_view.Title = kit.Name + " requires";
                resource_manifest_view.Toggle();
            }
        }

        #region Select Ship Construct
        public virtual bool CanConstruct(VesselKit kit) => true;

        [KSPEvent(guiName = "Select Vessel",
            guiActive = false,
            guiActiveEditor = true,
            active = true)]
        public void SelectVessel()
        {
            construct_loader.SelectVessel();
        }

        [KSPEvent(guiName = "Select Subassembly",
            guiActive = false,
            guiActiveEditor = true,
            active = true)]
        public void SelectSubassembly()
        {
            construct_loader.SelectSubassembly();
        }

        [KSPEvent(guiName = "Select Part",
            guiActive = false,
            guiActiveEditor = true,
            active = true)]
        public void SelectPart()
        {
            construct_loader.SelectPart(part.flagURL);
        }

        protected virtual void store_construct(ShipConstruct construct)
        {
            Facility = construct.shipFacility;
            StoreKit(new VesselKit(this, construct));
            construct.Unload();
            if(kit.AdditionalResources.Count > 0)
                resource_manifest_view.Show(true);
        }

        protected virtual void update_part_events()
        {
            if(kit && kit.AdditionalResources.Count > 0)
            {
                resource_manifest_view.DrawContent = kit.AdditionalResources.Draw;
                Events["ShowResources"].active = true;
            }
            else
            {
                resource_manifest_view.Show(false);
                Events["ShowResources"].active = false;
            }
        }

        protected void on_kit_changed(bool slow)
        {
            update_part_info();
            update_constraint_controls();
            update_size(slow);
            create_deploy_hint_mesh();
            update_deploy_hint();
        }

        public void StoreKit(VesselKit kit, bool slow = false)
        {
            if(CanConstruct(kit))
            {
                this.kit = kit;
                on_kit_changed(slow);
            }
            else
                Utils.Message("This kit cannot be constructed inside this container");
        }

        public void RemoveKit(bool slow = false)
        {
            if(kit)
            {
                kit = new VesselKit();
                on_kit_changed(slow);
            }
        }
        #endregion

        #region Deployment
        protected void shift_Y_rotation(int delta)
        {
            yRotation =
                (YRotation)(((int)yRotation + delta) % Enum.GetNames(typeof(YRotation)).Length);
            if(yRotation < 0)
                yRotation = 0;
            create_deploy_hint_mesh();
            ShowDeployHint = true;
        }

        [KSPEvent(guiName = "Rotate launch direction",
            guiActiveUnfocused = true,
            unfocusedRange = 10,
            guiActive = true,
            guiActiveEditor = true,
            active = true)]
        public void RotateSpawnOrientation()
        {
            if(kit && state == DeploymentState.IDLE)
                shift_Y_rotation(1);
        }

        protected Quaternion get_Y_rotation() =>
            Quaternion.AngleAxis((int)yRotation * 90f, Vector3.up);

        protected override Vector3 get_deployed_size() => kit.ShipMetric.size;

        protected abstract Transform get_deploy_transform_unrotated(
            Vector3 size,
            out Vector3 spawn_offset
        );

        protected override Transform get_deploy_transform(Vector3 size, out Vector3 spawn_offset)
        {
            var localRotation = Quaternion.identity;
            if(yRotation > 0)
            {
                localRotation = get_Y_rotation();
                if(!size.IsZero())
                    size = Quaternion.Inverse(localRotation)
                           * ((localRotation * size).AbsComponents());
            }
            var T = get_deploy_transform_unrotated(size, out spawn_offset);
            if(yRotation > 0 && T != null)
            {
                var rT = T.Find("__SPAWN_TRANSFORM_ROTATED");
                if(rT == null)
                {
                    var empty = new GameObject("__SPAWN_TRANSFORM_ROTATED");
                    empty.transform.SetParent(T, false);
                    rT = empty.transform;
                }
                rT.localPosition = Vector3.zero;
                rT.localRotation = localRotation;
                return rT;
            }
            return T;
        }

        protected override bool can_deploy()
        {
            if(Empty)
            {
                Utils.Message("Cannot deploy: nothing is stored inside the container.");
                return false;
            }
            if(vessel.packed)
            {
                Utils.Message("Cannot deploy a packed construction kit.");
                return false;
            }
            return true;
        }

        protected override void create_deploy_hint_mesh()
        {
            base.create_deploy_hint_mesh();
            if(kit)
            {
                var mesh = kit.ShipMetric.hull_mesh;
                if(mesh != null)
                    kit_hull_mesh.mesh = mesh;
            }
        }

        protected virtual void update_kit_hull_mesh(
            Transform deployT,
            Vector3 deployed_size,
            Vector3 spawn_offset
        )
        {
            if(deployT != null)
            {
                var growth_point = get_point_of_growth();
                var size = Size.Local2LocalDir(model, deployT).AbsComponents();
                var scale = Vector3.Scale(deployed_size, size.Inverse());
                var growth =
                    Vector3.Scale(
                        deployT.InverseTransformDirection(deployT.position - growth_point),
                        scale);
                var T = kit_hull_mesh.gameObject.transform;
                T.position = growth_point
                             + deployT.TransformDirection(growth)
                             + deployT.TransformDirection(spawn_offset - kit.ShipMetric.center);
                T.rotation = deployT.rotation;
            }
        }

        protected override void update_deploy_hint(
            Transform deployT,
            Vector3 deployed_size,
            Vector3 spawn_offset
        )
        {
            base.update_deploy_hint(deployT, deployed_size, spawn_offset);
            update_kit_hull_mesh(deployT, deployed_size, spawn_offset);
        }

        protected override void show_deploy_hint(bool show)
        {
            show &= kit;
            base.show_deploy_hint(show);
            kit_hull_mesh.gameObject.SetActive(show);
        }

        protected override IEnumerable prepare_deployment()
        {
            Events["DeployEvent"].active = false;
            return base.prepare_deployment();
        }

        protected override IEnumerable finalize_deployment()
        {
            update_unfocusedRange("Launch");
            yield return null;
        }

        public bool Empty => !kit;
        public override string Name => Empty ? "Container" : "Container: " + kit.Name;

        bool IKitContainer.Valid => isEnabled && ValidKitContainer;

        protected virtual bool ValidKitContainer => true;

        bool IConstructionSpace.Valid =>
            isEnabled && !Empty && kit.StageComplete(DIYKit.ASSEMBLY) && ValidConstructionSpace;

        public bool ConstructionComplete =>
            state == DeploymentState.DEPLOYED && kit && kit.Complete;

        protected virtual bool ValidConstructionSpace => true;

        [KSPEvent(guiName = "Deploy",
#if DEBUG
            guiActive = true,
#endif
            guiActiveUnfocused = true,
            unfocusedRange = 10,
            active = true)]
        public void DeployEvent()
        {
            Deploy();
        }
        #endregion

        #region Launching
        public virtual void Launch()
        {
            if(!can_launch())
                return;
            StartCoroutine(launch_complete_construct());
        }

        [KSPEvent(guiName = "Launch",
#if DEBUG
            guiActive = true,
#endif
            guiActiveUnfocused = true,
            unfocusedRange = 10,
            active = false)]
        public void LaunchEvent() => Launch();

        public virtual void EnableControls(bool enable = true)
        {
            Events["LaunchEvent"].active = enable;
        }

        protected virtual bool can_launch()
        {
            if(vessel_spawner.LaunchInProgress)
                return false;
            if(Empty)
            {
                Utils.Message("Nothing to launch: container is empty.");
                return false;
            }
            if(vessel.packed)
            {
                Utils.Message("Cannot launch from a packed construction kit.");
                return false;
            }
            if(!kit.Complete)
            {
                Utils.Message("Construction is not complete yet.");
                return false;
            }
            if(!kit.BlueprintComplete())
            {
                Utils.Message("Something went wrong. Not all parts were properly constructed.");
                return false;
            }
            return true;
        }

        protected virtual void on_vessel_loaded(Vessel vsl)
        {
            FXMonger.Explode(part, part.partTransform.position, 0);
            ShowDeployHint = false;
        }

        protected virtual void on_vessel_launched(Vessel vsl) => kit.TransferCrewToKit(vsl);

        protected abstract IEnumerator launch(ShipConstruct construct);

        IEnumerator<YieldInstruction> launch_complete_construct()
        {
            if(!HighLogic.LoadedSceneIsFlight)
                yield break;
            while(!FlightGlobals.ready)
                yield return null;
            vessel_spawner.BeginLaunch();
            //hide UI
            GameEvents.onHideUI.Fire();
            yield return null;
            //save the game
            GroundConstructionScenario.SaveGame(kit.Name + "-before_launch");
            yield return null;
            yield return new WaitForFixedUpdate();
            //load ship construct and launch it
            var construct = kit.LoadConstruct();
            if(construct == null)
            {
                Utils.Log("Unable to load ShipConstruct {}. "
                          + "This usually means that some parts are missing "
                          + "or some modules failed to initialize.",
                    kit.Name);
                Utils.Message("Something went wrong. Constructed ship cannot be launched.");
                GameEvents.onShowUI.Fire();
                vessel_spawner.AbortLaunch();
                yield break;
            }
            model.gameObject.SetActive(false);
            FXMonger.Explode(part, part.partTransform.position, 0);
            yield return StartCoroutine(launch(construct));
            GameEvents.onShowUI.Fire();
            part.Die();
        }

        public void ShowUI(bool enable = true) { }

        void OnGUI()
        {
            if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
                return;
            Styles.Init();
            if(vessel_spawner.LaunchInProgress)
            {
                GUI.Label(new Rect(Screen.width / 2 - 190, 30, 380, 70),
                    "<b><color=#FFD100><size=30>Launching. Please, wait...</size></color></b>",
                    Styles.rich_label);
                return;
            }
            construct_loader.Draw();
        }
        #endregion

        #region IPartCostModifier implementation
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) =>
            Empty ? 0 : kit.Cost;

        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.CONSTANTLY;
        #endregion

        #region IPartMassModifier implementation
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) =>
            Empty ? 0 : kit.Mass;

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;
        #endregion

        #region IConfigurable implementation
        public virtual bool IsConfigurable => kit;

        public virtual void DrawOptions()
        {
            GUILayout.BeginHorizontal(Styles.white);
            GUILayout.Label("Launch orientation:");
            GUILayout.FlexibleSpace();
            Utils.ButtonSwitch("Show", ref ShowDeployHint);
            if(state == DeploymentState.IDLE)
            {
                var choice = Utils.LeftRightChooser(yRotation.ToString(), width: 160);
                if(choice != 0)
                    shift_Y_rotation(choice);
            }
            else
                GUILayout.Label(yRotation.ToString(),
                    Styles.enabled,
                    GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
        }
        #endregion

#if DEBUG
        void OnRenderObject()
        {
            var T = get_deploy_transform(Size, out _);
            if(T != null)
            {
                var pos = T.position;
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
            Utils.GLDrawPoint(model.TransformPoint(PartCenter), Color.yellow);
        }
#endif
    }
}
