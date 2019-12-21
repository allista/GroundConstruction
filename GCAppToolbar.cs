//   ToolbarManager.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using KSP.UI.Screens;
using AT_Utils;

namespace GroundConstruction
{
    [KSPAddon(KSPAddon.Startup.EveryScene, true)]
    public class GCAppToolbar : AppToolbar<GCAppToolbar>
    {
        protected override string TB_ICON => "GroundConstruction/Icons/toolbar-icon";
        protected override string AL_ICON => "GroundConstruction/Icons/applauncher-icon";

        protected override ApplicationLauncher.AppScenes AL_SCENES =>
        ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW |
            ApplicationLauncher.AppScenes.TRACKSTATION | ApplicationLauncher.AppScenes.SPACECENTER |
            ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.VAB;

        protected override GameScenes[] TB_SCENES =>
        new[] { GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER, GameScenes.CREDITS };

        protected override string button_tooltip => "Global Construction";

        protected override bool ForceAppLauncher => Globals.Instance.UseStockAppLauncher;

        protected override void onLeftClick()
        {
            if(HighLogic.LoadedSceneIsEditor)
                GCEditorGUI.ToggleWithButton(ALButton);
            else
                GroundConstructionScenario.ToggleWindow();
        }
    }
}
