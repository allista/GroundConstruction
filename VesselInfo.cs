//   VesselInfo.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri

using System;
using AT_Utils;

namespace GroundConstruction
{
    public class VesselInfo : ConfigNodeObject
    {
        [Persistent] public Guid vesselID;

        public bool Valid { get { return vesselID != Guid.Empty; } }
        public bool IsActive { get { return FlightGlobals.ActiveVessel != null && vesselID == FlightGlobals.ActiveVessel.id; } }
        public Vessel GetVessel() { return FlightGlobals.FindVessel(vesselID); }

        public override void Save(ConfigNode node)
        {
            node.AddValue("vesselID", vesselID.ToString("N"));
            base.Save(node);
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            var svid = node.GetValue("vesselID");
            vesselID = string.IsNullOrEmpty(svid)? Guid.Empty : new Guid(svid);
        }

        public VesselInfo() {}

        protected VesselInfo(VesselInfo other)
        {
            vesselID = other.vesselID;
        }

        public VesselInfo Clone()
        {
            return new VesselInfo(this);
        }
    }
}

