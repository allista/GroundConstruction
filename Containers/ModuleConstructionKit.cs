//   ModuleConstructionKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
    public class ModuleConstructionKit : DeployableKitContainer
    {
        List<Transform> spawn_transforms;
        [KSPField] public string SpawnTransforms;

        ATGroundAnchor anchor;

        Transform get_spawn_transform()
        {
            Transform minT = null;
            var alt = double.MaxValue;
            foreach(var T in spawn_transforms)
            {
                var t_alt = vessel.mainBody.GetAltitude(T.position) - vessel.mainBody.TerrainAltitude(T.position);
                if(t_alt < alt) { alt = t_alt; minT = T; }
            }
            return minT;
        }


        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            anchor = part.FindModuleImplementing<ATGroundAnchor>();
            spawn_transforms = new List<Transform>();
            if(!string.IsNullOrEmpty(SpawnTransforms))
            {
                foreach(var t in Utils.ParseLine(SpawnTransforms, Utils.Whitespace))
                {
                    var transforms = part.FindModelTransforms(t);
                    if(transforms == null || transforms.Length == 0) continue;
                    spawn_transforms.AddRange(transforms);
                }
            }
        }

        #region Deployment
        protected override Transform get_deploy_transform() =>
        get_spawn_transform() ?? part.transform;

        protected override Vector3 get_deployed_size()
		{
			var size = kit.ShipMetric.size;
			if(Facility == EditorFacility.SPH) 
				size = new Vector3(size.x, size.z, size.y);
			return size;
		}

        protected override bool can_deploy()
        {
            if(!base.can_deploy())
                return false;
            if(!vessel.Landed)
            {
                Utils.Message("Cannot deploy construction kit unless landed.");
                return false;
            }
            if(vessel.srfSpeed > GLB.DeployMaxSpeed)
            {
                Utils.Message("Cannot deploy construction kit while mooving.");
                return false;
            }
            if(vessel.angularVelocity.sqrMagnitude > GLB.DeployMaxAV)
            {
                Utils.Message("Cannot deploy construction kit while rotating.");
                return false;
            }
            return true;
        }

        bool kit_is_settled
        {
            get
            {
                return vessel.srfSpeed < GLB.DeployMaxSpeed &&
                    vessel.angularVelocity.sqrMagnitude < GLB.DeployMaxAV;
            }
        }

        RealTimer settled_timer = new RealTimer(3);
        ActionDamper message_damper = new ActionDamper(1);
        IEnumerable wait_for_ground_contact(string wait_message)
        {
            settled_timer.Reset();
            while(!settled_timer.RunIf(part.GroundContact && kit_is_settled))
            {
                if(!part.GroundContact)
                    message_damper.Run(() => Utils.Message(1, "{0} Kit: no ground contact!", kit.Name));
                else if(!kit_is_settled)
                    message_damper.Run(() => Utils.Message(1, "{0} Kit is moving...", kit.Name));
                else message_damper.Run(() => Utils.Message(1, "{0} {1:F1}s", wait_message, settled_timer.Remaining));
                yield return null;
            }
        }

        protected override IEnumerable prepare_deployment()
        {
            if(part.parent) part.decouple();
            yield return null;
            while(part.children.Count > 0)
            {
                part.children[0].decouple();
                yield return null;
            }
            foreach(var _ in wait_for_ground_contact(string.Format("Deploing {0} Kit in", kit.Name)))
                yield return null;
        }

        protected override IEnumerable finalize_deployment()
        {
            foreach(var _ in base.finalize_deployment())
                yield return null;
            foreach(var _ in wait_for_ground_contact(string.Format("Fixing {0} Kit in", kit.Name)))
                yield return null;
            if(anchor != null)
                anchor.ForceAttach();
            Utils.Message(6, "{0} is deployed and fixed to the ground.", vessel.vesselName);
        }
        #endregion
        
        #region Launching
        protected override bool can_launch()
        {
            if(!base.can_launch())
                return false;
            if(!vessel.Landed)
            {
                Utils.Message("Cannot launch constructed ship unless landed.");
                return false;
            }
            if(vessel.srfSpeed > GLB.DeployMaxSpeed)
            {
                Utils.Message("Cannot launch constructed ship while mooving.");
                return false;
            }
            if(vessel.angularVelocity.sqrMagnitude > GLB.DeployMaxAV)
            {
                Utils.Message("Cannot launch constructed ship while rotating.");
                return false;
            }
            return true;
        }

        protected override IEnumerator<YieldInstruction> launch(ShipConstruct construct)
        {
            var launch_transform = get_spawn_transform();
            yield return 
                StartCoroutine(vessel_spawner
                               .SpawnShipConstructToGround(construct, launch_transform, Vector3.zero,
                                                           null, 
                                                           on_vessel_loaded,
                                                           null,
                                                           on_vessel_launched,
                                                           GLB.EasingFrames));
        }
        #endregion

        #if DEBUG
        void OnRenderObject()
        {
            if(vessel == null || spawn_transforms == null) return;
            var T = get_spawn_transform();
            if(T != null)
            {
                Utils.GLVec(T.position, T.up, Color.green);
                Utils.GLVec(T.position, T.forward, Color.blue);
                Utils.GLVec(T.position, T.right, Color.red);
            }
        }
        #endif
    }
}
