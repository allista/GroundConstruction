//   IAssemblySpace.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using UnityEngine;

namespace GroundConstruction
{
    public interface IAssemblySpace
    {
        Vector3 GetMaxDimensions();
        bool StartAssembly(ShipConstruct ship);
        bool UpdateProgress(float delta);
        void SpawnShip();
    }
}

