//   InSituAssemblySpace.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri
using System;
using System.Collections.Generic;
using AT_Utils;

namespace GroundConstruction
{
    public class InSituAssemblySpace : PartModule, IAssemblySpace
    {
        DeployableKitContainer container;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            container = part.FindModuleImplementing<DeployableKitContainer>();
            if(container == null || 
               !container.kit.Empty && container.kit.StageComplete(DIYKit.ASSEMBLY))
                this.EnableModule(false);
            else
                container.kit.Host = this;
        }

        #region IAssemblySpace
        public string Name => part.Title();
        public bool Empty => container == null || container.Empty;
        public bool Valid => isEnabled;
        public VesselKit GetKit(Guid id) => container.GetKit(id);
        public List<VesselKit> GetKits() => new List<VesselKit> { container.kit };

        public bool CheckKit(VesselKit kit, string part_name, out float kit2space_ratio)
        {
            kit2space_ratio = -1;
            if(kit && container.CanConstruct(kit))
            {
                kit2space_ratio = 1;
                return true;
            }
            return false;
        }

        public void SetKit(VesselKit kit, string part_name) 
        {
            container.StoreKit(kit, true);
            kit.Host = this;
        }

        public bool SpawnAutomatically => true;

        public void SpawnKit()
        {
            if(!container.kit) return;
            if(!container.kit.StageComplete(DIYKit.ASSEMBLY))
            {
                Utils.Message("The kit is not yet assembled");
                return;
            }
            this.EnableModule(false);
            container.kit.Host = container;
        }

        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => 0;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => 0;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        #endregion
    }
}
