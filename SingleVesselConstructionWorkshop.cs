// SingleVesselConstructionWorkshop.cs
//
//  Author:
//       Allis Tauri allista@gmail.com
//
//  Copyright (c) 2018 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;


namespace GroundConstruction
{
    public class SingleVesselConstructionWorkshop : ConstructionWorkshop
    {
        protected override void update_kits()
        {
            base.update_kits();
            var queued = get_queued_ids();
            if(vessel.loaded)
            {
                var containers = VesselKitInfo.GetKitContainers<IConstructionSpace>(vessel);
                if(containers != null)
                {
                    foreach(var container in containers.Where(c => (c as IDeployableContainer) == null))
                    {
                        foreach(var vsl_kit in container.GetKits())
                        {
                            if(vsl_kit != null && vsl_kit.Valid && vsl_kit.CurrentStageIndex == STAGE &&
                               vsl_kit != CurrentTask.Kit && !queued.Contains(vsl_kit.id))
                            {
                                if(!vsl_kit.Complete)
                                    unbuilt_kits.Add(new ConstructionKitInfo(vsl_kit));
                                else built_kits.Add(new ConstructionKitInfo(vsl_kit));
                            }
                        }
                    }
                }
            }
        }

        protected override bool init_task(ConstructionKitInfo task) => true;
        protected override bool check_host(ConstructionKitInfo task) => 
        task.Module != null && task.Module.vessel == vessel;
              
        #region GUI
        protected override void info_pane()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("<color=silver>Workforce:</color> <b>{0:F1}</b>/{0:F1} SK",
                                          workforce, max_workforce),
                            Styles.boxed_label, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        Vector2 unbuilt_scroll = Vector2.zero;
        protected override void unbuilt_kits_pane()
        {
            if(unbuilt_kits.Count == 0) return;
            GUILayout.Label("Unbuilt DIY kits:", Styles.label, GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(Styles.white);
            BeginScroll(unbuilt_kits.Count, ref unbuilt_scroll);
            ConstructionKitInfo add = null;
            foreach(var info in unbuilt_kits)
            {
                GUILayout.BeginHorizontal();
                info.Draw();
                set_highlighted_task(info);
                if(GUILayout.Button(new GUIContent("Add", "Add this kit to construction queue"),
                                    Styles.enabled_button, GUILayout.ExpandWidth(false)))
                    add = info;
                GUILayout.EndHorizontal();
            }
            if(add != null)
                Queue.Enqueue(add);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
        #endregion
    }
}
