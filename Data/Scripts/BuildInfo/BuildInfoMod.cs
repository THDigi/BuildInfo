using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utils;
using Sandbox.ModAPI;
using VRage.Game.Components;

namespace Digi.BuildInfo
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class BuildInfoMod : MySessionComponentBase
    {
        public const string MOD_NAME = "Build Info";

        public static BuildInfoMod Instance;

        public static Caches Caches => Instance.caches;
        public static Client Client => Instance.client;
        public static Config Config => Client.Config;
        public static EquipmentMonitor EquipmentMonitor => Client.EquipmentMonitor;

        public bool Started { get; private set; }
        public bool IsDS { get; private set; }

        private Client client;
        private Caches caches;

        public override void LoadData()
        {
            Instance = this;
            IsDS = (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated);
            Log.ModName = MOD_NAME;

            if(IsDS)
                return;

            caches = new Caches();

            client = new Client();
            client.WorldLoading();

            SetUpdateOrder(MyUpdateOrder.AfterSimulation);
        }

        public override void BeforeStart()
        {
            Started = true;

            if(IsDS)
                return;

            client.WorldStart();
        }

        protected override void UnloadData()
        {
            Instance = null;
            Started = false;

            if(IsDS)
                return;

            client.WorldExit();
        }

        public override void HandleInput()
        {
            if(IsDS)
                return;

            client.UpdateInput();
        }

        public override void UpdateAfterSimulation()
        {
            if(IsDS)
                return;

            client.UpdateAfterSim();
        }

        public override void Draw()
        {
            if(IsDS)
                return;

            client.UpdateDraw();
        }
    }
}
