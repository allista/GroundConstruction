//   DeployableKitContainer.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri

using System;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;
using System.Collections;

namespace GroundConstruction
{
    public abstract partial class DeployableKitContainer : DeployableModel, IConstructionSpace, IControllable, IAssemblySpace
    {
        [KSPField(isPersistant = true)] public EditorFacility Facility;

        [KSPField(guiName = "Kit", guiActive = true, guiActiveEditor = true, isPersistant = true)]
        public string KitName = "None";
        protected SimpleTextEntry kitname_editor;
        protected ShipConstructLoader construct_loader;
        protected VesselSpawner vessel_spawner;

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

        void update_part_info()
        {
            if(Empty)
            {
                KitName = "None";
                KitMass = 0;
                KitCost = 0;
                KitWork = 0;
                KitRes  = 0;
            }
            else
            {
                KitName = kit.Name;
                KitMass = kit.Mass;
                KitCost = kit.Cost;
                var rem = kit.RemainingRequirements();
                KitWork = (float)rem.work/3600;
                KitRes  = (float)rem.resource_amount;
            }
        }

        public override void OnAwake()
        {
            base.OnAwake();
            //add UI components
            kitname_editor = gameObject.AddComponent<SimpleTextEntry>();
            construct_loader = gameObject.AddComponent<ShipConstructLoader>();
            construct_loader.process_construct = store_construct;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Destroy(deploy_hint_mesh.gameObject);
            Destroy(kitname_editor);
            Destroy(construct_loader);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            vessel_spawner = new VesselSpawner(part);
            Events["DeployEvent"].active = !Empty && State == DeplyomentState.IDLE;
            Events["LaunchEvent"].active = !Empty && State == DeplyomentState.DEPLOYED && kit.Complete;
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
        bool IAssemblySpace.Valid => isEnabled && (Empty || !kit.StageComplete(DIYKit.ASSEMBLY));

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
            StoreKit(kit, true);
            kit.Host = this;
        }

        public void SpawnKit()
        {
            if(!kit) return;
            if(!kit.StageComplete(DIYKit.ASSEMBLY))
            {
                Utils.Message("The kit is not yet assembled");
                return;
            }
        }
        #endregion

        #region Select Ship Construct
        public virtual bool CanConstruct(VesselKit kit) => true;

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

        protected virtual void store_construct(ShipConstruct construct)
        {
            Facility = construct.shipFacility;
            StoreKit(new VesselKit(this, construct));
            construct.Unload();
        }

        public void StoreKit(VesselKit kit, bool slow = false)
        {
            if(CanConstruct(kit))
            {
                this.kit = kit;
                update_part_info();
                update_constraint_controls();
                update_size(slow);
                create_deploy_hint_mesh();
                update_deploy_hint();
                if(HighLogic.LoadedSceneIsEditor ||
                   !GroundConstructionScenario.ShowDeployHint)
                    deploy_hint_mesh.gameObject.SetActive(false);
            }
            else
                Utils.Message("This kit cannot be constructed inside this container");
        }
        #endregion

        #region Deployment
        protected override Vector3 get_deployed_size() => kit.ShipMetric.size;

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
        public bool Valid => isEnabled;
        public override string Name => Empty? "Container" : "Container: "+kit.Name;

        [KSPEvent(guiName = "Deploy",
                  #if DEBUG
                  guiActive = true,
                  #endif
                  guiActiveUnfocused = true, unfocusedRange = 10, active = true)]
        public void DeployEvent()
        {
            Deploy();
        }
        #endregion

        #region Launching
        public virtual void Launch()
        {
            if(!can_launch()) return;
            StartCoroutine(launch_complete_construct());
        }

        [KSPEvent(guiName = "Launch",
                  #if DEBUG
                  guiActive = true,
                  #endif
                  guiActiveUnfocused = true, unfocusedRange = 10, active = false)]
        public void LaunchEvent() => Launch();

        [KSPEvent(guiName = "Rename Kit", guiActive = true, guiActiveEditor = true,
                  guiActiveUnfocused = true, unfocusedRange = 10, active = true)]
        public void EditName()
        {
            kitname_editor.Text = KitName;
            kitname_editor.Toggle();
        }

        public void EnableControls(bool enable = true)
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
                Utils.Message("Something whent wrong. Not all parts were properly constructed.");
                return false;
            }
            return true;
        }

        protected virtual void on_vessel_loaded(Vessel vsl) =>
        FXMonger.Explode(part, part.partTransform.position, 0);

        protected virtual void on_vessel_launched(Vessel vsl)
        {
            kit.TransferCrewToKit(vsl);
        }

        protected abstract IEnumerator<YieldInstruction> launch(ShipConstruct construct);

        IEnumerator<YieldInstruction> launch_complete_construct()
        {
            if(!HighLogic.LoadedSceneIsFlight) yield break;
            while(!FlightGlobals.ready) yield return null;
            vessel_spawner.BeginLaunch();
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
                Utils.Log("Unable to load ShipConstruct {}. " +
                          "This usually means that some parts are missing " +
                          "or some modules failed to initialize.", kit.Name);
                Utils.Message("Something whent wrong. Constructed ship cannot be launched.");
                GameEvents.onShowUI.Fire();
                vessel_spawner.AbortLaunch();
                yield break;
            }
            model.gameObject.SetActive(false);
            yield return StartCoroutine(launch(construct));
            GameEvents.onShowUI.Fire();
            part.Die();
        }

        public void ShowUI(bool enable = true) {}

        void OnGUI()
        {
            if(Event.current.type != EventType.Layout && 
               Event.current.type != EventType.Repaint) return;
            Styles.Init();
            if(vessel_spawner.LaunchInProgress)
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
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => 
        Empty ? 0 : kit.Cost;

        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.CONSTANTLY;
        #endregion

        #region IPartMassModifier implementation
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => 
        Empty ? 0 : kit.Mass;

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;
        #endregion
    }
}
