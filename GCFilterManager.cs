//   CategoryFilter.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri

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
            SetMODULES(new []{
                typeof(GroundWorkshop), 
                typeof(SingleVesselConstructionWorkshop), 
                typeof(SingleVesselAssemblyWorkshop), 
                typeof(SinglePartAssemblyWorkshop), 
                typeof(AssemblySpace), 
                typeof(ModuleConstructionKit)});
        }



        protected override bool filter(AvailablePart part)
        {
            if(part.partPrefab != null)
            {
                if(check_module(part.partPrefab.FindModuleImplementing<IKitContainer>() as PartModule)) 
                    return true;
                if(check_module(part.partPrefab.FindModuleImplementing<WorkshopBase>())) 
                    return true;
            }
            return base.filter(part);
        }
    }
}
