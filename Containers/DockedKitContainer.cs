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

        //public ModuleDockingNode FindNodeApproaches(ModuleDockingNode m1)
        //{
        //    if (m1.part.packed) return null;
        //    int count1 = FlightGlobals.VesselsLoaded.Count;
        //    while (count1-- > 0)
        //    {
        //        Vessel vsl = FlightGlobals.VesselsLoaded[count1];
        //        if (vsl.packed)  continue;
        //        int count2 = vsl.dockingPorts.Count;
        //        if (count2 == 0)  continue;
        //        int index = count2;
        //        while (index-- > 0)
        //        {
        //            PartModule dockingPort = vsl.dockingPorts[index];
        //            if(dockingPort.part != part)
        //            {
        //                if (dockingPort.part != null)
        //                {
        //                    m1.Log("Checking {}: DEAD {}, is DockingNode {}",
        //                           dockingPort, dockingPort.part.State == PartStates.DEAD, dockingPort is ModuleDockingNode);
        //                    if(dockingPort.part.State == PartStates.DEAD) continue;
        //                    if(!(dockingPort is ModuleDockingNode)) continue;
        //                    var m2 = dockingPort as ModuleDockingNode;
        //                    m1.Log("{}.state {}, gendered {}:{}, snapRot {}:{}, snapOff {}:{}", 
        //                           m2, m2.state,
        //                           m1.gendered, m2.gendered,
        //                           m1.snapRotation, m2.snapRotation,
        //                           m1.snapOffset, m2.snapOffset);
        //                    if(m2.state != m1.st_ready.name) continue;
        //                    var types_match = true;
        //                    var enumerator = m1.nodeTypes.GetEnumerator();
        //                    while(enumerator.MoveNext())
        //                        types_match &= !m2.nodeTypes.Contains(enumerator.Current);
        //                    m1.Log("{}.types_match {}, {}", m2, types_match, m2.nodeTypes);
        //                    if(types_match) continue;
        //                    if(m2.gendered != m1.gendered) continue;
        //                    if(m1.gendered)
        //                    {
        //                        if(m2.genderFemale == m1.genderFemale) continue;
        //                    }
        //                    if(m2.snapRotation != m1.snapRotation) continue;
        //                    if(m1.snapRotation)
        //                    {
        //                        if(m2.snapOffset != m1.snapOffset) continue;
        //                    }
        //                    if(m1.CheckDockContact(m1, m2, m1.acquireRange, m1.acquireMinFwdDot, m1.acquireMinRollDot))
        //                    {
        //                        Utils.GLLine(m1.nodeTransform.position, m2.nodeTransform.position, Color.green);
        //                        return m2;
        //                    }
        //                    Utils.GLLine(m1.nodeTransform.position, m2.nodeTransform.position, Color.red);
        //                }
        //            }
        //        }
        //    }
        //    return null;
        //}

        #region Deployment
        protected override Transform get_deploy_transform() =>
        SpawnManager.GetSpawnTransform() ?? part.transform;

        protected override Vector3 get_deployed_offset() => SpawnManager.GetSpawnOffset(Size);

        protected override Vector3 get_deployed_size() => kit.ShipMetric.size;

        protected override IEnumerable prepare_deployment()
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
            foreach(var node in part.attachNodes)
            {
                Utils.GLDrawPoint(node.nodeTransform.TransformPoint(node.position), Color.green);
            }
        }
        #endif
    }
}
