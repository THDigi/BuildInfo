using Digi.BuildInfo.Features;
using Digi.BuildInfo.Features.ChatCommands;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Features.ConfigMenu;
using Digi.BuildInfo.Features.LeakInfo;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.ModelPreview;
using Digi.BuildInfo.Features.Overlays;
using Digi.BuildInfo.Features.ReloadTracker;
using Digi.BuildInfo.Features.Terminal;
using Digi.BuildInfo.Features.ToolbarInfo;
using Digi.BuildInfo.Features.Tooltips;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using VRage.Game.Components;
using Whiplash.WeaponFramework;

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

        // Systems
        public readonly TextAPI TextAPI;
        public readonly GameConfig GameConfig;
        public readonly GUIMonitor GUIMonitor;
        public readonly BlockMonitor BlockMonitor;
        public readonly InputLibHandler InputLibHandler;
        public readonly EquipmentMonitor EquipmentMonitor;
        public readonly GameBlockInfoHandler GameBlockInfoHandler;
        public readonly WeaponCoreAPIHandler WeaponCoreAPIHandler;
        public readonly WhipWeaponFrameworkAPI WhipWeaponFrameworkAPI;
        public readonly DefenseShieldsDetector DefenseShieldsDetector;
        public readonly RichHudFrameworkHandler RichHud;

        // Features
        public readonly Config Config;
        public readonly FontsHandler FontsHandler;
        public readonly ConfigMenuHandler ConfigMenuHandler;
        public readonly JumpDriveMonitor JumpDriveMonitor;
        public readonly ProjectedBlockInfo ProjectedBlockInfo;
        public readonly LeakInfo LeakInfo;
        public readonly Overlays Overlays;
        public readonly SpecializedOverlays SpecializedOverlays;
        public readonly LockOverlay LockOverlay;
        public readonly ModelPreview ModelPreview;
        public readonly PickBlock PickBlock;
        public readonly QuickMenu QuickMenu;
        public readonly AnalyseShip AnalyseShip;
        public readonly ChatCommandHandler ChatCommandHandler;
        public readonly ReloadTracking ReloadTracking;
        public readonly TerminalInfo TerminalInfo;
        public readonly DetailInfoButtons DetailInfoButtons;
        public readonly MultiDetailInfo MultiDetailInfo;
        public readonly TextGeneration TextGeneration;
        public readonly CrosshairMessages CrosshairMessages;
        public readonly LiveDataHandler LiveDataHandler;
        public readonly TurretHUD TurretHUD;
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
        public readonly Inventories Inventories;
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
        public readonly InterModAPI InterModAPI;
        public readonly DebugEvents DebugEvents;
        public readonly DebugLog DebugLog;

        public static bool IsDevMod { get; private set; } = false;

        public BuildInfoMod(BuildInfo_GameSession session) : base(ModName, session, MyUpdateOrder.AfterSimulation)
        {
            IsDevMod = (IsLocalMod && session?.ModContext?.ModId == "BuildInfo.dev");

            // Utils
            Caches = new Caches(this);
            Constants = new Constants(this);
            DrawUtils = new DrawUtils(this);
            VanillaDefinitions = new VanillaDefinitions(this);

            // Systems
            TextAPI = new TextAPI(this);
            InputLibHandler = new InputLibHandler(this);
            GameConfig = new GameConfig(this);
            GUIMonitor = new GUIMonitor(this);
            BlockMonitor = new BlockMonitor(this);
            EquipmentMonitor = new EquipmentMonitor(this);
            GameBlockInfoHandler = new GameBlockInfoHandler(this);
            WeaponCoreAPIHandler = new WeaponCoreAPIHandler(this);
            WhipWeaponFrameworkAPI = new WhipWeaponFrameworkAPI(this);
            DefenseShieldsDetector = new DefenseShieldsDetector(this);
            RichHud = new RichHudFrameworkHandler(this);

            // Features
            Config = new Config(this);
            FontsHandler = new FontsHandler(this);
            ConfigMenuHandler = new ConfigMenuHandler(this);
            JumpDriveMonitor = new JumpDriveMonitor(this);
            ProjectedBlockInfo = new ProjectedBlockInfo(this);
            OverrideToolSelectionDraw = new OverrideToolSelectionDraw(this);
            Overlays = new Overlays(this);
            SpecializedOverlays = new SpecializedOverlays(this);
            LockOverlay = new LockOverlay(this);
            ModelPreview = new ModelPreview(this);
            LeakInfo = new LeakInfo(this);
            PickBlock = new PickBlock(this);
            QuickMenu = new QuickMenu(this);
            AnalyseShip = new AnalyseShip(this);
            ChatCommandHandler = new ChatCommandHandler(this);
            ReloadTracking = new ReloadTracking(this);
            TerminalInfo = new TerminalInfo(this);
            DetailInfoButtons = new DetailInfoButtons(this);
            MultiDetailInfo = new MultiDetailInfo(this);
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
            WhatsNew = new WhatsNew(this);
            TooltipHandler = new TooltipHandler(this);
            BlueprintTooltips = new BlueprintTooltips(this);
            BlockDescriptions = new BlockDescriptions(this);
            ItemTooltips = new ItemTooltips(this);
            Inventories = new Inventories(this);
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
            InterModAPI = new InterModAPI(this);
            DebugEvents = new DebugEvents(this);
            DebugLog = new DebugLog(this);
        }
    }
}