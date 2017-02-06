//   CategoryFilter.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;
using AT_Utils;

namespace GroundConstruction
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class GCFilterManager : SimplePartFilter
    {
        public GCFilterManager()
        {
            SUBCATEGORY = "Ground Construction";
            FOLDER = "GroundConstruction/Icons";
            ICON = "GC-category";
            MODULES = new List<Type>{typeof(GroundWorkshop), typeof(ModuleConstructionKit)};
        }

        protected override bool filter(AvailablePart part)
        {
            if(part.partPrefab != null)
            {
                if(part.partPrefab.Modules.GetModule<ModuleConstructionKit>() != null) return true;
                var workshop = part.partPrefab.Modules.GetModule<GroundWorkshop>();
                return workshop != null && workshop.isEnabled && workshop.Efficiency > 0;
            }
            return part.moduleInfos.Any(m => MODULES.Any(t => t.Name == m.moduleName));
        }
    }
}
