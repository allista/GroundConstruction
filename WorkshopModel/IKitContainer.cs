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
        bool Valid { get; }
        List<VesselKit> GetKits();
        VesselKit GetKit(Guid id);
    }

    public interface IAssemblySpace : IKitContainer, IPartCostModifier, IPartMassModifier
    {
        new bool Valid { get; }
        bool CheckKit(VesselKit vessel_kit, string part_name, out float kit2space_ratio);
        void SetKit(VesselKit vessel_kit, string part_name);
        void SpawnKit();
    }

    public interface IConstructionSpace : IKitContainer, IPartCostModifier, IPartMassModifier
    {
        new bool Valid { get; }
        bool ConstructionComplete { get; }
        bool CanConstruct(VesselKit vessel_kit);
        void Launch();
    }

    public enum DeploymentState {
        IDLE,
        DEPLOYING,
        DEPLOYED,
    }

    public interface IDeployable : IJointLockState
    {
        DeploymentState State { get; }
        string DeploymentInfo { get; }
        double DeploymentETA { get; }
        void Deploy();
    }

    public interface IConfigurable
    {
        bool IsConfigurable { get; }
        void DrawOptions();
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
        bool HasAnimator { get; }
    }

    public interface IContainerProducer
    {
        void SpawnEmptyContainer(string part_name);
    }
}

