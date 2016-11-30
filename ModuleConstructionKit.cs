//   ModuleConstructionKit.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using AT_Utils;

namespace GroundConstruction
{
	public class ModuleConstructionKit : PartModule, IPartCostModifier, IPartMassModifier, iDIYKit
	{
		static Globals GLB { get { return Globals.Instance; } }

		Transform model;
		Transform spawn_transform_VAB;
		Transform spawn_transform_SPH;
		[KSPField] public string SpawnTransformVAB;
		[KSPField] public string SpawnTransformSPH;

		TextureSwitcherServer texture_switcher;
		[KSPField] public string TextureVAB;
		[KSPField] public string TextureSPH;
		[KSPField(isPersistant = true)] public EditorFacility Facility;

		[KSPField(isPersistant = true)] public Vector3 OrigScale;
		[KSPField(isPersistant = true)] public Vector3 OrigSize;
		[KSPField(isPersistant = true)] public Vector3 Size;

		[KSPField(isPersistant = true)] public float DeploymentTime;
		[KSPField(isPersistant = true)] public float DeployingSpeed;

		[KSPField(isPersistant = true)] public bool Deploying;
		[KSPField(isPersistant = true)] public bool Deployed;
		[KSPField(isPersistant = true)] public bool LaunchAllowed;

		[KSPField(guiName = "Kit", guiActive = true, guiActiveEditor = true)]
		public string KitName = "None";

		[KSPField(guiName = "Kit Mass", guiActive = true, guiActiveEditor = true, guiFormat = "0.0t")]
		public float KitMass;

		[KSPField(guiName = "Kit Cost", guiActive = true, guiActiveEditor = true, guiFormat = "0.0F")]
		public float KitCost;

		[KSPField(guiName = "Kit Work", guiActive = true, guiActiveEditor = true, guiFormat = "0.0 SKH")]
		public float KitWork;

		[KSPField(guiName = "Kit Status", guiActive = true)]
		public string KitStatus = "Empty Kit";

		[KSPField(guiName = "Bulding", guiActive = true)]
		public string PartStatus = "Nothing";

		#region Kit
		[KSPField(isPersistant = true)] public VesselKit kit = new VesselKit();

		public bool Valid { get { return part != null && vessel != null && kit.Valid; } }

		public float Completeness { get { return kit.Valid? kit.Completeness : 0; } }

		public float PartCompleteness 
		{ get { return kit.Valid && kit.PartUnderConstruction != null? kit.PartUnderConstruction.Completeness : 0; } }

		public VesselResources GetConstructResources()
		{
			if(Completeness < 1) return null;
			return new VesselResources(kit.Blueprint);
		}
		#endregion

		#region Anchor
		FixedJoint anchorJoint;
		GameObject anchor;

		void attach_anchor()
		{
			detach_anchor();
			anchor = new GameObject("AnchorBody");
			var rb = anchor.AddComponent<Rigidbody>();
			rb.isKinematic = true;
			anchor.transform.position = part.transform.position;
			anchor.transform.rotation = part.transform.rotation;
			anchorJoint = anchor.AddComponent<FixedJoint>();
			anchorJoint.breakForce = 1e6f;
			anchorJoint.breakTorque = 1e6f;
			anchorJoint.connectedBody = part.Rigidbody;
		}

		void detach_anchor()
		{
			if(anchor) Destroy(anchor);
			if(anchorJoint) Destroy(anchorJoint);
		}
		#endregion

		void update_texture()
		{
			if(texture_switcher == null ||
			   Facility == EditorFacility.None) return;
			texture_switcher.SetTexture(Facility == EditorFacility.VAB? 
			                            TextureVAB : TextureSPH);
		}

		void update_part_info()
		{
			if(kit.Valid)
			{
				KitName = kit.Name;
				KitMass = kit.Mass;
				KitCost = kit.Cost;
				KitWork = (float)(kit.TotalWork-kit.WorkDone)/18000;
				if(Deploying) KitStatus = string.Format("Deployed: {0:P1}", DeploymentTime);
				else if(Deployed) 
				{
					KitStatus = string.Format("Complete: {0:P1}", kit.Completeness);
					PartStatus = kit.PartUnderConstruction == null? "Nothing" :
						string.Format("{0}: {1:P1}", 
						              kit.PartUnderConstruction.Title, 
						              kit.PartUnderConstruction.Completeness);
				}
				else KitStatus = "Idle";
			}
			else
			{
				KitName = "None";
				KitMass = 0;
				KitCost = 0;
				KitWork = 0;
				KitStatus = "Empty";
				PartStatus = "Nothing";
			}
		}

		void update_model(bool initial)
		{
			//rescale part
			var scale = Vector3.Scale(Size, OrigSize.Inverse());
			var local_scale = Vector3.Scale(OrigScale, scale);
			var rel_scale = Vector3.Scale(local_scale, model.localScale.Inverse());
			model.localScale = local_scale;
			model.hasChanged = true;
			part.transform.hasChanged = true;
			//update attach nodes and attached parts
			var scale_quad = rel_scale.sqrMagnitude;
			for(int i = 0, count = part.attachNodes.Count; i < count; i++)
			{
				//update node position
				var node = part.attachNodes[i];
				node.position = Vector3.Scale(node.originalPosition, scale);
				part.UpdateAttachedPartPos(node);
				//update node breaking forces
				node.breakingForce *= scale_quad;
				node.breakingTorque *= scale_quad;
			}
			//update this surface attach node
			if(part.srfAttachNode != null)
			{
				Vector3 old_position = part.srfAttachNode.position;
				part.srfAttachNode.position = Vector3.Scale(part.srfAttachNode.originalPosition, scale);
				//don't move the part at start, its position is persistant
				if(!initial)
				{
					Vector3 d_pos = part.transform.TransformDirection(part.srfAttachNode.position - old_position);
					part.transform.position -= d_pos;
				}
			}
			//no need to update surface attached parts on start
			//as their positions are persistant; less calculations
			if(initial) return;
			//update parts that are surface attached to this
			for(int i = 0, count = part.children.Count; i < count; i++)
			{
				var child = part.children[i];
				if(child.srfAttachNode != null && child.srfAttachNode.attachedPart == part)
				{
					Vector3 attachedPosition = child.transform.localPosition + child.transform.localRotation * child.srfAttachNode.position;
					Vector3 targetPosition = Vector3.Scale(attachedPosition, rel_scale);
					child.transform.Translate(targetPosition - attachedPosition, part.transform);
				}
			}
		}

		void setup_transform(string transform_name, out Transform T)
		{
			T = null;
			if(!string.IsNullOrEmpty(transform_name))
				T = part.FindModelTransform(transform_name);
			if(T == null) T = part.transform;
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			Events["Deploy"].active = kit.Valid && !Deployed && !Deploying;
			Events["Launch"].active = kit.Valid &&  Deployed && LaunchAllowed && kit.Completeness >= 1;
			model = part.transform.Find("model");
			setup_transform(SpawnTransformVAB, out spawn_transform_VAB);
			setup_transform(SpawnTransformSPH, out spawn_transform_SPH);
			if(!string.IsNullOrEmpty(TextureVAB) && !string.IsNullOrEmpty(TextureSPH))
				texture_switcher = part.Modules.GetModule<TextureSwitcherServer>();
		}

		void OnPartPack() { detach_anchor(); }
		void OnPartUnpack() { if(Deployed) attach_anchor(); }
		void OnDestroy() { detach_anchor(); }

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			var metric = new Metric(part);
			model = part.transform.Find("model");
			OrigSize = metric.size;
			OrigScale = model.localScale;
			if(kit.Valid)
			{
				update_model(true);
				update_part_info();
			}
//			this.Log("OnLoad: node: {}\n\nkit: {}", node, kit);//debug
		}

		void Update()
		{
			if(HighLogic.LoadedSceneIsEditor && kit.Valid &&
			   model.localScale == OrigScale)
				update_model(true);
			if(Deployed)
			{
				if(!anchor || !anchorJoint || !anchor.GetComponent<FixedJoint>())
					attach_anchor();
			}
			else if(Deploying)
			{
				if(deployment == null) deployment = deploy();
				if(!deployment.MoveNext()) deployment = null;
			}
			update_part_info();
		}

		#region Select Ship Construct
		CraftBrowserDialog vessel_selector;

		[KSPEvent(guiName = "Select Vessel", guiActive = false, guiActiveEditor = true, active = true)]
		public void SelectVessel()
		{
			if(vessel_selector != null) return;
			vessel_selector = 
				CraftBrowserDialog.Spawn(
					EditorLogic.fetch.ship.shipFacility,
					HighLogic.SaveFolder,
					vessel_selected,
					selection_canceled, false);
		}

		IEnumerator<YieldInstruction> delayed_store_construct(ShipConstruct construct)
		{
			if(construct == null) yield break;
			Utils.LockEditor("construct_loading");
			for(int i = 0; i < 3; i++) yield return null;
			kit = new VesselKit(construct);
			KitName = kit.Name;
			KitMass = kit.Mass;
			KitCost = kit.Cost;
			var V = OrigSize.x*OrigSize.y*OrigSize.z;
			Size = OrigSize * Mathf.Pow(V/kit.Mass*GLB.VesselKitDensity, 1/3f);
			Size = Size.ClampComponentsL(GLB.VesselKitMinSize);
			Facility = construct.shipFacility;
			update_texture();
			update_model(false);
			construct.Unload();
			Utils.LockEditor("construct_loading", false);
		}

		void vessel_selected(string filename, CraftBrowserDialog.LoadType t)
		{
			vessel_selector = null;
			EditorLogic EL = EditorLogic.fetch;
			if(EL == null) return;
			//load vessel config
			var node = ConfigNode.Load(filename);
			if(node == null) return;
			var construct = new ShipConstruct();
			if(!construct.LoadShip(node))
			{
				Utils.Log("Unable to load ShipConstruct from {}. " +
				          "This usually means that some parts are missing " +
				          "or some modules failed to initialize.", filename);
				Utils.Message("Unable to load {0}", filename);
				return;
			}
			//check if the construct contains launch clamps
//			if(construct.HasLaunchClamp())
//			{
//				Utils.Message("\"{0}\" has launch clamps. Remove them before storing.", construct.shipName);
//				construct.Unload();
//				return;
//			}
			//check if it's possible to launch such vessel
			bool cant_launch = false;
			var preFlightCheck = new PreFlightCheck(new Callback(() => cant_launch = false), new Callback(() => cant_launch = true));
			preFlightCheck.AddTest(new PreFlightTests.ExperimentalPartsAvailable(construct));
			preFlightCheck.RunTests(); 
			//cleanup loaded parts and try to store construct
			if(cant_launch) construct.Unload();
			else StartCoroutine(delayed_store_construct(construct));
		}
		void selection_canceled() { vessel_selector = null; }
		#endregion

		#region Deployment
		bool can_deploy()
		{
			if(!kit.Valid)
			{
				Utils.Message("Cannot deploy: construction kit is empty.");
				return false;
			}
			if(vessel.packed)
			{
				Utils.Message("Cannot deploy construction kit now.");
				return false;
			}
			if(!vessel.Landed)
			{
				Utils.Message("Cannot deploy construction kit unless landed.");
				return false;
			}
			if(vessel.srfSpeed > GLB.DeployMaxSpeed)
			{
				Utils.Message("Cannot deploy construction kit while mooving.");
				return false;
			}
			if(vessel.angularVelocity.sqrMagnitude > GLB.DeployMaxAV)
			{
				Utils.Message("Cannot deploy construction kit while rotating.");
				return false;
			}
			return true;
		}

		IEnumerator decouple_attached_parts()
		{
			if(part.parent) part.decouple(2);
			yield return null;
			while(part.children.Count > 0)
			{
				part.children[0].decouple(2);
				yield return null;
			}
		}

		IEnumerator deployment;
		IEnumerator deploy()
		{
			var decoupler = decouple_attached_parts();
			while(decoupler.MoveNext())
				yield return decoupler.Current;
			yield return null;
			var start = Size;
			var start_time = DeploymentTime;
			var start_local_size = Vector3.Scale(OrigScale, OrigSize.Inverse());
			var end = kit.ShipMetric.size;
			while(DeploymentTime < 1)
			{
				DeploymentTime += DeployingSpeed*TimeWarp.deltaTime;
				Size = Vector3.Lerp(start, end, DeploymentTime-start_time);
				model.localScale = Vector3.Scale(Size, start_local_size);
				model.hasChanged = true;
				part.transform.hasChanged = true;
				yield return null;
			}
			Size = end;
			attach_anchor();
			Deploying = false;
			Deployed = true;
		}

		[KSPEvent(guiName = "Deploy", guiActive = true, active = true)]
		public void Deploy()
		{
			if(!can_deploy()) return;
			Events["Deploy"].active = false;
			DeployingSpeed = GLB.DeploymentSpeed/kit.ShipMetric.volume;
			Deploying = true;
		}
		#endregion

		#region Launching
		[KSPEvent(guiName = "Launch", guiActive = true, active = false)]
		public void Launch()
		{
			if(!can_launch()) return;
			StartCoroutine(launch_complete_construct());
		}

		public void AllowLaunch(bool allow = true)
		{ 
			LaunchAllowed = allow;
			Events["Launch"].active = allow; 
		}

		bool can_launch()
		{
			if(launch_in_progress) return false;
			if(!can_deploy()) return false;
			if(kit.Completeness < 1)
			{
				Utils.Message("The assembly is not complete yet.");
				return false;
			}
			return true;
		}

		bool launch_in_progress;
		Vessel launched_vessel;
		IEnumerator<YieldInstruction> launch_complete_construct()
		{
			if(!HighLogic.LoadedSceneIsFlight) yield break;
			while(!FlightGlobals.ready) yield return null;
			launch_in_progress = true;
//			this.Log("loading ship construct: {}", kit.Name);//debug
			var construct = kit.LoadConstruct();
			if(construct == null) 
			{
				Utils.Log("PackedConstruct: unable to load ShipConstruct {}. " +
				          "This usually means that some parts are missing " +
				          "or some modules failed to initialize.", kit.Name);
				Utils.Message("Something whent wrong. Constructed ship cannot be launched.");
				launch_in_progress = false;
				yield break;
			}
			//check if all the parts were indeed constructed
			var parts_state = kit.BuiltPartsState();
			if(construct.Parts.Any(p => !parts_state.ContainsKey(p.craftID) || parts_state[p.craftID] < 1))
			{
				Utils.Message("Something whent wrong. Not all parts were properly constructed.");
				construct.Unload();
				launch_in_progress = false;
				yield break;
			}
			Utils.SaveGame(kit.Name+"-before_launch");
			model.gameObject.SetActive(false);
			FXMonger.Explode(part, part.partTransform.position, 0);
//			this.Log("launching new vessel: {}", kit.Name);//debug
			var launch_transform = Facility == EditorFacility.VAB? 
				spawn_transform_VAB : spawn_transform_SPH;
			if(FlightGlobals.ready)
				FloatingOrigin.SetOffset(launch_transform.position);
			ShipConstruction.PutShipToGround(construct, launch_transform);
			ShipConstruction.AssembleForLaunch(construct, 
			                                   vessel.landedAt, part.flagURL, 
			                                   FlightDriver.FlightStateCache,
			                                   new VesselCrewManifest());
			launched_vessel = FlightGlobals.Vessels[FlightGlobals.Vessels.Count - 1];
//			this.Log("launched vessel: {}", launched_vessel);//debug
			while(!launched_vessel.loaded || launched_vessel.packed) yield return null;
//			this.Log("launched vessel loaded and unpacked: {}", launched_vessel);//debug
			FlightGlobals.ForceSetActiveVessel(launched_vessel);
			StageManager.BeginFlight();
			launch_in_progress = false;
			launched_vessel = null;
//			this.Log("dying...");//debug
			vessel.Die();
		}
		#endregion

		#region iDIYKit implementation
		public double RequiredMass(ref double skilled_kerbal_seconds, out double required_energy)
		{
			required_energy = 0;
			if(!kit.Valid) return 0;
			return kit.RequiredMass(ref skilled_kerbal_seconds, out required_energy);
		}

		public void DoSomeWork(double skilled_kerbal_seconds)
		{
			if(!kit.Valid) return;
			kit.DoSomeWork(skilled_kerbal_seconds);
			if(kit.Completeness >= 1)
				TimeWarp.SetRate(0, false);
		}
		#endregion

		#region IPartCostModifier implementation
		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
		{ return kit.Valid? kit.Cost : 0; }

		public ModifierChangeWhen GetModuleCostChangeWhen()
		{ return ModifierChangeWhen.CONSTANTLY; }
		#endregion

		#region IPartMassModifier implementation
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
		{ return kit.Valid? kit.Mass : 0; }

		public ModifierChangeWhen GetModuleMassChangeWhen()
		{ return ModifierChangeWhen.CONSTANTLY; }
		#endregion
	}
}

