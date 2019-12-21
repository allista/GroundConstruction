//   CategoryFilter.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri

using AT_Utils;

namespace GroundConstruction
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class GCFilterManager : SimplePartFilter
    {
        public GCFilterManager()
        {
            SUBCATEGORY = "Global Construction";
            FOLDER = "GroundConstruction/Icons";
            ICON = "GC-category";
        }

        protected override bool filter(AvailablePart part)
        {
            if(part.category != PartCategories.none)
            {
                if(part.partPrefab != null)
                {
                    if(check_module(part.partPrefab.FindModuleImplementing<IKitContainer>() as PartModule))
                        return true;
                    if(check_module(part.partPrefab.FindModuleImplementing<WorkshopBase>()))
                        return true;
                }
            }
            return false;
        }
    }
}
