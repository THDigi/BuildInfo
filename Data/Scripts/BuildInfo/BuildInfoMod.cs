using Digi.BuildInfo.Features;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Features.LeakInfo;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.TextAPIMenu;
using Digi.BuildInfo.Features.TurretInfo;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using VRage.Game.Components;

namespace Digi.BuildInfo
{
    public class BuildInfoMod : ModBase<BuildInfoMod>
    {
        public const string MOD_NAME = "Build Info";

        public Caches Caches;
        public Constants Constants;

        // Systems
        public DrawUtils DrawUtils;
        public TextAPI TextAPI;
        public GameConfig GameConfig;
        public BlockMonitor BlockMonitor;
        public InputLibHandler InputLibHandler;
        public EquipmentMonitor EquipmentMonitor;

        // Features
        public Config Config;
        public LegacyConfig LegacyConfig;
        public ModMenu ModMenu;
        public LeakInfo LeakInfo;
        public Overlays Overlays;
        public PickBlock PickBlock;
        public QuickMenu QuickMenu;
        public AnalyseShip AnalyseShip;
        public ChatCommands ChatCommands;
        public TerminalInfo TerminalInfo;
        public TextGeneration TextGeneration;
        public LiveDataHandler LiveDataHandler;
        public TurretTracking TurretTracking;
        public TurretHUD TurretHUD;
        public PlacementDistance PlacementDistance;
        public BlockInfoAdditions BlockInfoAdditions;
        public RelativeDampenerInfo RelativeDampenerInfo;
        public ShipToolInventoryBar ShipToolInventoryBar;
        public DebugEvents DebugEvents;

        public BuildInfoMod(BuildInfo_GameSession session) : base(MOD_NAME, session)
        {
            session.SetUpdateOrder(MyUpdateOrder.AfterSimulation);

            Caches = new Caches(this);
            Constants = new Constants(this);

            // Systems
            DrawUtils = new DrawUtils(this);
            TextAPI = new TextAPI(this);
            InputLibHandler = new InputLibHandler(this);
            GameConfig = new GameConfig(this);
            BlockMonitor = new BlockMonitor(this);
            EquipmentMonitor = new EquipmentMonitor(this);

            // Features
            Config = new Config(this);
            LegacyConfig = new LegacyConfig(this);
            ModMenu = new ModMenu(this);
            LeakInfo = new LeakInfo(this);
            Overlays = new Overlays(this);
            PickBlock = new PickBlock(this);
            QuickMenu = new QuickMenu(this);
            AnalyseShip = new AnalyseShip(this);
            ChatCommands = new ChatCommands(this);
            TerminalInfo = new TerminalInfo(this);
            TextGeneration = new TextGeneration(this);
            LiveDataHandler = new LiveDataHandler(this);
            TurretTracking = new TurretTracking(this);
            TurretHUD = new TurretHUD(this);
            PlacementDistance = new PlacementDistance(this);
            BlockInfoAdditions = new BlockInfoAdditions(this);
            RelativeDampenerInfo = new RelativeDampenerInfo(this);
            ShipToolInventoryBar = new ShipToolInventoryBar(this);
            DebugEvents = new DebugEvents(this);
        }
    }
}