//   VesselKitTask.cs
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
    public abstract class VesselKitInfo : ConfigNodeObject, IWorkshopTask
    {
        [Persistent] public Guid kitID;
        [Persistent] public Guid vesselID;

        public VesselKit Kit { get; protected set; }
        public PartModule Module => Kit.Host;
        public string Name => Kit ? Kit.Name : "";
        public Guid ID => kitID;
        public bool Valid => Kit;
        public abstract bool Complete { get; }

        public IKitContainer Container => Kit.Host as IKitContainer;
        public IControllableContainer ControllableContainer => Kit.Host as IControllableContainer;

        public bool Recheck()
        {
            if(Kit == null)
                Kit = FindKit();
            return Kit;
        }

        public abstract VesselKit FindKit();

        protected VesselKitInfo() { }
        protected VesselKitInfo(VesselKit kit)
        {
            Kit = kit;
            kitID = kit.id;
            vesselID = kit.Valid && kit.Host.vessel != null ? kit.Host.vessel.id : Guid.Empty;
        }

        public void Draw() { if(Valid) Kit.Draw(); }
        public void DrawCurrentPart()
        {
            if(Valid)
            {
                var part = Kit.CurrentJob;
                if(part != null)
                    part.Draw();
            }
        }

        public static List<T> GetKitContainers<T>(Vessel vsl) where T : class, IKitContainer =>
        vsl != null ? vsl.Parts.SelectMany(p => p.Modules.GetModules<T>()).ToList() : null;

        public VesselKit FindKit<T>() where T : class, IKitContainer
        {
            var containers = GetKitContainers<T>(FlightGlobals.FindVessel(vesselID));
            if(containers != null) 
            {
                var container = containers.Find(c => c.GetKit(kitID) != null);
                if(container != null)
                    return container.GetKit(kitID);
            }
            return null;
        }
    }

    public class ConstructionKitInfo : VesselKitInfo
    {
        public ConstructionKitInfo() {}
        public ConstructionKitInfo(VesselKit kit) : base(kit) {}

        public override bool Complete => Recheck() && Kit.Complete;

        public IConstructionSpace ConstructionSpace => Kit.Host as IConstructionSpace;
        public override VesselKit FindKit() => FindKit<IConstructionSpace>();
    }

    public class AssemblyKitInfo : VesselKitInfo
    {
        public AssemblyKitInfo() {}
        public AssemblyKitInfo(VesselKit kit) : base(kit) {}

        public override bool Complete => Recheck() && Kit.StageComplete(DIYKit.ASSEMBLY);

        public IAssemblySpace AssemblySpace => Kit.Host as IAssemblySpace;
        public override VesselKit FindKit() => FindKit<IKitContainer>();
    }
}

