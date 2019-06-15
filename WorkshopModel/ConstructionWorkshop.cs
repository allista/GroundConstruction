//   ConstructionWorkshop.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;
using System;
using System.Collections;

namespace GroundConstruction
{
    public abstract class ConstructionWorkshop : VesselKitWorkshop<ConstructionKitInfo>
    {
        #region Target Actions
        protected ConstructionKitInfo target_kit;
        protected virtual bool check_target_kit(ConstructionKitInfo target) => target.Recheck() && target.Complete;
        #endregion

        #region Resource Transfer
        readonly ResourceManifestList transfer_list = new ResourceManifestList();
        VesselResources host_resources, kit_resources;
        ResourceTransferWindow resources_window;

        protected void setup_resource_transfer(ConstructionKitInfo target)
        {
            target_kit = null;
            //this.Log("res: target_kit: complete {}, check {}", target.Complete, check_target_kit(target));//debug
            if(check_target_kit(target))
                target_kit = target;
            else return;
            host_resources = new VesselResources(vessel);
            kit_resources = target_kit.Kit.ConstructResources;
            transfer_list.NewTransfer(host_resources, kit_resources);
            if(transfer_list.Count > 0)
            {
                resources_window.Show(true);
                resources_window.TransferAction = delegate
                {
                    double dM, dC;
                    transfer_list.TransferResources(host_resources, kit_resources, out dM, out dC);
                    target_kit.Kit.ResourcesMass += (float)dM;
                    target_kit.Kit.ResourcesCost += (float)dC;
                };
            }
            else
            {
                resources_window.TransferAction = null;
                host_resources = null;
                kit_resources = null;
                target_kit = null;
            }
        }
        #endregion

        #region Crew Transfer
        int kit_crew_capacity;
        CrewTransferWindow crew_window;

        protected void setup_crew_transfer(ConstructionKitInfo target)
        {
            target_kit = null;
            //this.Log("crew: target_kit: complete {}, check {}", target.Complete, check_target_kit(target));//debug
            if(check_target_kit(target))
                target_kit = target;
            if(target_kit == null) return;
            target_kit.Kit.CrewSource = vessel;
            target_kit.Kit.KitCrew = new List<ProtoCrewMember>();
            kit_crew_capacity = target_kit.Kit.CrewCapacity();
            crew_window.Show(true);
        }
        #endregion

        #region WorkshopBase
        public override void OnAwake()
        {
            base.OnAwake();
            resources_window = gameObject.AddComponent<ResourceTransferWindow>();
            crew_window = gameObject.AddComponent<CrewTransferWindow>();
        }

        protected override void OnDestroy()
        {
            Destroy(resources_window);
            Destroy(crew_window);
            base.OnDestroy();
        }

        protected override bool check_task(ConstructionKitInfo task) =>
        base.check_task(task) && task.Kit.CurrentStageIndex >= DIYKit.CONSTRUCTION;

        protected override bool check_host(ConstructionKitInfo task) =>
        base.check_host(task) && task.ConstructionSpace != null && task.ConstructionSpace.Valid;

        protected override void draw()
        {
            base.draw();
            if(target_kit != null && target_kit.Recheck())
            {
                resources_window.Draw(string.Format("Transfer resources to {0}", target_kit.Kit.Name), transfer_list);
                crew_window.Draw(vessel.GetVesselCrew(), target_kit.Kit.KitCrew, kit_crew_capacity);
            }
            else
            {
                resources_window.UnlockControls();
                crew_window.UnlockControls();
            }
        }

        protected override void unlock()
        {
            base.unlock();
            resources_window.UnlockControls();
            crew_window.UnlockControls();
        }
        #endregion

        #region Recycling
        Part recycling;

        protected abstract IEnumerable<Vessel> get_recyclable_vessels();

        HashSet<uint> get_parts_to_skip(Vessel vsl)
        {
            HashSet<uint> skip_parts = null;
            if(vsl == vessel)
            {
                skip_parts = new HashSet<uint>
                {
                    vessel.rootPart.craftID,
                    part.craftID
                };
            }
            return skip_parts;
        }

        protected IEnumerator recycle(Vessel vsl, bool discard_excess_resources)
        => recycle(vsl.rootPart, discard_excess_resources);

        protected IEnumerator recycle(Part p, bool discard_excess_resources)
        {
            if(recycling != null) yield break;
            recycling = p;
            foreach(var result in recycle(p,
                                          get_recycle_experience_mod(),
                                          discard_excess_resources,
                                          get_parts_to_skip(p.vessel)))
                yield return result;
            recycling = null;
        }

        class SkipPart : CustomYieldInstruction
        {
            public override bool keepWaiting => false;
        }

        IEnumerable recycle(Part p, float efficiency, bool discard_excess_resources, HashSet<uint> skip_craftIDs = null)
        {
            this.Log("Recycling {}", p.GetID());//debug
            // first handle children
            var skip = false;
            if(p.children.Count > 0)
            {
                for(int i = p.children.Count-1; i >= 0; i--)
                {
                    foreach(var child_result in recycle(p.children[i], efficiency, discard_excess_resources, skip_craftIDs))
                    {
                        if(child_result is SkipPart)
                            skip = true;
                        else
                        {
                            yield return child_result;
                            if(child_result == null)
                                yield break;
                        }
                    }
                }
            }
            // decide if we have to skip this part (and thus all the parents)
            skip |= p.protoModuleCrew.Count > 0;
            skip |= skip_craftIDs != null && skip_craftIDs.Contains(p.craftID);
            if(!skip)
            {
                // if not, collect its resources if we can, else skip it anyway
                foreach(var res in p.Resources)
                {
                    if(res.amount > 0)
                    {
                        if(transfer_resource(p, res.info.id, res.amount, discard_excess_resources) != TransferState.FULL)
                            skip = true;
                    }
                }
            }
            this.Log("Recycling {}: skip {}", p.GetID(), skip);//debug
            if(skip)
            {
                yield return new SkipPart();
                yield break;
            }
            // recycle the part that is now empty
            var kit = new PartKit(p, false);
            var req_a = kit.RemainingRequirements().Copy();
            kit.SetStageComplete(DIYKit.ASSEMBLY, true);
            var req_c = kit.RemainingRequirements().Copy();
            var result = recycle_part(p, efficiency, discard_excess_resources, req_a, req_c);
            if(result > TransferState.ZERO)
            {
                this.Log("Recycled {}, result {}", p.GetID(), result);//debug
                FXMonger.Explode(p, p.transform.position, 0);
                p.Die();
            }
            else
                this.Log("Couldn't recycle {}, result {}", p.GetID(), result);//debug
            // wait for some time before recycle next one, or break the recycling if something went wrong
            if(result == TransferState.NO_EC)
                yield return new SkipPart();
            else if(result == TransferState.FULL)
                yield return new WaitForSeconds(GLB.RecycleRate);
            else
                yield return null;
        }

        [Flags] enum TransferState { NOOP = 0, NO_EC = 1 << 0, ZERO = 1 << 1, PARTIAL = 1 << 2, FULL = 1 << 3 }; 

        TransferState transfer_resource(Part from, int id, double amount, bool discard_excess_resources)
        {
            if(amount.Equals(0))
                return TransferState.FULL;
            this.Log("Transferring [{}]: {}", id, amount);//debug
            var remainder = amount;
            if(from.vessel != vessel)
                remainder += part.RequestResource(id, -amount, ResourceFlowMode.ALL_VESSEL_BALANCE);
            else
            {
                foreach(var p in vessel.Parts)
                {
                    if(p != from)
                    {
                        var part_res = p.Resources.GetFlowing(id, false);
                        if(part_res != null && part_res.amount < part_res.maxAmount)
                        {
                            remainder += part.TransferResource(part_res, amount, p);
                            if(remainder <= 0)
                                break;
                        }
                    }
                }
            }
            this.Log("Remainder [{}]: {}", id, remainder);//debug
            if(discard_excess_resources || Math.Abs(remainder) < 1e-6)
                return TransferState.FULL;
            if(remainder.Equals(amount))
                return TransferState.ZERO;
            return TransferState.PARTIAL;
        }

        TransferState recycle_part(Part from, float efficiency, bool discard_excess_resources, params DIYKit.Requirements[] requrements)
        {
            var ec = 0.0;
            var ec_req = 0.0;
            foreach(var req in requrements)
            {
                if(!req) continue;
                ec_req = req.energy * req.resource.MaxRecycleRatio * efficiency;
            }
            var result = TransferState.NOOP;
            if(ec_req > 0)
            {
                ec = part.RequestResource(Utils.ElectricCharge.id, ec_req);
                if(ec < ec_req)
                {
                    Utils.Message("Not enougth energy to fully recycle '{0}'\n{1} of electric charge required", 
                                  from.Title(), Utils.formatBigValue((float)ec_req, "u"));
                    part.RequestResource(Utils.ElectricCharge.id, -ec);
                    result = TransferState.NO_EC;
                }
                else
                    this.Log("Using {}/{} of EC recycle {}", ec, ec_req, from.GetID());//debug
            }
            if(result != TransferState.NO_EC)
            {
                foreach(var req in requrements)
                {
                    if(!req) continue;
                    result |= transfer_resource(from, req.resource.id,
                                                req.resource_amount * req.resource.MaxRecycleRatio * efficiency,
                                                discard_excess_resources);
                    if(result != TransferState.FULL)
                        Utils.Message("No space left for '{0}'", req.resource.def.name);
                }
                if(ec > 0 && result == TransferState.ZERO)
                    part.RequestResource(Utils.ElectricCharge.id, -ec);
            }
            return result;
        }

        float get_recycle_experience_mod()
        {
            var experience = 0;
            foreach(var kerbal in part.protoModuleCrew)
            {
                var trait = kerbal.experienceTrait;
                for(int i = 0, traitEffectsCount = trait.Effects.Count; i < traitEffectsCount; i++)
                    if(worker_effect.IsInstanceOfType(trait.Effects[i]))
                    {
                        experience = Math.Max(experience, trait.CrewMemberExperienceLevel());
                        break;
                    }
            }
            this.Log("Recycle experience modificator: {}", Math.Max(experience, 0.5f) / KerbalRoster.GetExperienceMaxLevel());//debug
            return Math.Max(experience, 0.5f) / KerbalRoster.GetExperienceMaxLevel();
        }
        #endregion

        #region GUI
        public override string Stage_Display => "CONSTRUCTION";

        protected override void unbuilt_kits_pane()
        {
            if(unbuilt_kits.Count == 0) return;
            GUILayout.Label("Available DIY kits:", Styles.label, GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(Styles.white);
            BeginScroll(unbuilt_kits.Count, ref unbuilt_scroll);
            ConstructionKitInfo add = null;
            IDeployable deploy = null;
            foreach(var info in unbuilt_kits)
            {
                GUILayout.BeginHorizontal();
                draw_task(info);
                if(info.ConstructionSpace is IDeployable depl)
                {
                    if(depl.State == DeplyomentState.DEPLOYED)
                    {
                        if(GUILayout.Button(new GUIContent("Add", "Add this kit to construction queue"),
                                            Styles.enabled_button, GUILayout.ExpandWidth(false),
                                            GUILayout.ExpandHeight(true)))
                            add = info;
                    }
                    else if(depl.State != DeplyomentState.DEPLOYING)
                    {
                        if(GUILayout.Button(new GUIContent("Deploy", "Deploy this kit and fix it to the ground"),
                                            Styles.active_button, GUILayout.ExpandWidth(false),
                                            GUILayout.ExpandHeight(true)))
                            deploy = depl;
                    }
                    else
                        GUILayout.Label(DeployableStatus(depl), Styles.boxed_label,
                                        GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                }
                else if(GUILayout.Button(new GUIContent("Add", "Add this kit to construction queue"),
                                         Styles.enabled_button, GUILayout.ExpandWidth(false),
                                         GUILayout.ExpandHeight(true)))
                    add = info;
                GUILayout.EndHorizontal();
            }
            if(add != null)
                Queue.Enqueue(add);
            else if(deploy != null)
                deploy.Deploy();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        protected override void built_kits_pane()
        {
            if(built_kits.Count == 0) return;
            GUILayout.Label("Built DIY kits:", Styles.label, GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(Styles.white);
            BeginScroll(built_kits.Count, ref built_scroll);
            ConstructionKitInfo crew = null;
            ConstructionKitInfo resources = null;
            ConstructionKitInfo launch = null;
            foreach(var info in built_kits)
            {
                GUILayout.BeginHorizontal();
                draw_task(info);
                set_highlighted_task(info);
                if(GUILayout.Button(new GUIContent("Resources", "Transfer resources between the workshop and the assembled vessel"),
                                    Styles.active_button, GUILayout.ExpandWidth(false)))
                    resources = info;
                if(GUILayout.Button(new GUIContent("Crew", "Select crew for the assembled vessel"),
                                    Styles.active_button, GUILayout.ExpandWidth(false)))
                    crew = info;
                if(GUILayout.Button(new GUIContent("Launch", "Launch assembled vessel"),
                                    Styles.danger_button, GUILayout.ExpandWidth(false)))
                    launch = info;
                GUILayout.EndHorizontal();
            }
            if(resources != null)
                setup_resource_transfer(resources);
            if(crew != null)
                setup_crew_transfer(crew);
            if(launch != null && launch.Recheck())
                launch.ConstructionSpace.Launch();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        Vector2 recycle_vessels_scroll;
        void draw_recycle_pane()
        {
            recycle_vessels_scroll = GUILayout.BeginScrollView(recycle_vessels_scroll, Styles.white, GUILayout.Height(100));
            foreach(var vsl in get_recyclable_vessels())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(vsl.GetDisplayName(), Styles.boxed_label, GUILayout.ExpandWidth(true));
                if(vsl == recycling)
                    GUILayout.Label("Recycling in progress...", Styles.boxed_label, GUILayout.ExpandWidth(true));
                else
                {
                    if(GUILayout.Button("Recycle", Styles.danger_button, GUILayout.ExpandWidth(false)))
                        StartCoroutine(recycle(vsl, false));
                    if(GUILayout.Button("Recycle (discard resources)", Styles.danger_button, GUILayout.ExpandWidth(false)))
                        StartCoroutine(recycle(vsl, true));
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        protected override void draw_panes()
        {
            base.draw_panes();
            draw_recycle_pane();
        }
        #endregion
    }
}
