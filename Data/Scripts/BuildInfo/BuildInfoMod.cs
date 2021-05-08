using Digi.BuildInfo.Features;
using Digi.BuildInfo.Features.ChatCommands;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Features.LeakInfo;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.Overlays;
using Digi.BuildInfo.Features.ReloadTracker;
using Digi.BuildInfo.Features.ConfigMenu;
using Digi.BuildInfo.Features.ToolbarInfo;
using Digi.BuildInfo.Features.Tooltips;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using VRage.Game.Components;
using Digi.BuildInfo.Features.Terminal;

namespace Digi.BuildInfo
{
    public class BuildInfoMod : ModBase<BuildInfoMod>
    {
        public const string MOD_NAME = "Build Info";

        // Utils
        public Caches Caches;
        public Constants Constants;
        public DrawUtils DrawUtils;
        public VanillaDefinitions VanillaDefinitions;

        // Systems
        public TextAPI TextAPI;
        public GameConfig GameConfig;
        public BlockMonitor BlockMonitor;
        public InputLibHandler InputLibHandler;
        public EquipmentMonitor EquipmentMonitor;
        public WeaponCoreAPIHandler WeaponCoreAPIHandler;

        // Features
        public Config Config;
        public LegacyConfig LegacyConfig;
        public FontsHandler FontsHandler;
        public ConfigMenuHandler ConfigMenuHandler;
        public JumpDriveMonitor JumpDriveMonitor;
        public LeakInfo LeakInfo;
        public Overlays Overlays;
        public LockOverlay LockOverlay;
        public PickBlock PickBlock;
        public QuickMenu QuickMenu;
        public AnalyseShip AnalyseShip;
        public ChatCommandHandler ChatCommandHandler;
        public ReloadTracking ReloadTracking;
        public TerminalInfo TerminalInfo;
        public DetailInfoRefresh DetailInfoRefresh;
        public TextGeneration TextGeneration;
        public LiveDataHandler LiveDataHandler;
        public TurretHUD TurretHUD;
        public PlacementDistance PlacementDistance;
        public BlockInfoAdditions BlockInfoAdditions;
        public ProjectedBlockInfo ProjectedBlockInfo;
        public OverrideToolSelectionDraw OverrideToolSelectionDraw;
        public RelativeDampenerInfo RelativeDampenerInfo;
        public ShipToolInventoryBar ShipToolInventoryBar;
        public BlockInfoScrollComponents BlockInfoScrollComponents;
        public WhatsNew WhatsNew;
        public TooltipHandler TooltipHandler;
        public BlueprintTooltips BlueprintTooltips;
        public ItemTooltips ItemTooltips;
        public ToolbarOverride ToolbarOverride;
        public ToolbarMonitor ToolbarMonitor;
        public ToolbarCustomLabels ToolbarCustomLabels;
        public ToolbarStatusProcessor ToolbarStatusProcessor;
        public ToolbarLabelRender ToolbarLabelRender;
        public GridMassCompute GridMassCompute;
        public PBMonitor PBMonitor;
        public InterModAPI InterModAPI;
        public DebugEvents DebugEvents;
        public DebugLog DebugLog;

        public BuildInfoMod(BuildInfo_GameSession session) : base(MOD_NAME, session, MyUpdateOrder.AfterSimulation)
        {
            // Utils
            Caches = new Caches(this);
            Constants = new Constants(this);
            DrawUtils = new DrawUtils(this);
            VanillaDefinitions = new VanillaDefinitions(this);

            // Systems
            TextAPI = new TextAPI(this);
            InputLibHandler = new InputLibHandler(this);
            GameConfig = new GameConfig(this);
            BlockMonitor = new BlockMonitor(this);
            EquipmentMonitor = new EquipmentMonitor(this);
            WeaponCoreAPIHandler = new WeaponCoreAPIHandler(this);

            // Features
            Config = new Config(this);
            LegacyConfig = new LegacyConfig(this);
            FontsHandler = new FontsHandler(this);
            ConfigMenuHandler = new ConfigMenuHandler(this);
            JumpDriveMonitor = new JumpDriveMonitor(this);
            ProjectedBlockInfo = new ProjectedBlockInfo(this);
            OverrideToolSelectionDraw = new OverrideToolSelectionDraw(this);
            Overlays = new Overlays(this);
            LockOverlay = new LockOverlay(this);
            LeakInfo = new LeakInfo(this);
            PickBlock = new PickBlock(this);
            QuickMenu = new QuickMenu(this);
            AnalyseShip = new AnalyseShip(this);
            ChatCommandHandler = new ChatCommandHandler(this);
            ReloadTracking = new ReloadTracking(this);
            TerminalInfo = new TerminalInfo(this);
            DetailInfoRefresh = new DetailInfoRefresh(this);
            TextGeneration = new TextGeneration(this);
            LiveDataHandler = new LiveDataHandler(this);
            PlacementDistance = new PlacementDistance(this);
            BlockInfoAdditions = new BlockInfoAdditions(this);
            RelativeDampenerInfo = new RelativeDampenerInfo(this);
            ShipToolInventoryBar = new ShipToolInventoryBar(this);
            BlockInfoScrollComponents = new BlockInfoScrollComponents(this);
            TurretHUD = new TurretHUD(this);
            WhatsNew = new WhatsNew(this);
            TooltipHandler = new TooltipHandler(this);
            BlueprintTooltips = new BlueprintTooltips(this);
            ItemTooltips = new ItemTooltips(this);
            ToolbarOverride = new ToolbarOverride(this);
            ToolbarMonitor = new ToolbarMonitor(this);
            ToolbarCustomLabels = new ToolbarCustomLabels(this);
            ToolbarStatusProcessor = new ToolbarStatusProcessor(this);
            ToolbarLabelRender = new ToolbarLabelRender(this);
            GridMassCompute = new GridMassCompute(this);
            PBMonitor = new PBMonitor(this);
            InterModAPI = new InterModAPI(this);
            DebugEvents = new DebugEvents(this);
            DebugLog = new DebugLog(this);
        }
    }
}