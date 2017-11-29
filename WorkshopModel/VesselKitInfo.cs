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
    public class VesselKitInfo : ConfigNodeObject, IWorkshopTask
    {
        [Persistent] public Guid kitID;
        [Persistent] public Guid vesselID;

        public VesselKit Kit { get; private set; }

        public PartModule Module { get { return Kit.Host; } }

        public IKitContainer Container { get { return Kit.Host as IKitContainer; } }

        public string Name { get { return Kit ? Kit.Name : ""; } }

        public bool Recheck()
        {
            if(Kit == null)
                Kit = FindKit();
            return Kit;
        }

        public Guid ID { get { return kitID; } }

        public bool Valid { get { return Kit; } }

        public bool Complete { get { return Recheck() && Kit.Complete; } }

        public VesselKitInfo()
        {
        }

        public VesselKitInfo(VesselKit kit)
        {
            Kit = kit;
            kitID = kit.id;
            vesselID = kit.Valid && kit.Host.vessel != null ? kit.Host.vessel.id : Guid.Empty;
        }

        public static List<IKitContainer> GetKitContainers(Vessel vsl)
        {
            if(vsl != null)
                return vsl.Parts.SelectMany(p => p.Modules.GetModules<IKitContainer>()).ToList();
            return null;
        }

        public VesselKit FindKit()
        {
            var containers = GetKitContainers(FlightGlobals.FindVessel(vesselID));
            if(containers != null) 
            {
                var container = containers.Find(c => c.GetKit(kitID) != null);
                if(container != null)
                    return container.GetKit(kitID);
            }
            return null;
        }

        public override string ToString()
        {
            if(Valid)
                return string.Format("\"{0}\" ", Kit.Name) + Kit.RequirementsStatus();
            return "";
        }

        public string ContainerStatus
        {
            get
            {
                var container = Container;
                if(container != null) 
                {
                    switch(container.State)
                    {
                    case ContainerState.IDLE:
                        return "Idle";
                    case ContainerState.DEPLOYING:
                        return "Deploying...";
                    case ContainerState.DEPLOYED:
                        return "Deployed";
                    }
                }
                return "";
            }
        }

//        public string KitStatus
//        {
//            get
//            { 
//                var container = Container;
//                if(container == null) return "";
//                switch(container.State)
//                {
//                case ContainerState.DEPLOYING:
//                    return "Deploying...";
//                case ContainerState.DEPLOYED:
//                    return "Deployed";
//                case ContainerState.ASSEMBLING:
//                    return string.Format("Assembly: {0:P1}", Kit.Current.Completeness);
//                case ContainerState.CONSTRUCTING:
//                    return string.Format("Construction: {0:P1}", Kit.Current.Completeness);
//                default:
//                    return "Idle";
//                }
//            }
//        }

//        public string RequirementsStatus
//        { 
//            get
//            {
//                if(!Kit)
//                    return "";
//                ResourceUsageInfo resource;
//                double work, energy, res_amount;
//                work = Kit.RemainingRequirements(out energy, out resource, out res_amount) / 3600;
//                return string.Format("Reqires: {0} {1}, {2}, {3} SKH",
//                                     Utils.formatBigValue((float)res_amount, "u"), resource.name,
//                                     Utils.formatBigValue((float)energy, "EC"), work);
//            }
//        }
    }
}

