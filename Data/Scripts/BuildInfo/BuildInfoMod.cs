using Digi.BuildInfo.Features;
using Digi.BuildInfo.Features.ChatCommands;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Features.LeakInfo;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.Overlays;
using Digi.BuildInfo.Features.ReloadTracker;
using Digi.BuildInfo.Features.TextAPIMenu;
using Digi.BuildInfo.Features.ToolbarLabels;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using VRage.Game.Components;

namespace Digi.BuildInfo
{
    public class BuildInfoMod : ModBase<BuildInfoMod>
    {
        public const string MOD_NAME = "Build Info";

        // Utils
        public Caches Caches;
        public Constants Constants;
        public DrawUtils DrawUtils;

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
        public ModMenu ModMenu;
        public LeakInfo LeakInfo;
        public Overlays Overlays;
        public LockOverlay LockOverlay;
        public PickBlock PickBlock;
        public QuickMenu QuickMenu;
        public AnalyseShip AnalyseShip;
        public ChatCommandHandler ChatCommandHandler;
        public ReloadTracking ReloadTracking;
        public TerminalInfo TerminalInfo;
        public TextGeneration TextGeneration;
        public LiveDataHandler LiveDataHandler;
        public TurretHUD TurretHUD;
        public PlacementDistance PlacementDistance;
        public BlockInfoAdditions BlockInfoAdditions;
        public RelativeDampenerInfo RelativeDampenerInfo;
        public ShipToolInventoryBar ShipToolInventoryBar;
        public ToolbarActionLabels ToolbarActionLabels;
        public WhatsNew WhatsNew;
        public DebugEvents DebugEvents;

        public BuildInfoMod(BuildInfo_GameSession session) : base(MOD_NAME, session)
        {
            session.SetUpdateOrder(MyUpdateOrder.AfterSimulation);

            // Utils
            Caches = new Caches(this);
            Constants = new Constants(this);
            DrawUtils = new DrawUtils(this);

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
            ModMenu = new ModMenu(this);
            LeakInfo = new LeakInfo(this);
            Overlays = new Overlays(this);
            LockOverlay = new LockOverlay(this);
            PickBlock = new PickBlock(this);
            QuickMenu = new QuickMenu(this);
            AnalyseShip = new AnalyseShip(this);
            ChatCommandHandler = new ChatCommandHandler(this);
            ReloadTracking = new ReloadTracking(this);
            TerminalInfo = new TerminalInfo(this);
            TextGeneration = new TextGeneration(this);
            LiveDataHandler = new LiveDataHandler(this);
            TurretHUD = new TurretHUD(this);
            PlacementDistance = new PlacementDistance(this);
            BlockInfoAdditions = new BlockInfoAdditions(this);
            RelativeDampenerInfo = new RelativeDampenerInfo(this);
            ShipToolInventoryBar = new ShipToolInventoryBar(this);
            ToolbarActionLabels = new ToolbarActionLabels(this);
            WhatsNew = new WhatsNew(this);
            DebugEvents = new DebugEvents(this);
        }
    }
}