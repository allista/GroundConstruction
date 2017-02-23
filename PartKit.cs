//   PartKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using UnityEngine;
using AT_Utils;

namespace GroundConstruction
{
	public interface iDIYKit
	{
		double RequiredMass(ref double skilled_kerbal_seconds, out double required_energy);
		void DoSomeWork(double skilled_kerbal_seconds);
		bool Valid { get; }
	}

	public abstract class DIYKit : ConfigNodeObject, iDIYKit
	{
		public new const string NODE_NAME = "DIY_KIT";

		protected static Globals GLB { get { return Globals.Instance; } }

		[Persistent] public double TotalWork = -1; //seconds
		[Persistent] public double WorkDone;       //seconds
		[Persistent] public float  Completeness;    //fraction

		[Persistent] public float Mass;
		[Persistent] public float Cost;

		public bool Valid { get { return TotalWork > 0; } }

		public double WorkLeft { get { return TotalWork-WorkDone; } }

		public abstract double RequiredMass(ref double skilled_kerbal_seconds, out double required_energy);
		public abstract void DoSomeWork(double skilled_kerbal_seconds);
	}

	public class PartKit : DIYKit
	{
		public new const string NODE_NAME = "PART_KIT";

		[Persistent] public string Name;
		[Persistent] public string Title;
		[Persistent] public uint   craftID;

		[Persistent] public float PartMass;
		[Persistent] public float KitMass;

		[Persistent] public float PartCost;
		[Persistent] public float KitCost;

		[Persistent] public float Complexity; //fraction

		public float MassLeft { get { return PartMass-Mass; } }

		public PartKit() {}

		public PartKit(Part part)
		{
			Name = part.partInfo.name;
			Title = part.partInfo.title;
			craftID  = part.craftID;
			var is_DIY_Kit = part.Modules.Contains<ModuleConstructionKit>();
			var res_mass = part.GetResourceMass();
            var dry_cost = part.DryCost();
			PartMass = part.mass+res_mass;
            PartCost = dry_cost+part.ResourcesCost();
			if(is_DIY_Kit)
			{
                Mass = KitMass = PartMass;
				Cost = KitCost = PartCost;
				Complexity = 1;
				TotalWork = 0;
				Completeness = 1;
			}
			else 
			{
                Complexity = 1-1/((dry_cost/part.mass+part.Modules.Count*1000)*GLB.ComplexityFactor+1);
				Mass = KitMass = part.mass*Complexity+res_mass;
				var add_mass = PartMass - Mass;
                Cost = KitCost = Mathf.Max(0, PartCost - add_mass/GLB.StructureResource.density*GLB.StructureResource.unitCost);
				TotalWork = (Complexity*GLB.ComplexityWeight + add_mass*GLB.MetalworkWeight)*3600;
			}
//            Utils.Log("{}: complexity {}, KitMass {}/{} = {}, KitCost {}/{} = {}", 
//                      part, Complexity, 
//                      KitMass, PartMass, KitMass/PartMass,
//                      KitCost, PartCost, KitCost/PartCost);//debug
		}

		public override double RequiredMass(ref double skilled_kerbal_seconds, out double required_energy)
		{
			if(WorkDone+skilled_kerbal_seconds > TotalWork)
				skilled_kerbal_seconds = WorkLeft;
			var mass = skilled_kerbal_seconds/TotalWork*(PartMass-KitMass);
			required_energy = mass*GLB.EnergyForMetalwork;
			return mass;
		}

		public override void DoSomeWork(double skilled_kerbal_seconds)
		{
			WorkDone = Math.Min(TotalWork, WorkDone+skilled_kerbal_seconds);
			Completeness =(float)(WorkDone/TotalWork);
			Mass = Mathf.Lerp(KitMass, PartMass, Completeness);
			Cost = Mathf.Lerp(KitCost, PartCost, Completeness);
//			Utils.Log("Constructing: {} {}", Name, this);//debug
		}
	}
}

