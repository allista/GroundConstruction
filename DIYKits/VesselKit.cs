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

        [Persistent] public DockingNodeList DockingNodes = new DockingNodeList();

        public bool DockingPossible => DockingNodes.Count > 0;

        public AttachNode GetDockingNode(Vessel vsl, int node_idx) =>
            node_idx >= 0 && node_idx < DockingNodes.Count
                ? DockingNodes[node_idx].GetDockingNode(vsl)
                : null;

        [Persistent] public float KitResourcesMass;
        [Persistent] public float KitResourcesCost;
        [Persistent] public float ResourcesMass;
        [Persistent] public float ResourcesCost;
        [Persistent] public bool HasLaunchClamps;
        [Persistent] public KitResourcesList AdditionalResources = new KitResourcesList();

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
                        if(r.info.isTweakable
                           && r.info.density > 0
                           && r.info.id != Utils.ElectricCharge.id
                           && !GLB.KeepResources.Values.Contains(r.info.id))
                            AdditionalResources.Strip(r);
                    }));
            else
                ship.Parts.ForEach(p =>
                    p.Resources.ForEach(r =>
                    {
                        if(!GLB.AssembleResources.Values.Contains(r.info.id)
                           && !GLB.ConstructResources.Values.Contains(r.info.id))
                            AdditionalResources.Strip(r);
                    }));
        }

        KitResourcesList count_kit_resources(IShipconstruct ship, bool assembled)
        {
            var resouces_to_assemble = new KitResourcesList();
            KitResourcesCost = KitResourcesMass = 0f;
            ship.Parts.ForEach(p =>
                p.Resources.ForEach(res =>
                {
                    if(!assembled && GLB.AssembleResources.Values.Contains(res.info.id))
                        resouces_to_assemble.Keep(res, KitResourceInfo.ResourceType.ASSEMBLED);
                    else if(!assembled && GLB.ConstructResources.Values.Contains(res.info.id))
                        resouces_to_assemble.Keep(res, KitResourceInfo.ResourceType.CONSTRUCTED);
                    else
                    {
                        var amount = (float)res.amount;
                        KitResourcesMass += amount * res.info.density;
                        KitResourcesCost += amount * res.info.unitCost;
                    }
                }));
            return resouces_to_assemble;
        }

        public static DockingNodeList FindDockingNodes(IShipconstruct ship, Metric ship_metric)
        {
            var nodes = new DockingNodeList();
            var bounds = ship_metric.bounds;
            var bottom_center = bounds.center - new Vector3(0, bounds.extents.y, 0);
            foreach(var p in ship.Parts)
            {
                var variants = p.Modules.GetModule<ModulePartVariants>();
                var attachNodes = p.attachNodes;
                if(variants != null)
                {
                    attachNodes = variants.SelectedVariant.AttachNodes;
                }
                foreach(var n in attachNodes)
                {
                    if(n.attachedPart != null)
                        continue;
                    var orientation = p.partTransform.TransformDirection(n.orientation).normalized;
                    if(Vector3.Dot(Vector3.down, orientation) > GLB.MaxDockingCos)
                    {
                        var delta = p.partTransform.TransformPoint(n.position) - bottom_center;
                        if(delta.y < GLB.MaxDockingDist)
                        {
                            nodes.Add(new ConstructDockingNode
                            {
                                Name = string.Format("{0} {1:X} ({2})",
                                    p.Title(),
                                    p.craftID,
                                    n.id),
                                PartId = p.craftID,
                                NodeId = n.id,
                                DockingOffset = delta
                            });
                        }
                    }
                }
            }
            return nodes;
        }

        public Bounds GetBoundsForDocking(int node_idx) =>
            new Bounds(ShipMetric.bounds.center,
                ShipMetric.bounds.size + DockingNodes[node_idx].DockingOffset.AbsComponents());

        public VesselKit()
        {
            id = Guid.NewGuid();
        }

        public VesselKit(
            PartModule host,
            ShipConstruct ship,
            bool assembled = true,
            bool simulate = false
        )
            : this()
        {
            Host = host;
            Name = Localizer.Format(ship.shipName);
            ship.Parts.ForEach(p => p.UpdateMass());
            if(!simulate)
            {
                strip_resources(ship, assembled);
                Blueprint = ship.SaveShip();
            }
            var create_resources = count_kit_resources(ship, assembled);
            ShipMetric = new Metric((IShipconstruct)ship, true, true);
            DockingNodes = FindDockingNodes(ship, ShipMetric);
            var final_assembly_work = 0f;
            ship.Parts.ForEach(p =>
            {
                var kit = new PartKit(p, assembled);
                final_assembly_work += p.mass * GLB.FinalizationWorkPerMass * 3600;
                Jobs.Add(kit);
            });
            create_resources.ForEach(r =>
            {
                var assembled_resource = r.Value.type == KitResourceInfo.ResourceType.CONSTRUCTED;
                Jobs.Add(new PartKit(r.Value.name,
                    r.Value.mass,
                    r.Value.cost,
                    assembled_resource ? 0 : 1,
                    0,
                    assembled_resource));
            });
            Jobs.Add(new PartKit("Final Assembly", 0, 0, 0, final_assembly_work, true));
            if(assembled)
                SetStageComplete(DIYKit.ASSEMBLY, true);
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
                return KitResourcesMass + ResourcesMass + parts;
            }
        }

        public float Cost
        {
            get
            {
                var parts = 0f;
                Jobs.ForEach(p => parts += p.Cost);
                return KitResourcesCost + ResourcesCost + parts;
            }
        }

        public float MassAtStage(int stage)
        {
            var parts = 0f;
            Jobs.ForEach(p => parts += p.MassAtStage(stage));
            return KitResourcesMass + parts;
        }

        public float CostAtStage(int stage)
        {
            var parts = 0f;
            Jobs.ForEach(p => parts += p.CostAtStage(stage));
            return KitResourcesCost + parts;
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

        public VesselResources ConstructResources => Complete ? new VesselResources(Blueprint) : null;

        public void CheckinWorker(WorkshopBase module) => workers[module.part.flightID] = module.Workforce;

        public void CheckoutWorker(WorkshopBase module) => workers.Remove(module.part.flightID);

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
                        if(work <= req.work)
                            break;
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
            if(work > 0)
                remainder?.Clear();
            return base.DoSomeWork(work);
        }

        public override void NextStage()
        {
            base.NextStage();
            remainder?.Clear();
            workers.Clear();
        }

        public override void SetStageComplete(int stage, bool complete)
        {
            base.SetStageComplete(stage, complete);
            remainder?.Clear();
            workers.Clear();
        }

        public bool Draw(GUIStyle style = null)
        {
            var rem = RemainingRequirements();
            var stage = CurrentStageIndex;
            var total_work = stage < StagesCount
                ? Jobs.Sum(j =>
                {
                    var cur_stage = j.CurrentStage;
                    return cur_stage != null ? cur_stage.TotalWork : j.TotalWork;
                })
                : 1;
            return DIYKit.Draw(Name, stage, total_work, rem, style, DockingPossible ? "(D)" : null);
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
            return kit_part
                ? PartMaker.CreatePartConstruct(kit_part, "DIY Kit: " + Name, "")
                : null;
        }

        public void TransferCrewToKit(Vessel vsl)
        {
            if(CrewSource != null && KitCrew != null && KitCrew.Count > 0)
                CrewTransferBatch.moveCrew(CrewSource, vsl, KitCrew);
        }

        public bool Equals(VesselKit other) => id != Guid.Empty && id == other.id;
    }

    public class ConstructDockingNode : ConfigNodeObject
    {
        [Persistent] public string Name;
        [Persistent] public uint PartId;
        [Persistent] public string NodeId;
        [Persistent] public Vector3 DockingOffset;

        public Part GetDockingPart(Vessel vsl) => vsl.Parts.GetPartByCraftID(PartId);

        public AttachNode GetDockingNode(Vessel vsl) => vsl.Parts.GetPartByCraftID(PartId)?.FindAttachNode(NodeId);

        public override string ToString() => Name;
    }

    public class DockingNodeList : PersistentList<ConstructDockingNode> { }

    public class KitResourceInfo : ResourceInfo
    {
        public enum ResourceType { STRIPPED, ASSEMBLED, CONSTRUCTED };

        public ResourceType type;

        [Persistent] public double amount;

        public KitResourceInfo() { }

        public KitResourceInfo(string name, ResourceType type)
            : base(name)
        {
            this.type = type;
        }

        public float mass => (float)amount * def.density;
        public float cost => (float)amount * def.unitCost;

        public void Add(double a)
        {
            amount += a;
            if(amount < 0)
                amount = 0;
        }
    }

    public class KitResourcesList : SortedList<string, KitResourceInfo>, IConfigNode
    {
        public void Strip(PartResource res)
        {
            Keep(res, KitResourceInfo.ResourceType.STRIPPED);
            res.amount = 0;
        }

        public void Keep(PartResource res, KitResourceInfo.ResourceType type)
        {
            if(res.amount > 0)
            {
                KitResourceInfo info;
                if(!TryGetValue(res.resourceName, out info))
                {
                    info = new KitResourceInfo(res.resourceName, type);
                    Add(info.name, info);
                }
                info.Add(res.amount);
            }
        }

        public void Draw()
        {
            if(Count == 0)
                return;
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
                var item = ConfigNodeObject.FromConfig<KitResourceInfo>(n);
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
