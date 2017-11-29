//   IKitContainer.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using System.Collections.Generic;

namespace GroundConstruction
{
    public enum ContainerState {
        IDLE,
        DEPLOYING,
        DEPLOYED,
    }

    public interface IKitContainer
    {
        ContainerState State { get; }
        List<VesselKit> GetKits();
        VesselKit GetKit(Guid id);
        void Deploy();
        void Launch();
        void EnableLaunchControls(bool enable = true);
    }
}

