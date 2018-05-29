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
    public sealed class VesselKit : CompositeJob<PartKit>, iDIYKit
    {
        public new const string NODE_NAME = "VESSEL_KIT";

        [Persistent] public Guid id = Guid.Empty;
        [Persistent] public ConfigNode Blueprint;
        [Persistent] public Metric ShipMetric;

        [Persistent] public float ResourcesMass;
        [Persistent] public float ResourcesCost;

        public PartModule Host;
        public Vessel CrewSource;
        public List<ProtoCrewMember> KitCrew;
        Dictionary<uint, float> workers = new Dictionary<uint, float>();

        DIYKit.Requirements remainder;

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

        public VesselKit() { }
        public VesselKit(PartModule host, ShipConstruct ship, bool assembled = true)
        {
            id = Guid.NewGuid();
            Host = host;
            Name = ship.shipName;
            strip_resources(ship, assembled);
            Blueprint = ship.SaveShip();
            ShipMetric = new Metric(ship, true, true);
            Jobs.AddRange(ship.Parts.ConvertAll(p => new PartKit(p, assembled)));
            SetStageComplete(DIYKit.ASSEMBLY, assembled);
            CurrentIndex = 0;
        }

        public override bool Valid => base.Valid && Host != null && Host.part != null;

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

        public VesselResources ConstructResources =>
        Complete ? new VesselResources(Blueprint) : null;

        public void CheckinWorker(WorkshopBase module) =>
        workers[module.part.flightID] = module.Workforce;

        public void CheckoutWorker(WorkshopBase module) =>
        workers.Remove(module.part.flightID);

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

        public DIYKit.Requirements RequirementsForWork(double work)
        {
            var req = new DIYKit.Requirements();
            if(work > 0 && Jobs.Count > 0)
            {
                var njobs = Jobs.Count;
                if(CurrentIndex < njobs)
                {
                    for(int i = CurrentIndex; i < njobs; i++)
                    {
                        req.Update(Jobs[i].RequirementsForWork(work - req.work));
                        if(work <= req.work) break;
                    }
                }
            }
            return req;
        }

        public DIYKit.Requirements RemainingRequirements()
        {
            if(!remainder)
            {
                if(remainder == null)
                    remainder = new DIYKit.Requirements();
                var njobs = Jobs.Count;
                if(njobs > 0 && CurrentIndex >= 0 && CurrentIndex < njobs)
                {
                    for(int i = CurrentIndex; i < njobs; i++)
                        remainder.Update(Jobs[i].RemainingRequirements());
                }
            }
            return remainder;
        }

        public override double DoSomeWork(double work)
        {
            if(work > 0 && remainder != null)
                remainder.Clear();
            return base.DoSomeWork(work);
        }

        public override void NextStage()
        {
            base.NextStage();
            if(remainder != null)
                remainder.Clear();
        }

        public void Draw()
        {
            var rem = RemainingRequirements();
            var stage = CurrentStageIndex;
            var total_work = stage < StagesCount ? Jobs.Sum(j => j.CurrentStage.TotalWork) : 1;
            DIYKit.Draw(Name, stage, total_work, rem);
        }

        public override void Load(ConfigNode node)
        {
            Utils.Log("VesselKit.Load: {}\n{}", this, node);//debug
            base.Load(node);
            if(node.HasValue("Completeness"))
            {
                //deprecated config conversion
                CurrentIndex = 0;
                var list = new PersistentList<PartKit>();
                var n = node.GetNode("BuiltParts");
                if(n != null)
                {
                    list.Load(n);
                    Jobs.AddRange(list);
                    list.Clear();
                    CurrentIndex = Jobs.Count;
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
            Utils.Log("VesselKit.Loaded: {}", this);//debug
        }

        public bool Equals(VesselKit other) => id != Guid.Empty && id == other.id;
    }
}

