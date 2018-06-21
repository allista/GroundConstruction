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

    public interface IAssemblySpace : IKitContainer, IPartCostModifier, IPartMassModifier
    {
        float KitToSpaceRatio(VesselKit kit);
        void SetKit(VesselKit kit);
        void SpawnKit();
    }

    public interface IConstructionSpace : IKitContainer, IPartCostModifier, IPartMassModifier
    {
        void Launch();
    }

    public enum DeplyomentState {
        IDLE,
        DEPLOYING,
        DEPLOYED,
    }

    public interface IDeployable
    {
        DeplyomentState State { get; }
        void Deploy();
    }

    public interface IControllable
    {
        void ShowUI(bool enable = true);
        void EnableControls(bool enable = true);        
    }

    public interface IAnimatedSpace
    {
        void Open();
        void Close();
        bool Opened { get; }
    }
}

