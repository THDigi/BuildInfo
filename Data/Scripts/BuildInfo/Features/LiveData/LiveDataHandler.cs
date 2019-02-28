using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Features.LiveData
{
    /// <summary>
    /// Handles grabbing and catching live block data which requires spawning a temporary grid with block to get that data from the block, because it's unavailable from definitions.
    /// </summary>
    public class LiveDataHandler : ClientComponent
    {
        public BData_Base BlockDataCache;
        public bool BlockDataCacheValid = true;
        public readonly Dictionary<MyDefinitionId, BData_Base> BlockData = new Dictionary<MyDefinitionId, BData_Base>(MyDefinitionId.Comparer);
        public readonly HashSet<MyDefinitionId> BlockSpawnInProgress = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        public LiveDataHandler(Client mod) : base(mod)
        {
            AddType<BData_Collector>(typeof(MyObjectBuilder_Collector));

            AddType<BData_LandingGear>(typeof(MyObjectBuilder_LandingGear));

            AddType<BData_Connector>(typeof(MyObjectBuilder_ShipConnector));

            AddType<BData_ShipTool>(typeof(MyObjectBuilder_ShipWelder));
            AddType<BData_ShipTool>(typeof(MyObjectBuilder_ShipGrinder));

            AddType<BData_Thrust>(typeof(MyObjectBuilder_Thrust));

            AddType<BData_Weapon>(typeof(MyObjectBuilder_SmallGatlingGun));
            AddType<BData_Weapon>(typeof(MyObjectBuilder_SmallMissileLauncher));
            AddType<BData_Weapon>(typeof(MyObjectBuilder_SmallMissileLauncherReload));
            AddType<BData_Weapon>(typeof(MyObjectBuilder_LargeGatlingTurret));
            AddType<BData_Weapon>(typeof(MyObjectBuilder_LargeMissileTurret));
            AddType<BData_Weapon>(typeof(MyObjectBuilder_InteriorTurret));
        }

        public override void RegisterComponent()
        {
            EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
        }

        public override void UnregisterComponent()
        {
            EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
        }

        private void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BlockDataCache = null;
            BlockDataCacheValid = true;
        }

        private void AddType<T>(MyObjectBuilderType blockType) where T : BData_Base, new()
        {
            BlockMonitor.MonitorType(blockType, BlockAdded<T>);
        }

        private void BlockAdded<T>(IMySlimBlock block) where T : BData_Base, new()
        {
            var success = BData_Base.TrySetData<T>(block.FatBlock);

            if(success && TextGeneration != null)
            {
                // reset caches and force block text recalc
                TextGeneration.CachedBuildInfoTextAPI.Remove(block.BlockDefinition.Id);
                TextGeneration.CachedBuildInfoNotification.Remove(block.BlockDefinition.Id);
                TextGeneration.LastDefId = default(MyDefinitionId);
            }
        }
    }
}
