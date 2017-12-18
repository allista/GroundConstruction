//   ConstructionWorkshop.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System.Collections.Generic;
using AT_Utils;

namespace GroundConstruction
{
    public abstract class ConstructionWorkshop : VesselKitWorkshop
    {
        protected override int STAGE { get { return DIYKit.CONSTRUCTION; } }

        #region Target Actions
        protected VesselKitInfo target_kit;
        protected virtual bool check_target_kit(VesselKitInfo target)
        {
            return target.Recheck() && target.Complete;
        }
        #endregion

        #region Resource Transfer
        readonly ResourceManifestList transfer_list = new ResourceManifestList();
        VesselResources host_resources, kit_resources;
        ResourceTransferWindow resources_window;

        protected void setup_resource_transfer(VesselKitInfo target)
        {
            target_kit = null;
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

        protected void setup_crew_transfer(VesselKitInfo target)
        {
            check_target_kit(target);
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

        protected override bool check_task(VesselKitInfo task)
        {
            return base.check_task(task) && task.Kit.CurrentStageIndex == DIYKit.CONSTRUCTION;
        }

        protected override void on_task_complete(VesselKitInfo task)
        {
            var container = task.Container;
            if(container != null)
                container.EnableLaunchControls();
        }

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
    }
}
