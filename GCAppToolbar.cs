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
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class GCAppToolbar : AppToolbar<GCAppToolbar>
    {
        protected override string TB_ICON => "GroundConstruction/Icons/toolbar-icon";
        protected override string AL_ICON => "GroundConstruction/Icons/applauncher-icon";

        protected override ApplicationLauncher.AppScenes AL_SCENES =>
        ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW |
            ApplicationLauncher.AppScenes.TRACKSTATION | ApplicationLauncher.AppScenes.SPACECENTER;

        protected override GameScenes[] TB_SCENES =>
        new[] { GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER };

        protected override string button_tooltip => "Global Construction";

        protected override bool ForceAppLauncher => Globals.Instance.UseStockAppLauncher;

        protected override void onLeftClick()
        {
            GroundConstructionScenario.ToggleWindow();
        }
    }
}
