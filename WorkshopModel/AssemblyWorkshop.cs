//   AssemblyWorkshop.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri

using System;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;
using AT_Utils.UI;

namespace GroundConstruction
{
    public abstract class AssemblyWorkshop : VesselKitWorkshop<AssemblyKitInfo>, IKitContainer
    {
        [KSPField]
        public string KitParts = "DIYKit";
        SortedList<string, string> kit_parts = new SortedList<string, string>();

        [KSPField(isPersistant = true)]
        public string SelectedPart = string.Empty;
        string kit_part => kit_parts[SelectedPart];

        [KSPField(isPersistant = true)]
        public PersistentList<VesselKit> Kits = new PersistentList<VesselKit>();

        List<IAssemblySpace> available_spaces = new List<IAssemblySpace>();

        ShipConstructLoader construct_loader;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if(!string.IsNullOrEmpty(KitParts))
            {
                foreach(var part_name in Utils.ParseLine(KitParts, Utils.Comma))
                {
                    var part_info = PartLoader.getPartInfoByName(part_name);
                    if(part_info == null)
                    {
                        this.Log("[WARNING] no such part: {}", part_name);
                        continue;
                    }
                    kit_parts.Add(part_info.title, part_name);
                }
                if(kit_parts.Count > 0
                   && (string.IsNullOrEmpty(SelectedPart)
                       || !kit_parts.ContainsKey(SelectedPart)))
                    SelectedPart = kit_parts.Keys[0];
            }
        }

        public string Name => part.Title();
        public bool Empty => Kits.Count == 0;
        public bool Valid => isEnabled;
        public List<VesselKit> GetKits() => Kits;
        public VesselKit GetKit(Guid id) => Kits.Find(kit => kit.id == id);

        protected override bool check_task(AssemblyKitInfo task) =>
        (base.check_task(task)
         && (task.Kit.CurrentStageIndex < DIYKit.CONSTRUCTION
             || !task.Kit.StageStarted(DIYKit.CONSTRUCTION)));

        protected override bool check_host(AssemblyKitInfo task) =>
        (base.check_host(task)
         && (task.Container as PartModule == this
             || task.AssemblySpace != null && task.AssemblySpace.Valid));

        protected virtual void process_construct(ShipConstruct construct)
        {
            var kit = new VesselKit(this, construct, false);
            if (selected_space is PartModule)
            {
                if(!selected_space.Empty)
                    Utils.Message("Selected assembly space is occupied");
                else if (!selected_space.CheckKit(kit, kit_part, out float ratio))
                {
                    if(ratio > 0)
                        Utils.Message("Selected assembly space is too small");
                    else
                        Utils.Message("Selected container is not suitable for construction of this kit");
                }
                else
                    selected_space.SetKit(kit, kit_part);
            }
            else if(find_assembly_space(kit, false) != null)
            {
                kit.Host = this;
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
                if(space != null)
                {
                    space.SetKit(task.Kit, kit_part);
                    Kits.Remove(task.Kit);
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
                available_spaces = get_assembly_spaces();
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
                if(space.Valid)
                {
                    if(space.CheckKit(kit, kit_part, out _))
                        return space;
                }
            }
            return null;
        }

        IAssemblySpace find_best_assembly_space(VesselKit kit, IList<IAssemblySpace> spaces)
        {
            float best_ratio = -1;
            IAssemblySpace available_space = null;
            foreach(var space in spaces)
            {
                if(space.Valid)
                {
                    if(space.CheckKit(kit, kit_part, out float ratio))
                    {
                        if(ratio > best_ratio)
                        {
                            best_ratio = ratio;
                            available_space = space;
                        }
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

        protected void update_kits(IEnumerable<IKitContainer> containers)
        {
            if(containers == null) return;
            var queued = get_queued_ids();
            foreach(var container in containers)
            {
                if(!container.Valid) continue;
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

        protected override void unbuilt_kits_pane()
        {
            if(unbuilt_kits.Count == 0) return;
            GUILayout.Label("Available DIY kits:", Styles.label, GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(Styles.white);
            BeginScroll(unbuilt_kits.Count, ref unbuilt_scroll);
            AssemblyKitInfo add = null, remove = null;
            foreach(var info in unbuilt_kits)
            {
                GUILayout.BeginHorizontal();
                draw_task(info);
                if(!info.Kit.StageStarted(DIYKit.ASSEMBLY) &&
                   GUILayout.Button(new GUIContent("<b>X</b>", "Discard this kit"),
                                    Styles.danger_button, GUILayout.ExpandWidth(false),
                                    GUILayout.ExpandHeight(true)))
                    remove = info;
                if(GUILayout.Button(new GUIContent("Add", "Add this kit to construction queue"),
                                    Styles.enabled_button, GUILayout.ExpandWidth(false),
                                    GUILayout.ExpandHeight(true)))
                    add = info;
                GUILayout.EndHorizontal();
            }
            if(add != null)
                Queue.Enqueue(add);
            else if(remove != null)
            {
                var space = remove.AssemblySpace;
                if(space != null)
                    space.SetKit(null, "");
                clear_if_selected(remove);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
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
                draw_task(info);
                if(GUILayout.Button(new GUIContent("Finalize", "Finalize assembly and seal the container"),
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
        IAssemblySpace selected_space;
        protected virtual void assembly_spaces_pane()
        {
            if(available_spaces.Count == 0) return;
            GUILayout.Label("Available assembly spaces:", Styles.label, GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(Styles.white);
            BeginScroll(available_spaces.Count, ref assembly_spaces_scroll, 40);
            foreach(var space in available_spaces)
            {
                GUILayout.BeginHorizontal(Styles.white);
                if(GUILayout.Button(new GUIContent(Colors.Active.Tag("<b>{0}</b>", space.Name),
                                                   "Press to select to assign assembly task to this space. " +
                                                   "Press again to deselect."),
                                    selected_space == space ? Styles.normal_button : Styles.rich_label,
                                    GUILayout.ExpandWidth(true)))
                {
                    if(selected_space != space)
                        selected_space = space;
                    else
                        selected_space = null;
                }
                if (space is PartModule module)
                    set_highlighted_part(module.part);
                if(space.Empty)
                {
                    if (space is IContainerProducer producer)
                    {
                        if(GUILayout.Button(new GUIContent("Create Empty Container",
                                                           "Create a new empty container of the selected type"),
                                            Styles.active_button, GUILayout.ExpandWidth(false)))
                            producer.SpawnEmptyContainer(kit_part);
                    }
                    if (space is IAnimatedSpace animated && animated.HasAnimator)
                    {
                        var opened = animated.Opened;
                        if(Utils.ButtonSwitch("Close", "Open", opened, "",
                                              GUILayout.ExpandWidth(false)))
                        {
                            if(opened) animated.Close();
                            else animated.Open();
                        }
                    }
                }
                else
                {
                    if (space is IConstructionSpace construction_space && construction_space.Valid)
                        GUILayout.Label(new GUIContent(Colors.Enabled.Tag("In-place Construction"),
                                                       "It is possible to construct this kit directly in the assembly space"),
                                        Styles.rich_label, GUILayout.ExpandWidth(false));
                    GUILayout.Space(10);
                    GUILayout.Label(new GUIContent(Colors.Warning.Tag("Occupied"),
                                                   "The assembly space is occupied by a kit being assembled " +
                                                   "or just by something located inside."),
                                    Styles.rich_label, GUILayout.ExpandWidth(false));
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if(kit_parts.Count > 1)
            {
                GUILayout.BeginHorizontal();
                SelectedPart = Utils.LeftRightChooser(SelectedPart, kit_parts, "Select container type to use");
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(new GUIContent("Add Vessel",
                                               "Add a vessel from VAB/SPH to construction queue"),
                                Styles.active_button, GUILayout.ExpandWidth(true)))
                construct_loader.SelectVessel();
            if(GUILayout.Button(new GUIContent("Add Subassembly",
                                               "Add a subassembly to construction queue"),
                                Styles.active_button, GUILayout.ExpandWidth(true)))
                construct_loader.SelectSubassembly();
            if(GUILayout.Button(new GUIContent("Add Part",
                                               "Add a single part to construction queue"),
                                Styles.active_button, GUILayout.ExpandWidth(true)))
                construct_loader.SelectPart(part.flagURL);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        protected override void info_pane()
        {
            base.info_pane();
            assembly_spaces_pane();
        }
        #endregion
    }
}

