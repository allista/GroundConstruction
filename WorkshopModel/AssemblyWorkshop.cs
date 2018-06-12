//   AssemblyWorkshop.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using System.Collections.Generic;
using System.Linq;
using AT_Utils;
using UnityEngine;

namespace GroundConstruction
{
    public abstract class AssemblyWorkshop : VesselKitWorkshop<AssemblyKitInfo>, IKitContainer
    {
        [KSPField(isPersistant = true)]
        public PersistentList<VesselKit> Kits = new PersistentList<VesselKit>();

        List<IAssemblySpace> available_spaces = new List<IAssemblySpace>();

        ShipConstructLoader construct_loader;

        public string Name => part.Title();
        public bool Empty => Kits.Count == 0;
        public List<VesselKit> GetKits() => Kits;
        public VesselKit GetKit(Guid id) => Kits.Find(kit => kit.id == id);

        protected override bool check_task(AssemblyKitInfo task) =>
        base.check_task(task) && task.Kit.CurrentStageIndex <= DIYKit.CONSTRUCTION;

        protected virtual void process_construct(ShipConstruct construct)
        {
            var kit = new VesselKit(this, construct, false);
            if(find_assembly_space(kit, false) != null)
            {
                Kits.Add(kit);
                Queue.Enqueue(new AssemblyKitInfo(kit));
            }
            else
                Utils.Message("No suitable assembly space was found.");
            construct.Unload();
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
                    Kits.Remove(task.Kit);
                    task.Kit.Host = space_module;
                    return true;
                }
            }
            return false;
        }

        protected override void update_ui_data()
        {
            base.update_ui_data();
            available_spaces.Clear();
            if(vessel != null && vessel.loaded)
                available_spaces = get_assembly_spaces();//.Where(s => s.Empty));
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
                if(!space.Empty) continue;
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
                if(!space.Empty) continue;
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
                if(container as AssemblyWorkshop == this) continue;
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

        #region GUI
        public override string Stage_Display => "ASSEMBLY";

        protected override void queue_pane()
        {
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(new GUIContent("Add Vessel",
                                               "Add a vessel from VAB/SPH to construction queue"),
                                Styles.active_button, GUILayout.ExpandWidth(true)))
                construct_loader.SelectVessel();
            if(GUILayout.Button(new GUIContent("Add Subassembly",
                                               "Add a subassembly to construction queue"),
                                Styles.active_button, GUILayout.ExpandWidth(true)))
                construct_loader.SelectSubassembly();
            GUILayout.EndHorizontal();
            base.queue_pane();
        }

        protected override void built_kits_pane()
        {
            if(built_kits.Count == 0) return;
            GUILayout.Label("Built DIY kits:", Styles.label, GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(Styles.white);
            BeginScroll(built_kits.Count, ref built_scroll);
            AssemblyKitInfo spawn = null;
            foreach(var info in built_kits)
            {
                GUILayout.BeginHorizontal();
                info.Draw();
                set_highlighted_task(info);
                if(GUILayout.Button(new GUIContent("Release", "Release complete kit from the dock"),
                                    Styles.danger_button, GUILayout.ExpandWidth(false),
                                    GUILayout.ExpandHeight(true)))
                    spawn = info;
                GUILayout.EndHorizontal();
            }
            if(spawn != null && spawn.Recheck())
                spawn.AssemblySpace.SpawnKit();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        Vector2 assembly_spaces_scroll;
        protected virtual void assembly_spaces_pane()
        {
            if(available_spaces.Count == 0) return;
            GUILayout.Label("Available assembly spaces:", Styles.label, GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(Styles.white);
            BeginScroll(available_spaces.Count, ref assembly_spaces_scroll, 40);
            foreach(var space in available_spaces)
            {
                GUILayout.BeginHorizontal(Styles.white);
                GUILayout.Label(string.Format("<color=yellow><b>{0}</b></color>",
                                              available_spaces[0].Name), 
                                Styles.rich_label, GUILayout.ExpandWidth(true));
                if(space.Empty)
                {
                    var opened = space.Opened;
                    if(Utils.ButtonSwitch("Close", "Open", opened, "", 
                                          GUILayout.ExpandWidth(false)))
                    {
                        if(opened) space.Close();
                        else space.Open();
                    }
                }
                else
                    GUILayout.Label("<color=yellow>Occupied</color>",
                                    Styles.rich_label, GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        protected override void info_pane()
        {
            base.info_pane();
            assembly_spaces_pane();
        }

        //protected override void draw_panes()
        //{
        //    base.draw_panes();
        //    assembly_spaces_pane();
        //}
        #endregion
    }
}

