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

        protected VesselInfo() {}

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

