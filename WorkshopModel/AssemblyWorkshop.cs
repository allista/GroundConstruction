//   AssemblyWorkshop.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using System.Collections.Generic;
using AT_Utils;
using KSP.UI.Screens;
using UnityEngine;

namespace GroundConstruction
{
    public abstract class AssemblyWorkshop : VesselKitWorkshop
    {
        protected override int STAGE { get { return DIYKit.ASSEMBLY; } }

        #region implemented abstract members of WorkshopBase
        protected override bool check_task(VesselKitInfo task)
        {
            return base.check_task(task) && task.Kit.CurrentStageIndex == DIYKit.ASSEMBLY;
        }
        #endregion

        protected virtual void create_kit(ShipConstruct construct)
        {
            
        }

        ShipConstructLoader construct_loader;

        public override void OnAwake()
        {
            base.OnAwake();
            construct_loader = gameObject.AddComponent<ShipConstructLoader>();
            construct_loader.process_construct = create_kit;
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
	}
}

