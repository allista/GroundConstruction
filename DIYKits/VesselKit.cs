//   VesselKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;
using AT_Utils;

namespace GroundConstruction
{
    public sealed class VesselKit : CompositeJob<PartKit>
    {
        public new const string NODE_NAME = "VESSEL_KIT";

        [Persistent] public Guid id;
        [Persistent] public ConfigNode Blueprint;
        [Persistent] public Metric ShipMetric;

        [Persistent] public float ResourcesMass;
        [Persistent] public float ResourcesCost;

        public PartModule Host;
        public Vessel CrewSource;
        public List<ProtoCrewMember> KitCrew;
        Dictionary<uint,float> workers = new Dictionary<uint, float>();

        static void strip_resources(IShipconstruct ship, bool assembled)
        {
            if(assembled)
                ship.Parts.ForEach(p =>
                                   p.Resources.ForEach(r =>
                {
                    if(r.info.isTweakable &&
                       r.info.density > 0 &&
                       r.info.id != Utils.ElectricCharge.id &&
                       !GLB.KeepResourcesIDs.Contains(r.info.id))
                        r.amount = 0;
                }));
            else
                ship.Parts.ForEach(p =>
                                   p.Resources.ForEach(r => r.amount = 0));
        }

        public VesselKit()
        {
            id = Guid.NewGuid();
        }

        public VesselKit(PartModule host, ShipConstruct ship, bool assembled = true)
            : this()
        {
            Host = host;
            Name = ship.shipName;
            strip_resources(ship, assembled);
            Blueprint = ship.SaveShip();
            ShipMetric = new Metric(ship, true);
            Jobs.AddRange(ship.Parts.ConvertAll(p => new PartKit(p, assembled)));
            SetStageComplete(DIYKit.ASSEMBLY, assembled);
            CurrentIndex = 0;
        }

        public override bool Valid
        { get { return base.Valid && Host != null && Host.part != null; } }

        public float Mass
        {
            get
            {
                var parts = 0f;
                Jobs.ForEach(p => parts += p.Mass);
                return ResourcesMass + parts;
            }
        }

        public float Cost
        {
            get
            {
                var parts = 0f;
                Jobs.ForEach(p => parts += p.Cost);
                return ResourcesCost + parts;
            }
        }

        public double CurrentTaskETA
        { 
            get
            {
                if(!Valid)
                    return -1;
                var workforce = workers.Values.Sum();
                return workforce > 0 ? WorkLeftInStage(CurrentStageIndex) / workforce : -1;
            }
        }

        public VesselResources ConstructResources
        { get { return Complete ? new VesselResources(Blueprint) : null; } }

        public void CheckinWorker(WorkshopBase module)
        {
            workers[module.part.flightID] = module.Workforce;
        }

        public void CheckoutWorker(WorkshopBase module)
        {
            workers.Remove(module.part.flightID);
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
            if(!Valid || !Complete)
                return 0;
            var capacity = 0;
            foreach(ConfigNode p in Blueprint.nodes)
            {
                var name_id = p.GetValue("part");
                if(string.IsNullOrEmpty(name_id))
                    continue;
                string name = KSPUtil.GetPartName(name_id);
                var kit_part = PartLoader.getPartInfoByName(name);
                if(kit_part == null || kit_part.partPrefab == null)
                    continue;
                capacity += kit_part.partPrefab.CrewCapacity;
            }
            return capacity;
        }

        public bool BlueprintComplete()
        {
            if(!Complete)
                return false;
            var db = new HashSet<uint>();
            Jobs.ForEach(p => db.Add(p.craftID));
            foreach(ConfigNode p in Blueprint.nodes)
            {
                var name_id = p.GetValue("part");
                if(string.IsNullOrEmpty(name_id))
                    continue;
                string name = "", cid = "0";
                KSPUtil.GetPartInfo(name_id, ref name, ref cid);
                if(!db.Contains(uint.Parse(cid)))
                    return false;
            }
            return true;
        }

        public double RequirementsForWork(double work, out double energy, out ResourceUsageInfo resource, out double resource_amount)
        {
            energy = 0;
            resource = null;
            resource_amount = 0;
            var job = CurrentJob;
            if(work <= 0 || job == null)
                return 0;
            return job.RequirementsForWork(work, out energy, out resource, out resource_amount);
        }

        public double RemainingRequirements(out double energy, out ResourceUsageInfo resource, out double resource_amount)
        {
            energy = 0;
            resource = null;
            resource_amount = 0;
            var work = 0.0;
            var njobs = Jobs.Count;
            if(CurrentIndex < njobs)
            {
                for(int i = CurrentIndex; i < njobs; i++)
                {
                    double e, ra;
                    work += Jobs[i].RemainingRequirements(out e, out resource, out ra);
                    energy += e;
                    resource_amount += ra;
                }
            }
            return work;
        }

        public string Status()
        {
            ResourceUsageInfo resource;
            double energy, resource_amount;
            var work_left = RemainingRequirements(out energy, out resource, out resource_amount);
            var total_work = work_left > 0 ? Jobs.Sum(j => j.CurrentStage.TotalWork) : 1;
            return DIYKit.Status(Name, CurrentStageIndex, work_left, total_work, energy, resource, resource_amount);
        }

        public void Draw()
        {
            ResourceUsageInfo resource;
            double energy, resource_amount;
            var work_left = RemainingRequirements(out energy, out resource, out resource_amount);
            var total_work = work_left > 0 ? Jobs.Sum(j => j.CurrentStage.TotalWork) : 1;
            DIYKit.Draw(Name, CurrentStageIndex, work_left, total_work, energy, resource, resource_amount);
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            if(node.HasValue("Completeness"))
            {   
                //deprecated config conversion
                var list = new PersistentList<PartKit>();
                var n = node.GetNode("BuiltParts");
                if(n != null)
                {
                    list.Load(n);
                    Jobs.AddRange(list);
                    list.Clear();
                }
                n = node.GetNode("PartUnderConstruction");
                if(n != null)
                {
                    var p = new PartKit();
                    p.Load(n);
                    Jobs.Add(p);
                }
                n = node.GetNode("UnbuiltParts");
                if(n != null)
                {
                    list.Load(n);
                    Jobs.AddRange(list);
                    list.Clear();
                }
            }
        }
    }
}

