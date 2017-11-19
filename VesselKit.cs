//   VesselKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Collections.Generic;
using AT_Utils;

namespace GroundConstruction
{
    public class VesselKit : DIYKit
    {
        public new const string NODE_NAME = "VESSEL_KIT";

        [Persistent] public PersistentList<PartKit> BuiltParts = new PersistentList<PartKit>();
        [Persistent] public PersistentList<PartKit> UnbuiltParts = new PersistentList<PartKit>();
        [Persistent] public PersistentList<PartKit> UnassembledParts = new PersistentList<PartKit>();

        [Persistent] public PartKit PartUnderConstruction;

        [Persistent] public string Name;
        [Persistent] public ConfigNode Blueprint;
        [Persistent] public Metric ShipMetric;

        public double AssemblyWork { get { return work_left(Assembly); } }
        public double ConstructionWork { get { return work_left(Construction); } }

        public float AssemblyResource
        {
            get
            {
                var part_mass = PartUnderConstruction?
                    PartUnderConstruction.AssemblyMassLeft : 0;
                var required_mass = 0f;
                UnassembledParts.ForEach(p => required_mass += p.AssemblyMassLeft);
                return (required_mass+part_mass)/PartUnderConstruction.Assembly.Resource.def.density;
            }
        }

        public float ConstructionResource
        {
            get
            {
                var part_mass = PartUnderConstruction?
                    PartUnderConstruction.ConstructionMassLeft : 0;
                var required_mass = 0f;
                UnbuiltParts.ForEach(p => required_mass += p.ConstructionMassLeft);
                return (required_mass+part_mass)/PartUnderConstruction.Construction.Resource.def.density;
            }
        }

        double work_left(Job job)
        {
            var work = job.WorkLeft;
            if(PartUnderConstruction != null)

                work -= PartUnderConstruction.SameJob(job).WorkDone;
            return work < 0? 0 : work;
        }

        static void strip_resources(ShipConstruct ship)
        {
            ship.Parts.ForEach(p =>
                               p.Resources.ForEach(r =>
            {
                if(r.info.isTweakable &&
                   r.info.density > 0 &&
                   r.info.id != Utils.ElectricCharge.id &&
                   !GLB.KeepResourcesIDs.Contains(r.info.id))
                    r.amount = 0;
            }));
        }

        public VesselKit() {}

        public VesselKit(ShipConstruct ship, bool assembled = true)
        {
            Mass = Cost = 0;
            Name = ship.shipName;
            strip_resources(ship);
            Blueprint = ship.SaveShip();
            ShipMetric = new Metric(ship, true);
            if(assembled)
            {
                UnbuiltParts.Capacity = ship.Parts.Count;
                UnbuiltParts.AddRange(ship.Parts.ConvertAll(p => new PartKit(p, assembled)));
                UnbuiltParts.ForEach(p =>
                {
                    Mass += p.Mass;
                    Cost += p.Cost;
                    Construction.AddSubtask(p.Construction);
                });
                Assembly.Completeness = 1;
            }
            else
            {
                UnassembledParts.Capacity = ship.Parts.Count;
                UnassembledParts.AddRange(ship.Parts.ConvertAll(p => new PartKit(p, assembled)));
                UnbuiltParts.ForEach(p => Assembly.AddSubtask(p.Assembly));
                Assembly.Completeness = 0;
            }
        }

        public ShipConstruct LoadConstruct()
        {
            var ship = new ShipConstruct();
            if(!ship.LoadShip(Blueprint))
            {
                ship.Unload();
                return null;
            }
            return ship;
        }

        public int CrewCapacity()
        {
            if(!Valid || Construction.Completeness < 1) return 0;
            var capacity = 0;
            foreach(ConfigNode p in Blueprint.nodes)
            {
                var name_id = p.GetValue("part");
                if(string.IsNullOrEmpty(name_id)) continue;
                string name = KSPUtil.GetPartName(name_id);
                var kit_part = PartLoader.getPartInfoByName(name);
                if(kit_part == null || kit_part.partPrefab == null) continue;
                capacity += kit_part.partPrefab.CrewCapacity;
            }
            return capacity;
        }

        public bool BlueprintComplete()
        {
            var db = new HashSet<uint>();
            BuiltParts.ForEach(p => db.Add(p.craftID));
            foreach(ConfigNode p in Blueprint.nodes)
            {
                var name_id = p.GetValue("part");
                if(string.IsNullOrEmpty(name_id)) continue;
                string name = "", cid = "0";
                KSPUtil.GetPartInfo(name_id, ref name, ref cid);
                if(!db.Contains(uint.Parse(cid))) return false;
            }
            return true;
        }

        void update_mass_cost()
        {
            Mass = Cost = 0;
            UnbuiltParts.ForEach(p =>
            {
                Mass += p.Mass;
                Cost += p.Cost;
            });
            BuiltParts.ForEach(p =>
            {
                Mass += p.PartMass;
                Cost += p.PartCost;
            });
            if(PartUnderConstruction)
            {
                Mass += PartUnderConstruction.Mass;
                Cost += PartUnderConstruction.Cost;
            }
        }

        bool get_next_part(IList<PartKit> parts)
        {
            if(PartUnderConstruction) 
                return true;
            if(parts.Count > 0)
            {
                PartUnderConstruction = parts[0];
                parts.RemoveAt(0);
                return true;
            }
            return false;
        }

        public override double AssembleyRequirement(double work, out double energy, out int resource_id, out double resource_mass)
        {
            if(get_next_part(UnassembledParts))
                return PartUnderConstruction.AssembleyRequirement(work, out energy, out resource_id, out resource_mass);
            resource_id = Construction.Resource.id;
            energy = resource_mass = 0;
            return 0;
        }

        public override double ConstructionRequerement(double work, out double energy, out int resource_id, out double resource_mass)
        {
            if(get_next_part(UnbuiltParts))
                return PartUnderConstruction.ConstructionRequerement(work, out energy, out resource_id, out resource_mass);
            resource_id = Construction.Resource.id;
            energy = resource_mass = 0;
            return 0;
        }

        public override bool Assemble(double work)
        {
            if(!PartUnderConstruction) return true;
            if(PartUnderConstruction.Assemble(work))
            {
                Assembly.DoSomeWork(PartUnderConstruction.Assembly);
                UnassembledParts.Add(PartUnderConstruction);
                PartUnderConstruction = null;
            }
            update_mass_cost();
        }

        public override bool Construct(double work)
        {
            if(!PartUnderConstruction) return true;
            if(PartUnderConstruction.Construct(work))
            {
                Construction.DoSomeWork(PartUnderConstruction.Construction);
                BuiltParts.Add(PartUnderConstruction);
                PartUnderConstruction = null;
            }
            update_mass_cost();
        }
    }
}

