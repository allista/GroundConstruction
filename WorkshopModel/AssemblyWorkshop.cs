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
        protected override bool check_task(AssemblyKitInfo task) => 
        base.check_task(task) && task.Kit.CurrentStageIndex == DIYKit.ASSEMBLY;

        protected virtual void process_construct(ShipConstruct construct)
        {
            var kit = new VesselKit(this, construct);
            if(find_assembly_space(kit, false) != null)
            {
                Kits.Add(kit);
                Queue.Enqueue(new AssemblyKitInfo(kit));
            }
        }

        protected override bool init_task(AssemblyKitInfo task)
        {
            if(task.Recheck())
            {
                var space = task.AssemblySpace;
                if(space != null) return true;
                space = find_assembly_space(task.Kit, true);
                var space_module = space as PartModule;
                if(space != null && space_module != null)
                {
                    space.SetKit(task.Kit);
                    task.Kit.Host = space_module;
                    return true;
                }
            }
            return false;
        }

        protected abstract List<IAssemblySpace> get_assembly_spaces();
        protected virtual IAssemblySpace find_assembly_space(VesselKit kit, bool best)
        {
            var spaces = get_assembly_spaces();
            return best ? find_best_assembly_space(kit, spaces) : find_assembly_space(kit, spaces);
        }

        IAssemblySpace find_assembly_space(VesselKit kit, IList<IAssemblySpace> spaces)
        {
            foreach(var space in spaces)
            {
                var ratio = space.KitToSpaceRatio(kit);
                if(ratio > 0)
                    return space;
            }
            return null;
        }

        IAssemblySpace find_best_assembly_space(VesselKit kit, IList<IAssemblySpace> spaces)
        {
            float best_ratio = -1;
            IAssemblySpace available_space = null;
            foreach(var space in spaces)
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

        protected IAssemblySpace find_assembly_space(VesselKit kit, Part p) =>
        find_assembly_space(kit, p.FindModulesImplementing<IAssemblySpace>());

        protected IAssemblySpace find_best_assembly_space(VesselKit kit, Part p) =>
        find_best_assembly_space(kit, p.FindModulesImplementing<IAssemblySpace>());

        protected IAssemblySpace find_assembly_space(VesselKit kit, Vessel vsl) =>
        find_assembly_space(kit, VesselKitInfo.GetKitContainers<IAssemblySpace>(vsl));

        protected IAssemblySpace find_best_assembly_space(VesselKit kit, Vessel vsl) =>
        find_best_assembly_space(kit, VesselKitInfo.GetKitContainers<IAssemblySpace>(vsl));

        protected void update_kits(List<IKitContainer> containers)
        {
            if(containers == null) return;
            var queued = get_queued_ids();
            foreach(var container in containers)
            {
                if(container == this) continue;
                foreach(var vsl_kit in container.GetKits())
                {
                    if(vsl_kit != null && vsl_kit.Valid &&
                       vsl_kit != CurrentTask.Kit && !queued.Contains(vsl_kit.id))
                        sort_task(new AssemblyKitInfo(vsl_kit));
                }
            }
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

