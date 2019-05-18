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
using KSP.Localization;
using AT_Utils;

namespace GroundConstruction
{
    public sealed class VesselKit : CompositeJob<PartKit>, iDIYKit
    {
        public new const string NODE_NAME = "VESSEL_KIT";

        [Persistent] public Guid id = Guid.Empty;
        [Persistent] public ConfigNode Blueprint;
        [Persistent] public Metric ShipMetric;

        [Persistent] public uint DockingPartId;
        [Persistent] public string DockingNodeId;
        [Persistent] public Vector3 DockingOffset;

        public bool DockingPossible => !string.IsNullOrEmpty(DockingNodeId);

        public AttachNode GetDockingNode(Vessel vsl) =>
        DockingPossible 
            ? vsl.Parts.GetPartByCraftID(DockingPartId)?.FindAttachNode(DockingNodeId) 
            : null;

        [Persistent] public float ResourcesMass;
        [Persistent] public float ResourcesCost;
        [Persistent] public bool HasLaunchClamps;
        [Persistent] public ConstructResources AdditionalResources = new ConstructResources();

        public PartModule Host;
        public Vessel CrewSource;
        public List<ProtoCrewMember> KitCrew;
        Dictionary<uint, float> workers = new Dictionary<uint, float>();

        DIYKit.Requirements remainder;

        void strip_resources(IShipconstruct ship, bool assembled)
        {
            AdditionalResources.Clear();
            if(assembled)
                ship.Parts.ForEach(p =>
                                   p.Resources.ForEach(r =>
            {
                if(r.info.isTweakable &&
                   r.info.density > 0 &&
                   r.info.id != Utils.ElectricCharge.id &&
                   !GLB.KeepResourcesIDs.Values.Contains(r.info.id))
                    AdditionalResources.Strip(r);
            }));
            else
                ship.Parts.ForEach(p =>
                                   p.Resources.ForEach(r => AdditionalResources.Strip(r)));
        }

        void update_docking_offset(IShipconstruct ship)
        {
            DockingPartId = 0;
            DockingNodeId = string.Empty;
            DockingOffset = Vector3.zero;
            var bounds = ship.Bounds();
            var bottom_center = bounds.center - new Vector3(0, bounds.extents.y, 0);
            Utils.Log("{}: bounds {}\nbottom center: {}", Name, bounds, bottom_center);//debug
            var offset = Vector3.zero;
            var best_dist = float.MaxValue;
            Part best_part = null;
            AttachNode best_node = null;
            foreach(var p in ship.Parts)
            {
                foreach(var n in p.attachNodes)
                {
                    p.Log("Examining attach node: {}", n.id);//debug
                    var orientation = p.partTransform.TransformDirection(n.orientation).normalized;
                    p.Log("{}.orientation: {} => {}", n.id, n.orientation, orientation);//debug
                    p.Log("{} down cos: {}", n.id, Vector3.Dot(Vector3.down, orientation));//debug
                    if(Vector3.Dot(Vector3.down, orientation) > GLB.MaxDockingCos)
                    {
                        var delta = p.partTransform.TransformPoint(n.position) - bottom_center;
                        var dist = delta.sqrMagnitude;
                        p.Log("{}.dist: {}", n.id, dist);//debug
                        if(best_node == null || dist < best_dist)
                        {
                            best_part = p;
                            best_node = n;
                            best_dist = dist;
                            offset = delta;
                        }
                    }
                }
            }
            Utils.Log("Part: {}, Best dist: {}, AttachNode: {}\nOccupied: {}\noffset: {}",
                      best_part.GetID(), best_dist, best_node?.id, best_node?.attachedPart != null, offset);//debug
            if(best_node != null 
               && best_node.attachedPart == null
               && offset.y < GLB.MaxDockingDist)
            {
                DockingOffset = offset;
                DockingPartId = best_part.craftID;
                DockingNodeId = best_node.id;
                Utils.Log("Docking posible through: {}, bounds: {}", best_node.id, GetBoundsForDocking());//debug
            }
        }

        public Bounds GetBoundsForDocking() =>
        new Bounds(ShipMetric.bounds.center,
                   ShipMetric.bounds.size + DockingOffset.AbsComponents());

        public VesselKit() { id = Guid.NewGuid(); }
        public VesselKit(PartModule host, ShipConstruct ship, bool assembled = true)
            : this()
        {
            Host = host;
            Name = Localizer.Format(ship.shipName);
            strip_resources(ship, assembled);
            Blueprint = ship.SaveShip();
            ShipMetric = new Metric(ship, true, true);
            update_docking_offset(ship);
            Jobs.AddRange(ship.Parts.ConvertAll(p => new PartKit(p, assembled)));
            SetStageComplete(DIYKit.ASSEMBLY, assembled);
            HasLaunchClamps = ship.HasLaunchClamp();
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

        public bool Draw(GUIStyle style = null)
        {
            var rem = RemainingRequirements();
            var stage = CurrentStageIndex;
            var total_work = stage < StagesCount ? Jobs.Sum(j => j.CurrentStage.TotalWork) : 1;
            return DIYKit.Draw(Name, stage, total_work, rem, style, DockingPossible? "(D)" : null);
        }

        public Part CreatePart(string part_name, string flag_url, bool set_host)
        {
            var kit_part = PartMaker.CreatePart(part_name, flag_url);
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
            return kit_part ? PartMaker.CreatePartConstruct(kit_part, "DIY Kit: " + Name, "") : null;
        }

        public void TransferCrewToKit(Vessel vsl)
        {
            if(CrewSource != null && KitCrew != null && KitCrew.Count > 0)
                CrewTransferBatch.moveCrew(CrewSource, vsl, KitCrew);
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

    public class ConstructResourceInfo : ResourceInfo
    {
        [Persistent] public double amount;

        public ConstructResourceInfo() { }
        public ConstructResourceInfo(string name) : base(name) { }

        public void Add(double a)
        {
            amount += a;
            if(amount < 0)
                amount = 0;
        }
    }

    public class ConstructResources : SortedList<string, ConstructResourceInfo>, IConfigNode
    {
        public void Strip(PartResource res)
        {
            if(res.amount > 0)
            {
                ConstructResourceInfo info;
                if(!TryGetValue(res.resourceName, out info))
                {
                    info = new ConstructResourceInfo(res.resourceName);
                    Add(info.name, info);
                }
                info.Add(res.amount);
                res.amount = 0;
            }
        }

        public void Draw()
        {
            if(Count == 0) return;
            GUILayout.BeginVertical(Styles.white);
            foreach(var r in this)
            {
                GUILayout.BeginHorizontal(Styles.white);
                GUILayout.Label(r.Key);
                GUILayout.FlexibleSpace();
                GUILayout.Label(Utils.formatBigValue((float)r.Value.amount, "u"));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        public void Load(ConfigNode node)
        {
            Clear();
            foreach(var n in node.GetNodes())
            {
                var item = ConfigNodeObject.FromConfig<ConstructResourceInfo>(n);
                if(item != null)
                    Add(item.name, item);
            }
        }

        public void Save(ConfigNode node)
        {
            foreach(var r in this)
                r.Value.SaveInto(node);
        }
    }
}

