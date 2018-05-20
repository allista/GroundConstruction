//   AssemblyWorkshop.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using System.Collections.Generic;
using AT_Utils;
using KSP.UI.Screens;
using UnityEngine;

namespace GroundConstruction
{
    public abstract class AssemblyWorkshop : VesselKitWorkshop<AssemblyKitInfo>, IKitContainer
    {
        [KSPField(isPersistant=true)] 
        public PersistentList<VesselKit> Kits = new PersistentList<VesselKit>();

        ShipConstructLoader construct_loader;

		protected override int STAGE => DIYKit.ASSEMBLY;

        public bool Empty => Kits.Count == 0;

        public List<VesselKit> GetKits() => Kits;

        public VesselKit GetKit(Guid id) => Kits.Find(kit => kit.id == id);

        protected override bool check_task(AssemblyKitInfo task)
        {
            return base.check_task(task) && task.Kit.CurrentStageIndex == DIYKit.ASSEMBLY;
        }

        protected virtual void process_construct(ShipConstruct construct)
        {
            var kit = new VesselKit(this, construct);
            if(find_assembly_space(kit) != null)
            {
				Kits.Add(kit);
                Queue.Enqueue(new AssemblyKitInfo(kit));
            }
        }

        protected abstract IAssemblySpace find_assembly_space(VesselKit kit);

        protected IAssemblySpace find_assembly_space(VesselKit kit, Vessel vsl)
        {
            foreach(var space in VesselKitInfo.GetKitContainers<IAssemblySpace>(vsl))
            {
                var ratio = space.KitToSpaceRatio(kit);
                if(ratio > 0)
                    return space;
            }
            return null;
        }

        protected IAssemblySpace find_best_assembly_space(VesselKit kit, Vessel vsl)
        {
            float best_ratio = -1;
            IAssemblySpace available_space = null;
            foreach(var space in VesselKitInfo.GetKitContainers<IAssemblySpace>(vsl))
            {
                var ratio = space.KitToSpaceRatio(kit);
                if(ratio > 0)
                {
                    if(best_ratio < 0 || ratio < best_ratio)
                    {
                        best_ratio = ratio;
                        available_space = space;
                    }
                }
            }
            return available_space;
        }

        public override void OnAwake()
        {
            base.OnAwake();
            construct_loader = gameObject.AddComponent<ShipConstructLoader>();
            construct_loader.process_construct = process_construct;
            construct_loader.Show(false);
        }

        protected override void OnDestroy()
        {
            Destroy(construct_loader);
            base.OnDestroy();
        }

		protected override void draw()
		{
            base.draw();
            construct_loader.Draw();
		}
	}
}

