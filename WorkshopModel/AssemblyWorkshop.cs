//   AssemblyWorkshop.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using AT_Utils;
using UnityEngine;

namespace GroundConstruction
{
    public abstract class AssemblyWorkshop : VesselKitWorkshop
    {
        #region implemented abstract members of WorkshopBase
        protected override bool check_task(VesselKitInfo task)
        {
            return base.check_task(task) && task.Kit.Current == task.Kit.Assembly;
        }

        #endregion
    }
}

