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
        protected override Transform get_deploy_transform() =>
        SpawnManager.GetSpawnTransform() ?? part.transform;

        protected override Vector3 get_deployed_size() => kit.ShipMetric.size;

        protected override IEnumerable prepare_deployment()
        {
            Part cpart = null;
            var cnode = part.FindAttachNode(ConstructionNode);
            if(cnode != null)
                cpart = cnode.attachedPart;
            if(part.parent != null && part.parent != cpart) 
            {
                part.decouple();
                yield return null;
            }
            while(part.children.Count > 0)
            {
                var child = part.children[0];
                if(child != cpart)
                {
                    child.decouple();
                    yield return null;
                }
            }
        }
        #endregion

        protected override IEnumerator<YieldInstruction> launch(ShipConstruct construct)
        {
            var bounds = construct.Bounds(construct.Parts[0].localRoot.transform);
            yield return
                StartCoroutine(vessel_spawner
                               .SpawnShipConstruct(construct,
                                                   SpawnManager.GetSpawnTransform(bounds),
                                                   SpawnManager.GetSpawnOffset(bounds) - bounds.center,
                                                   Vector3.zero,
                                                   null, 
                                                   on_vessel_loaded,
                                                   null, 
                                                   on_vessel_launched));
        }
    }
}
