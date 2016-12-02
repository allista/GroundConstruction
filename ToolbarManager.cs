//   ToolbarManager.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using UnityEngine;
using KSP.UI.Screens;
using AT_Utils;

namespace GroundConstruction
{
	/// <summary>
	/// Toolbar manager is needed becaus in KSP-1.0+ the ApplicationLauncher
	/// works differently: it only fires OnReady event at MainMenu and the first 
	/// time the Spacecenter is loaded. Thus we need to register the AppButton only 
	/// once and then just hide and show it using VisibleScenes, not removing it.
	/// IMHO, this is a bug in the RemoveModApplication method, cause if you use
	/// Add/RemoveModApp repeatedly, the buttons are duplicated each time.
	/// </summary>
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class GCToolbarManager : MonoBehaviour
	{
		static GCToolbarManager Instance;
		//icons
		const string TB_ICON = "GroundConstruction/Icons/toolbar-icon";
		const string AP_ICON = "GroundConstruction/Icons/applauncher-icon";
		static Texture texture;
		//buttons
		static ApplicationLauncherButton ALButton;
		const ApplicationLauncher.AppScenes AP_SCENES = ApplicationLauncher.AppScenes.FLIGHT|ApplicationLauncher.AppScenes.MAPVIEW|
			ApplicationLauncher.AppScenes.TRACKSTATION|ApplicationLauncher.AppScenes.SPACECENTER;
		
		static IButton TBButton;
		static readonly GameScenes[] TB_SCENES = {GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER};

		void Awake()
		{
			if(Instance != null) 
			{
				Destroy(gameObject);
				return;
			}
			DontDestroyOnLoad(this);
			Instance = this;
			init();
		}

		void init()
		{
			//setup toolbar/applauncher button
			if(ToolbarManager.ToolbarAvailable && !Globals.Instance.UseStockAppLauncher)
			{ 
				Utils.Log("Using Blizzy's toolbar");
				if(TBButton == null) AddToolbarButton(); 
				if(ALButton != null) ALButton.VisibleInScenes = ApplicationLauncher.AppScenes.NEVER;
			}
			else 
			{
				Utils.Log("Using stock AppLauncher");
				if(ALButton == null)
				{
					if(HighLogic.CurrentGame != null && ApplicationLauncher.Ready) AddAppLauncherButton();
					else GameEvents.onGUIApplicationLauncherReady.Add(AddAppLauncherButton);
				}
				else ALButton.VisibleInScenes = AP_SCENES;
				if(TBButton != null)
				{
					TBButton.Destroy();
					TBButton = null;
				}
			}
		}
		public static void Init() { if(Instance != null) Instance.init(); }

		//need to be instance method for Event.Add to work
		void AddAppLauncherButton()
		{
			if(!ApplicationLauncher.Ready || ALButton != null) return;
			Utils.Log("Adding AppLauncher button");
			texture  = GameDatabase.Instance.GetTexture(AP_ICON,  false);
			ALButton = ApplicationLauncher.Instance.AddModApplication(
				onAppLaunchToggleOn,
				onAppLaunchToggleOff,
				null, null, null, null,
				AP_SCENES,
				texture);
		}

		static void AddToolbarButton()
		{
			TBButton = ToolbarManager.Instance.add("GroundConstruction", "GroundConstructionButton");
			TBButton.TexturePath = TB_ICON;
			TBButton.ToolTip     = "Ground Construction";
			TBButton.Visibility  = new GameScenesVisibility(TB_SCENES);
			TBButton.Visible     = true;
			TBButton.OnClick    += onToolbarToggle;
		}

		static void onToolbarToggle(ClickEvent e) { GroundConstructionScenario.ToggleWindow(); }
		static void onAppLaunchToggleOn() { onToolbarToggle(null); }
		static void onAppLaunchToggleOff() { onToolbarToggle(null); }

		public static void SetDefaultButton()
		{
			if(TBButton != null) TBButton.TexturePath = TB_ICON;
			if(ALButton != null) ALButton.SetTexture(texture);
		}

//		public void Update()
//		{ 
//			if(TCA != null)
//			{
//				if(TBButton != null)
//				{
//					if(TCA.IsStateSet(TCAState.Enabled))
//						TBButton.TexturePath = TCA.State != TCAState.NoEC? ICON_ON : ICON_NC;
//					else TBButton.TexturePath = ICON_OFF;
//				}
//				if(ALButton != null) 
//				{
//					if(TCA.IsStateSet(TCAState.Enabled))
//						ALButton.SetTexture(TCA.State != TCAState.NoEC? texture : textureNoCharge);
//					else ALButton.SetTexture(textureOff);
//				}
//			}
//			else if(HighLogic.LoadedSceneIsEditor && TCAGuiEditor.Available)
//			{
//				if(TBButton != null) TBButton.TexturePath = ICON_OFF;
//				if(ALButton != null) ALButton.SetTexture(textureOff);
//			}
//			else SetDefaultButton();
//		}
	}
}

