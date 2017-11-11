//   AssemblyLineBase.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using UnityEngine;
using KSP.UI.Screens;
using AT_Utils;

namespace GroundConstruction
{
    public abstract class AssemblyLineBase : WorkshopBase
    {
        SubassemblySelector subassembly_selector;
        CraftBrowserDialog vessel_selector;
        EditorFacility facility;

        protected abstract bool request_assembly_space(ShipConstruct ship,
                                                       Callback<IAssemblySpace> space_available,
                                                       Callback<string> space_unavailable);

        protected ShipConstruct create_kit(ShipConstruct content) {}

        void OnAwake()
        {
            base.OnAwake();
            subassembly_selector = gameObject.AddComponent<SubassemblySelector>();
        }

        void OnDestroy()
        {
            Destroy(subassembly_selector);
        }
    }
}

