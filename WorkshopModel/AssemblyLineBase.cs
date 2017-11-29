////   AssemblyLineBase.cs
////
////  Author:
////       Allis Tauri <allista@gmail.com>
////
////  Copyright (c) 2017 Allis Tauri
//using System;
//using UnityEngine;
//using KSP.UI.Screens;
//using AT_Utils;
//
//namespace GroundConstruction
//{
//    public interface IAssemblySpace
//    {
//        Vector3 GetMaxDimensions();
//        bool StartAssembly(ShipConstruct ship);
//        bool UpdateProgress(float delta);
//        void SpawnShip();
//    }
//
//    public abstract class AssemblyLineBase : WorkshopBase
//    {
//        SubassemblySelector subassembly_selector;
//        CraftBrowserDialog vessel_selector;
//        EditorFacility facility;
//
//        void OnAwake()
//        {
//            base.OnAwake();
//            subassembly_selector = gameObject.AddComponent<SubassemblySelector>();
//        }
//
//        void OnDestroy()
//        {
//            Destroy(subassembly_selector);
//        }
//
//        protected abstract bool request_assembly_space(ShipConstruct ship,
//                                                       Callback<IAssemblySpace> space_available,
//                                                       Callback<string> space_unavailable);
//
//        protected ShipConstruct create_kit(ShipConstruct content) {}
//
//        protected void subassembly_selected(ShipTemplate template) {}
//        protected void ship_selected(ShipConstruct template) {}
//
//
//
//        void main_window(int WindowID)
//        {
//            GUILayout.BeginVertical();
//            info_pane();
//            nearby_kits_pane();
//            queue_pane();
//            construction_pane();
//            built_kits_pane();
//            if(GUILayout.Button("Close", Styles.close_button, GUILayout.ExpandWidth(true)))
//                show_window = false;
//            GUILayout.EndVertical();
//            GUIWindowBase.TooltipsAndDragWindow();
//        }
//
//        protected override void draw()
//        {
//
//
//            if(vessel_selector == null && 
//               (subassembly_selector == null || 
//                !subassembly_selector.WindowEnabled))
//            {
//                Utils.LockIfMouseOver(LockName, WindowPos);
//                WindowPos = GUILayout.Window(GetInstanceID(),
//                                         WindowPos, main_window, part.partInfo.title,
//                                         GUILayout.Width(width),
//                                         GUILayout.Height(height)).clampToScreen();
//            }
//            if(subassembly_selector != null)
//                subassembly_selector.Draw(subassembly_selected);
//        }
//    }
//}
//
