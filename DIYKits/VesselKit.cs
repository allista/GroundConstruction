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

        [Persistent] public ConfigNode Blueprint;
        [Persistent] public Metric ShipMetric;
        [Persistent] public PersistentList<PartKit> Parts = new PersistentList<PartKit>();

        static void strip_resources(IShipconstruct ship)
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
            Name = ship.shipName;
            strip_resources(ship);
            Blueprint = ship.SaveShip();
            ShipMetric = new Metric(ship, true);
            Parts.AddRange(ship.Parts.ConvertAll(p => new PartKit(p, assembled)));
            Parts.ForEach(AddSubJob);
            Assembly.TotalWork = Parts.Count/10;
            Construction.TotalWork = Parts.Count;
            UpdateTotalWork();
            Assembly.SetComplete(assembled);
        }

        public override bool Valid
        { get { return base.Valid && Parts.Count > 0; } }

        public override float Mass
        {
            get
            {
                var parts = 0f;
                Parts.ForEach(p => parts += p.Mass);
                return base.Mass + parts;
            }
        }

        public override float Cost
        {
            get
            {
                var parts = 0f;
                Parts.ForEach(p => parts += p.Cost);
                return base.Cost + parts;
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
            if(!Complete) return false;
            var db = new HashSet<uint>();
            Parts.ForEach(p => db.Add(p.craftID));
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
    }
}

