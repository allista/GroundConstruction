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
    public class Globals : PluginGlobals<Globals>
    {
        #region Resources
        [Persistent] public ResourceUsageInfo AssemblyResource = new ResourceUsageInfo("SpecializedParts");
        [Persistent] public ResourceUsageInfo ConstructionResource = new ResourceUsageInfo("MaterialKits");

        public ResourceIdSet KeepResourcesIDs = new ResourceIdSet("GC_KIT_RESOURCES");
        #endregion

        public ModuleNamesSet IgnoreModules = new ModuleNamesSet("GC_IGNORE_MODULES");

        [Persistent] public bool UseStockAppLauncher = true;

        [Persistent] public float WorkshopShutdownThreshold = 0.99f;
        [Persistent] public float MaxDistanceToWorkshop = 300f;
        [Persistent] public float MinDistanceToWorkshop = 50f;
        [Persistent] public float MaxDistanceEfficiency = 0.2f;

        [Persistent] public float VolumePerKerbal = 3;
        [Persistent] public float PartVolumeFactor = 0.3f;
        [Persistent] public float MinGenericEfficiency = 0.05f;
        [Persistent] public float MaxGenericEfficiency = 0.5f;

        [Persistent] public float ComplexityFactor = 1e-4f;

        [Persistent] public float ComplexityWeight = 110;
        [Persistent] public float MetalworkWeight = 100;

        [Persistent] public float VesselKitDensity = 0.5f; //t/m3
        [Persistent] public float MinKitVolume = 0.02f; //m3

        [Persistent] public float DeployMaxSpeed = 0.5f;  //m/s
        [Persistent] public float DeployMaxAV = 5e-6f; //(rad/s)2
        [Persistent] public float MaxDeploymentMomentum = 1;     //t*m/s
        [Persistent] public float MinDeploymentTime = 3;    //s

        [Persistent] public int EasingFrames = 120;
    }

    public class ResourceUsageInfo : ResourceInfo
    {
        [Persistent] public float ComplexityWork = 0;
        [Persistent] public float EnergyPerMass = 0;
        [Persistent] public float WorkPerMass = 0;

        public ResourceUsageInfo(string name = "") : base(name) { }
    }

    public abstract class ConfigValueSet<T>
    {
        protected string node_name = string.Empty;
        HashSet<T> values;

        protected abstract bool transform_value(ConfigNode.Value val, out T value);

        protected ConfigValueSet(string nodeName)
        {
            node_name = nodeName;
        }

        public HashSet<T> Values
        {
            get
            {
                if(values == null)
                {
                    values = new HashSet<T>();
                    T value;
                    var nodes = GameDatabase.Instance.GetConfigNodes(node_name);
                    foreach(var node in nodes)
                    {
                        foreach(ConfigNode.Value val in node.values)
                        {
                            if(transform_value(val, out value))
                                values.Add(value);
                        }
                    }
                }
                return values;
            }
        }

        public static implicit operator HashSet<T>(ConfigValueSet<T> configValueSet)
        => configValueSet.Values;
    }

    public class ModuleNamesSet : ConfigValueSet<string>
    {
        public ModuleNamesSet(string nodeName) : base(nodeName) { }
        protected override bool transform_value(ConfigNode.Value val, out string value)
        {
            value = val.value;
            return true;
        }

        public int SizeOfDifference(PartModuleList modules)
        {
            int size = 0;
            for(int i = 0, modulesCount = modules.Count; i < modulesCount; i++)
            {
                if(!Values.Contains(modules[i].ClassName))
                    size++;
            }
            return size;
        }
    }

    public class ResourceIdSet : ConfigValueSet<int>
    {
        public ResourceIdSet(string nodeName) : base(nodeName) { }
        protected override bool transform_value(ConfigNode.Value val, out int value)
        {
            value = 0;
            var res = PartResourceLibrary.Instance.GetDefinition(val.value);
            if(res != null)
            {
                value = res.id;
                return true;
            }
            return false;
        }
    }
}
