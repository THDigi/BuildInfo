using Digi.BuildInfo.Features;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Features.LeakInfo;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.TextAPIMenu;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;

namespace Digi.BuildInfo
{
    public class Client : ModBase
    {
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
        public ChatCommands ChatCommands;
        public TextGeneration TextGeneration;
        public LiveDataHandler LiveDataHandler;
        public TurretAmmoPrint TurretAmmoPrint;
        public BuilderAdditions BuilderAdditions;
        public PlacementDistance PlacementDistance;
        public BlockInfoAdditions BlockInfoAdditions;
        public ShipToolInventoryBar ShipToolInventoryBar;
        public DebugEvents DebugEvents;

        public override void WorldLoading()
        {
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
            ChatCommands = new ChatCommands(this);
            TextGeneration = new TextGeneration(this);
            LiveDataHandler = new LiveDataHandler(this);
            TurretAmmoPrint = new TurretAmmoPrint(this);
            BuilderAdditions = new BuilderAdditions(this);
            PlacementDistance = new PlacementDistance(this);
            BlockInfoAdditions = new BlockInfoAdditions(this);
            ShipToolInventoryBar = new ShipToolInventoryBar(this);
            DebugEvents = new DebugEvents(this);
        }

        public override void WorldStart()
        {
            base.WorldStart(); // registers components
        }

        public override void WorldExit()
        {
            base.WorldExit(); // unregisters components
        }
    }
}
