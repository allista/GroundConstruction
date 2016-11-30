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

		[Persistent] public PartKit PartUnderConstruction;

		[Persistent] public string Name;
		[Persistent] public ConfigNode Blueprint;
		[Persistent] public Metric ShipMetric;

		float unbuilt_mass, built_mass;
		float unbuilt_cost, built_cost;

		static void strip_resources(ShipConstruct ship)
		{
			ship.Parts.ForEach(p => 
			                   p.Resources.ForEach(r => 
			{ 
				if(r.info.isTweakable && 
				   r.info.density > 0 &&
				   r.info.id != Utils.ElectricChargeID &&
				   !GLB.KeepResourcesIDs.Contains(r.info.id)) 
					r.amount = 0; 
			}));
		}

		public VesselKit() {}

		public VesselKit(ShipConstruct ship)
		{
			TotalWork = 0;
			Mass = Cost = 0;
			Name = ship.shipName;
			strip_resources(ship);
			Blueprint = ship.SaveShip();
			ShipMetric = new Metric(ship);
			UnbuiltParts.Capacity = ship.Parts.Count;
			UnbuiltParts.AddRange(ship.Parts.ConvertAll(p => new PartKit(p)));
			UnbuiltParts.ForEach(p => 
			{ 
				Mass += p.Mass;
				Cost += p.Cost;
				TotalWork += p.TotalWork; 
			});
			unbuilt_mass = Mass;
			unbuilt_cost = Cost;
//			Utils.Log("VesselKit.Constructor: {}", this);//debug
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

		public Dictionary<uint,float> BuiltPartsState()
		{
			var db = new Dictionary<uint,float>(BuiltParts.Count);
			BuiltParts.ForEach(p => db.Add(p.craftID, p.Completeness));
			return db;
		}

		public override void Load(ConfigNode node)
		{
			base.Load(node);
			unbuilt_mass = unbuilt_cost = 0;
			UnbuiltParts.ForEach(p => 
			{ 
				unbuilt_mass += p.Mass;
				unbuilt_cost += p.Cost;
			});
			built_mass = built_cost = 0;
			BuiltParts.ForEach(p => 
			{ 
				built_mass += p.PartMass;
				built_cost += p.PartCost;
			});
		}

		bool get_next_if_needed()
		{
			if(PartUnderConstruction == null || !PartUnderConstruction.Valid)
			{
				if(UnbuiltParts.Count == 0) return false;
				PartUnderConstruction = UnbuiltParts[0];
				UnbuiltParts.RemoveAt(0);
				unbuilt_mass -= PartUnderConstruction.Mass;
				unbuilt_cost -= PartUnderConstruction.Cost;
			}
			return true;
		}

		public override double RequiredMass(ref double skilled_kerbal_seconds, out double required_energy)
		{
			required_energy = 0;
			if(get_next_if_needed())
				return PartUnderConstruction.RequiredMass(ref skilled_kerbal_seconds, out required_energy);
			skilled_kerbal_seconds = 0;
			return 0;
		}

		public override void DoSomeWork(double skilled_kerbal_seconds)
		{
			if(!get_next_if_needed()) return;
			PartUnderConstruction.DoSomeWork(skilled_kerbal_seconds);
			if(PartUnderConstruction.Completeness >= 1)
			{
				WorkDone = Math.Min(TotalWork, WorkDone+PartUnderConstruction.TotalWork);
				Completeness = (float)(WorkDone/TotalWork);
				BuiltParts.Add(PartUnderConstruction);
				built_mass += PartUnderConstruction.PartMass;
				built_cost += PartUnderConstruction.PartCost;
				PartUnderConstruction = null;
			}
			Mass = built_mass+unbuilt_mass;
			Cost = built_cost+unbuilt_cost;
			if(PartUnderConstruction != null)
			{
				Mass += PartUnderConstruction.Mass;
				Cost += PartUnderConstruction.Cost;
			}
		}
	}
}

