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
        string Name { get; }
        bool Empty { get; }
        List<VesselKit> GetKits();
        VesselKit GetKit(Guid id);
    }

    public interface IControllableContainer : IKitContainer
    {
        void ShowUI(bool enable = true);
        void EnableControls(bool enable = true);        
    }

    public interface IAssemblySpace : IControllableContainer
    {
        float KitToSpaceRatio(VesselKit kit);
        void SetKit(VesselKit kit);
        void SpawnKit();
    }

    public interface IConstructionSpace : IControllableContainer
    {
        void Launch();
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
}

