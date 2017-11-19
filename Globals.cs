//   Globals.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Collections.Generic;
using AT_Utils;

namespace GroundConstruction
{
    public class ResourceUsageInfo : ResourceInfo
    {
        [Persistent] public float ComplexityWork = 0;
        [Persistent] public float EnergyPerMass = 0;
        [Persistent] public float WorkPerMass = 0;

        public ResourceUsageInfo(string name = "") : base(name) {}
    }

    public class Globals : PluginGlobals<Globals>
    {
        #region Resources
        [Persistent] public ResourceUsageInfo AssemblyResource = new ResourceUsageInfo("SpecializedParts");
        [Persistent] public ResourceUsageInfo ConstructionResource = new ResourceUsageInfo("MaterialKits");

        const string RESOURCES_NODE = "GC_KIT_RESOURCES";
        HashSet<int> keep_res_ids;
        public HashSet<int> KeepResourcesIDs
        {
            get
            {
                if(keep_res_ids == null)
                {
                    keep_res_ids = new HashSet<int>();
                    var nodes = GameDatabase.Instance.GetConfigNodes(RESOURCES_NODE);
                    foreach(var node in nodes)
                    {
                        foreach(ConfigNode.Value val in node.values)
                        {
                            var res = PartResourceLibrary.Instance.GetDefinition(val.value);
                            if(res != null) keep_res_ids.Add(res.id);
                        }
                    }
                }
                return keep_res_ids;
            }
        }
        #endregion

        [Persistent] public bool  UseStockAppLauncher = true;

        [Persistent] public float WorkshopShutdownThreshold = 0.99f;
        [Persistent] public float MaxDistanceToWorkshop     = 300f;
        [Persistent] public float MinDistanceToWorkshop     = 50f;
        [Persistent] public float MaxDistanceEfficiency     = 0.2f;
//        [Persistent] public float MaxMetalworkPerHour       = 0.01f;


        [Persistent] public float VolumePerKerbal           = 3;
        [Persistent] public float PartVolumeFactor          = 0.3f;
        [Persistent] public float MinGenericEfficiency      = 0.05f;
        [Persistent] public float MaxGenericEfficiency      = 0.5f;

        [Persistent] public float ComplexityFactor = 1e-4f;

        [Persistent] public float ComplexityWeight = 110;
        [Persistent] public float MetalworkWeight  = 100;

        [Persistent] public float VesselKitDensity = 0.5f; //t/m3
        [Persistent] public float VesselKitMinSize = 0.5f; //m

        [Persistent] public float DeployMaxSpeed   = 0.5f;  //m/s
        [Persistent] public float DeployMaxAV      = 5e-6f; //(rad/s)2
        [Persistent] public float DeploymentSpeed  = 1;     //m3/s
        [Persistent] public float MinDeploymentTime = 3;     //s

        [Persistent] public int EasingFrames = 120;
    }
}

