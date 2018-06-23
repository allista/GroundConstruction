//   VesselKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
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

        public VesselKit() { id = Guid.NewGuid(); }
        public VesselKit(PartModule host, ShipConstruct ship, bool assembled = true)
            : this()
        {
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

        public float MassAtStage(int stage)
        {
            var parts = 0f;
            Jobs.ForEach(p => parts += p.MassAtStage(stage));
            return parts;
        }

        public float CostAtStage(int stage)
        {
            var parts = 0f;
            Jobs.ForEach(p => parts += p.CostAtStage(stage));
            return parts;
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
            workers.Clear();
        }

        public void Draw()
        {
            var rem = RemainingRequirements();
            var stage = CurrentStageIndex;
            var total_work = stage < StagesCount ? Jobs.Sum(j => j.CurrentStage.TotalWork) : 1;
            DIYKit.Draw(Name, stage, total_work, rem);
        }

        public Part CreatePart(string part_name, string flag_url, bool set_host)
        {
            var part_info = PartLoader.getPartInfoByName(part_name);
            if(part_info == null)
            {
                Utils.Message("No such part: {0}", part_name);
                return null;
            }
            var kit_part = UnityEngine.Object.Instantiate(part_info.partPrefab);
            kit_part.gameObject.SetActive(true);
            kit_part.partInfo = part_info;
            kit_part.name = part_info.name;
            kit_part.flagURL = flag_url;
            kit_part.persistentId = FlightGlobals.GetUniquepersistentId();
            FlightGlobals.PersistentLoadedPartIds.Remove(kit_part.persistentId);
            kit_part.transform.position = Vector3.zero;
            kit_part.attPos0 = Vector3.zero;
            kit_part.transform.rotation = Quaternion.identity;
            kit_part.attRotation = Quaternion.identity;
            kit_part.attRotation0 = Quaternion.identity;
            kit_part.partTransform = kit_part.transform;
            kit_part.orgPos = kit_part.transform.root.InverseTransformPoint(kit_part.transform.position);
            kit_part.orgRot = Quaternion.Inverse(kit_part.transform.root.rotation) * kit_part.transform.rotation;
            kit_part.packed = true;
            //initialize modules
            kit_part.InitializeModules();
            var module_nodes = part_info.partConfig.GetNodes("MODULE");
            for(int i = 0, maxLength = module_nodes.Length; i < maxLength; i++)
            {
                var node = module_nodes[i];
                var module_name = node.GetValue("name");
                if(!string.IsNullOrEmpty(module_name))
                {
                    var module = kit_part.Modules[i];
                    if(module != null && module.ClassName == module_name)
                        module.Load(node);
                }
            }

            foreach(var module in kit_part.Modules)
                module.OnStart(PartModule.StartState.PreLaunch);
            //add the kit to construction kit module
            var kit_module = kit_part.FindModuleImplementing<DeployableKitContainer>();
            if(kit_module == null)
            {
                Utils.Message("{0} has no DeployableKitContainer-dervied MODULE", part_name);
                UnityEngine.Object.Destroy(kit_part);
                return null;
            }
            kit_module.StoreKit(this);
            if(set_host)
                Host = kit_module;
            return kit_part;
        }

        public ShipConstruct CreateShipConstruct(string part_name, string flag_url)
        {
            var kit_part = CreatePart(part_name, flag_url, true);
            if(kit_part)
            {
                var ship = new ShipConstruct("DIY Kit: "+Name, "", kit_part);
                ship.rotation = Quaternion.identity;
                ship.missionFlag = kit_part.flagURL;
                return ship;
            }
            return null;
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            if(node.HasValue("Completeness"))
            {
                //Utils.Log("VesselKit.Load: {}\n{}", this, node);//debug
                //deprecated config conversion
                CurrentIndex = 0;
                var list = new PersistentList<PartKit>();
                var n = node.GetNode("BuiltParts");
                if(n != null)
                {
                    list.Load(n);
                    Jobs.AddRange(list.Where(j => j.Valid));
                    list.Clear();
                    CurrentIndex = Jobs.Count;
                }
                n = node.GetNode("PartUnderConstruction");
                if(n != null)
                {
                    var p = new PartKit();
                    p.Load(n);
                    if(p.Valid)
                        Jobs.Add(p);
                }
                n = node.GetNode("UnbuiltParts");
                if(n != null)
                {
                    list.Load(n);
                    Jobs.AddRange(list.Where(j => j.Valid));
                    list.Clear();
                }
                //Utils.Log("VesselKit.Loaded: {}", this);//debug
            }
        }

        public bool Equals(VesselKit other) => id != Guid.Empty && id == other.id;
    }
}

