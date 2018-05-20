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
    public interface IKitContainer
    {
        bool Empty { get; }
        List<VesselKit> GetKits();
        VesselKit GetKit(Guid id);
    }

    public interface IConstructionSpace : IKitContainer
    {
        void Launch();
        void EnableLaunchControls(bool enable = true);
    }

    public enum ContainerDeplyomentState {
        IDLE,
        DEPLOYING,
        DEPLOYED,
    }

    public interface IDeployableContainer : IConstructionSpace
    {
        ContainerDeplyomentState State { get; }
        void Deploy();

    }

    public interface IAssemblySpace : IKitContainer
    {
        float KitToSpaceRatio(VesselKit kit);
        AssemblyKitInfo SetKit(VesselKit kit);
        void SpawnKit();
    }
}

