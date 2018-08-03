using System;
using System.Collections.Generic;
using Digi.BuildInfo.BlockData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo
{
    public class BlockMonitorComponent
    {
        private readonly BuildInfo mod;

        private delegate bool CallSignature(IMyCubeBlock block);
        private readonly Dictionary<MyObjectBuilderType, CallSignature> monitorBlockTypes
                   = new Dictionary<MyObjectBuilderType, CallSignature>(MyObjectBuilderType.Comparer);

        public BlockMonitorComponent(BuildInfo mod)
        {
            this.mod = mod;

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

            MyAPIGateway.Entities.OnEntityAdd += OnEntitySpawned;
        }

        public void Close()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntitySpawned;
        }

        private void AddType<T>(MyObjectBuilderType blockType) where T : BData_Base, new()
        {
            monitorBlockTypes.Add(blockType, GotBlock<T>);
        }

        private void OnEntitySpawned(IMyEntity ent)
        {
            try
            {
                var grid = ent as IMyCubeGrid;

                if(grid == null)
                    return;

                grid.GetBlocks(null, CheckExistingBlock);
                grid.OnBlockAdded += OnBlockAdded;
                grid.OnClose += OnGridClosed;
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void OnGridClosed(IMyEntity ent)
        {
            var grid = (IMyCubeGrid)ent;
            grid.OnBlockAdded -= OnBlockAdded;
            grid.OnClose -= OnGridClosed;
        }

        private bool CheckExistingBlock(IMySlimBlock slim)
        {
            OnBlockAdded(slim);
            return false; // needs to be false because the list given to GetBlocks() is null.
        }

        private void OnBlockAdded(IMySlimBlock slim)
        {
            try
            {
                if(slim.FatBlock == null)
                    return;

                var typeId = slim.BlockDefinition.Id.TypeId;
                var method = monitorBlockTypes.GetValueOrDefault(typeId, null);

                if(method == null)
                    return;

                method.Invoke(slim.FatBlock);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private bool GotBlock<T>(IMyCubeBlock block) where T : BData_Base, new()
        {
            return BData_Base.TrySetData<T>(block);
        }
    }
}
