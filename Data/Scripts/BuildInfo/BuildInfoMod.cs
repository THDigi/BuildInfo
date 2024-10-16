﻿using System;
using Digi.BuildInfo.Features;
using Digi.BuildInfo.Features.ChatCommands;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Features.ConfigMenu;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.ModderHelp;
using Digi.BuildInfo.Features.ModelPreview;
using Digi.BuildInfo.Features.Overlays;
using Digi.BuildInfo.Features.Overlays.ConveyorNetwork;
using Digi.BuildInfo.Features.ReloadTracker;
using Digi.BuildInfo.Features.Terminal;
using Digi.BuildInfo.Features.Terminal.Underlays;
using Digi.BuildInfo.Features.ToolbarInfo;
using Digi.BuildInfo.Features.Toolbars;
using Digi.BuildInfo.Features.Toolbars.FakeAPI;
using Digi.BuildInfo.Features.Tooltips;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using VRage.Game.Components;
using Whiplash.WeaponFramework;

// TODO: measuring tool?
// TODO: show acceleration somehow
// TODO: compute jumpdrive obstacles and show them? ... code looks nasty xD

namespace Digi.BuildInfo
{
    public class BuildInfoMod : ModBase<BuildInfoMod>
    {
        public const string ModName = "Build Info";

        // Utils
        public readonly Caches Caches;
        public readonly Constants Constants;
        public readonly DrawUtils DrawUtils;
        public readonly VanillaDefinitions VanillaDefinitions;
        public readonly HUDSounds HUDSounds;

        // Systems
        public readonly TextAPI TextAPI;
        public readonly GUIMonitor GUIMonitor;
        public readonly GameConfig GameConfig;
        public readonly BlockMonitor BlockMonitor;
        public readonly PlanetMonitor PlanetMonitor;
        public readonly InputLibHandler InputLibHandler;
        public readonly EquipmentMonitor EquipmentMonitor;
        public readonly GameBlockInfoHandler GameBlockInfoHandler;
        public readonly CoreSystemsAPIHandler CoreSystemsAPIHandler;
        public readonly WhipWeaponFrameworkAPI WhipWeaponFrameworkAPI;
        public readonly DefenseShieldsDetector DefenseShieldsDetector;
        public readonly RichHudFrameworkHandler RichHud;
        public readonly ModDetector ModDetector;
        public readonly SplitChecking SplitChecking;

        // Features
        public readonly Config Config;
        public readonly FontsHandler FontsHandler;
        public readonly ConfigMenuHandler ConfigMenuHandler;
        public readonly TextReplacer TextReplacer;
        public readonly MenuHandler MenuHandler;
        public readonly HUDEditor HUDEditor;
        public readonly ScreenTooltips ScreenTooltips;
        public readonly JumpDriveMonitor JumpDriveMonitor;
        public readonly ProjectedBlockInfo ProjectedBlockInfo;
        public readonly ProjectorShowRemaining ProjectorShowRemaining;
        public readonly ShipOverlays ShipOverlays;
        public readonly ConveyorNetworkView ConveyorNetworkView;
        public readonly Overlays Overlays;
        public readonly SpecializedOverlays SpecializedOverlays;
        public readonly LockOverlay LockOverlay;
        public readonly SubpartPreview SubpartPreview;
        public readonly ConstructionModelPreview ConstructionModelPreview;
        public readonly PickBlock PickBlock;
        public readonly QuickMenu QuickMenu;
        public readonly AnalyseShip AnalyseShip;
        public readonly ChatCommandHandler ChatCommandHandler;
        public readonly ReloadTracking ReloadTracking;
        public readonly TerminalInfo TerminalInfo;
        public readonly DetailInfoButtons DetailInfoButtons;
        public readonly MultiDetailInfo MultiDetailInfo;
        public readonly TerminalUnderlays TerminalUnderlays;
        public readonly TextGeneration TextGeneration;
        public readonly CrosshairMessages CrosshairMessages;
        public readonly LiveDataHandler LiveDataHandler;
        public readonly TurretHUD TurretHUD;
        public readonly WeaponModeIndicator WeaponModeIndicator;
        public readonly PlacementDistance PlacementDistance;
        public readonly CubeBuilderAdditions CubeBuilderAdditions;
        public readonly BlockInfoAdditions BlockInfoAdditions;
        public readonly OverrideToolSelectionDraw OverrideToolSelectionDraw;
        public readonly RelativeDampenerInfo RelativeDampenerInfo;
        public readonly ShipToolInventoryBar ShipToolInventoryBar;
        public readonly BlockInfoScrollComponents BlockInfoScrollComponents;
        public readonly WhatsNew WhatsNew;
        public readonly TooltipHandler TooltipHandler;
        public readonly BlueprintTooltips BlueprintTooltips;
        public readonly BlockDescriptions BlockDescriptions;
        public readonly ItemTooltips ItemTooltips;
        public readonly IconOverlays IconOverlays;
        public readonly Inventories Inventories;
        public readonly ToolbarTracker ToolbarTracker;
        public readonly ToolbarInfoInMenu ToolbarInfoInMenu;
        public readonly ToolbarOverride ToolbarOverride;
        public readonly ToolbarMonitor ToolbarMonitor;
        public readonly ToolbarCustomLabels ToolbarCustomLabels;
        public readonly ToolbarStatusProcessor ToolbarStatusProcessor;
        public readonly ToolbarLabelRender ToolbarLabelRender;
        public readonly EventToolbarMonitor EventToolbarMonitor;
        public readonly EventToolbarInfo EventToolbarInfo;
        public readonly GridMassCompute GridMassCompute;
        public readonly PBMonitor PBMonitor;
        public readonly TopPartColor TopPartColor;
        public readonly FixTextures FixTextures;
        public readonly SpectatorControlInfo SpectatorControlInfo;
        public readonly InterModAPI InterModAPI;
        public readonly DebugEvents DebugEvents;
        public readonly DebugLog DebugLog;
        public readonly ModderHelpMain ModderHelpMain;
        public readonly CheckRestartRequired CheckRestartRequired;
        public readonly ProfilerDisplay ProfilerDisplay;

        public static bool IsDevMod { get; private set; } = false;

        public static int MainThreadId { get; private set; } = 1;

        public BuildInfoMod(BuildInfo_GameSession session) : base(ModName, session, MyUpdateOrder.AfterSimulation)
        {
            IsDevMod = (IsLocalMod && session?.ModContext?.ModId == "BuildInfo.dev");

            MainThreadId = Environment.CurrentManagedThreadId;

            // Utils
            Caches = new Caches(this);
            Constants = new Constants(this);
            DrawUtils = new DrawUtils(this);
            VanillaDefinitions = new VanillaDefinitions(this);
            HUDSounds = new HUDSounds(this);

            // Systems
            TextAPI = new TextAPI(this);
            InputLibHandler = new InputLibHandler(this);
            GUIMonitor = new GUIMonitor(this);
            GameConfig = new GameConfig(this);
            BlockMonitor = new BlockMonitor(this);
            PlanetMonitor = new PlanetMonitor(this);
            EquipmentMonitor = new EquipmentMonitor(this);
            GameBlockInfoHandler = new GameBlockInfoHandler(this);
            CoreSystemsAPIHandler = new CoreSystemsAPIHandler(this);
            WhipWeaponFrameworkAPI = new WhipWeaponFrameworkAPI(this);
            DefenseShieldsDetector = new DefenseShieldsDetector(this);
            RichHud = new RichHudFrameworkHandler(this);
            ModDetector = new ModDetector(this);
            SplitChecking = new SplitChecking(this);

            // Features
            Config = new Config(this);
            FontsHandler = new FontsHandler(this);
            ConfigMenuHandler = new ConfigMenuHandler(this);
            TextReplacer = new TextReplacer(this);
            MenuHandler = new MenuHandler(this);
            JumpDriveMonitor = new JumpDriveMonitor(this);
            HUDEditor = new HUDEditor(this);
            ScreenTooltips = new ScreenTooltips(this);
            ProjectedBlockInfo = new ProjectedBlockInfo(this);
            ProjectorShowRemaining = new ProjectorShowRemaining(this);
            OverrideToolSelectionDraw = new OverrideToolSelectionDraw(this);
            ShipOverlays = new ShipOverlays(this);
            ConveyorNetworkView = new ConveyorNetworkView(this);
            Overlays = new Overlays(this);
            SpecializedOverlays = new SpecializedOverlays(this);
            LockOverlay = new LockOverlay(this);
            SubpartPreview = new SubpartPreview(this);
            ConstructionModelPreview = new ConstructionModelPreview(this);
            PickBlock = new PickBlock(this);
            QuickMenu = new QuickMenu(this);
            AnalyseShip = new AnalyseShip(this);
            ChatCommandHandler = new ChatCommandHandler(this);
            ReloadTracking = new ReloadTracking(this);
            TerminalInfo = new TerminalInfo(this);
            DetailInfoButtons = new DetailInfoButtons(this);
            MultiDetailInfo = new MultiDetailInfo(this);
            TerminalUnderlays = new TerminalUnderlays(this);
            TextGeneration = new TextGeneration(this);
            CrosshairMessages = new CrosshairMessages(this);
            LiveDataHandler = new LiveDataHandler(this);
            PlacementDistance = new PlacementDistance(this);
            CubeBuilderAdditions = new CubeBuilderAdditions(this);
            BlockInfoAdditions = new BlockInfoAdditions(this);
            RelativeDampenerInfo = new RelativeDampenerInfo(this);
            ShipToolInventoryBar = new ShipToolInventoryBar(this);
            BlockInfoScrollComponents = new BlockInfoScrollComponents(this);
            TurretHUD = new TurretHUD(this);
            WeaponModeIndicator = new WeaponModeIndicator(this);
            WhatsNew = new WhatsNew(this);
            TooltipHandler = new TooltipHandler(this);
            BlueprintTooltips = new BlueprintTooltips(this);
            BlockDescriptions = new BlockDescriptions(this);
            ItemTooltips = new ItemTooltips(this);
            IconOverlays = new IconOverlays(this);
            Inventories = new Inventories(this);
            ToolbarTracker = new ToolbarTracker(this);
            ToolbarInfoInMenu = new ToolbarInfoInMenu(this);
            ToolbarOverride = new ToolbarOverride(this);
            ToolbarMonitor = new ToolbarMonitor(this);
            ToolbarCustomLabels = new ToolbarCustomLabels(this);
            ToolbarStatusProcessor = new ToolbarStatusProcessor(this);
            ToolbarLabelRender = new ToolbarLabelRender(this);
            EventToolbarMonitor = new EventToolbarMonitor(this);
            EventToolbarInfo = new EventToolbarInfo(this);
            GridMassCompute = new GridMassCompute(this);
            PBMonitor = new PBMonitor(this);
            TopPartColor = new TopPartColor(this);
            FixTextures = new FixTextures(this);
            SpectatorControlInfo = new SpectatorControlInfo(this);
            InterModAPI = new InterModAPI(this);
            DebugEvents = new DebugEvents(this);
            DebugLog = new DebugLog(this);
            ModderHelpMain = new ModderHelpMain(this);
            CheckRestartRequired = new CheckRestartRequired(this);
            ProfilerDisplay = new ProfilerDisplay(this);
        }
    }
}