//   DockedKitContainer.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
    public class DockedKitContainer : DeployableKitContainer
    {
        [KSPField, SerializeField]
        public SpawnSpaceManager SpawnManager = new SpawnSpaceManager();

        [KSPField]
        public string ConstructionNode = "bottom";

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            SpawnManager.Init(part);
            SpawnManager.SetupSensor();
        }

        #region Deployment
        public override bool CanConstruct(VesselKit kit) => !kit.HasLaunchClamps;

        protected override bool can_deploy()
        {
            if(!base.can_deploy())
                return false;
            if(vessel.angularVelocity.sqrMagnitude > GLB.DeployMaxAV)
            {
                Utils.Message("Cannot deploy the kit while the vessel is rotating.");
                return false;
            }
            return true;
        }

        protected override Transform get_deploy_transform() =>
        SpawnManager.GetSpawnTransform() ?? part.transform;

        protected override Vector3 get_deployed_offset() => SpawnManager.GetSpawnOffset(Size);

        protected override Vector3 get_deployed_size() => kit.ShipMetric.size;

        Part get_construction_part()
        {
            //try to find part connected though the construction node
            Part cpart = null;
            var cnode = part.FindAttachNode(ConstructionNode);
            if(cnode != null)
                cpart = cnode.attachedPart;
            if(cpart == null)
            {
                foreach(var port in part.FindModulesImplementing<ModuleDockingNode>())
                {
                    if(port.nodeTransformName == ConstructionNode
                       || port.referenceAttachNode == ConstructionNode)
                    {
                        cpart = vessel[port.dockedPartUId];
                        break;
                    }
                }
            }
            return cpart;
        }

        protected override IEnumerable prepare_deployment()
        {
            foreach(var i in base.prepare_deployment())
                yield return i;
            var cpart = get_construction_part();
            //decouple all parts but the one on the construction node
            if(part.parent != null && part.parent != cpart)
            {
                part.decouple(2);
                yield return null;
            }
            while(part.children.Count > 0)
            {
                var child = part.children[0];
                if(child != cpart)
                {
                    child.decouple(2);
                    yield return null;
                }
            }
        }
        #endregion

        protected override IEnumerator<YieldInstruction> launch(ShipConstruct construct)
        {
            var bounds = new Metric(construct, world_space:true).bounds;
            yield return
                StartCoroutine(vessel_spawner
                               .SpawnShipConstruct(construct,
                                                   SpawnManager.GetSpawnTransform(bounds),
                                                   SpawnManager.GetSpawnOffset(bounds) 
                                                   - bounds.center 
                                                   + construct.Parts[0].localRoot.transform.position,
                                                   Vector3.zero,
                                                   null, 
                                                   on_vessel_loaded,
                                                   null, 
                                                   on_vessel_launched));
        }

        #if DEBUG
        void OnRenderObject()
        {
            if(vessel == null) return;
            var T = SpawnManager.GetSpawnTransform();
            if(T != null)
            {
                var pos = T.position+T.TransformDirection(SpawnManager.GetSpawnOffset(Size));
                Utils.GLVec(pos, T.up, Color.green);
                Utils.GLVec(pos, T.forward, Color.blue);
                Utils.GLVec(pos, T.right, Color.red);
            }
            if(part.attachJoint != null)
            {
                var j = part.attachJoint;
                if(j.Host != null)
                    Utils.GLDrawPoint(j.Host.transform.TransformPoint(j.HostAnchor), Color.magenta);
            }
        }
        #endif
    }
}
