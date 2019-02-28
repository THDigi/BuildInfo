using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Systems
{
    public class BlockMonitor : ClientComponent
    {
        public bool CanAddTypes { get; private set; } = true;

        public delegate void CallbackDelegate(IMySlimBlock block);

        private readonly Dictionary<MyObjectBuilderType, CallbackDelegate> monitorBlockTypes = new Dictionary<MyObjectBuilderType, CallbackDelegate>(MyObjectBuilderType.Comparer);
        private readonly List<IMySlimBlock> tmpBlocks = new List<IMySlimBlock>();

        public BlockMonitor(Client mod) : base(mod)
        {
        }

        public override void RegisterComponent()
        {
            CanAddTypes = false;

            MyAPIGateway.Entities.OnEntityAdd += OnEntitySpawned;

            var ents = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(ents);

            foreach(var ent in ents)
            {
                OnEntitySpawned(ent);
            }
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntitySpawned;
        }

        public void MonitorType(MyObjectBuilderType blockType, CallbackDelegate callback)
        {
            if(!CanAddTypes)
                throw new Exception("BlockMonitor can not accept monitor requests at RegisterComponent() or later.");

            monitorBlockTypes.Add(blockType, callback);
        }

        private void OnEntitySpawned(IMyEntity ent)
        {
            try
            {
                var grid = ent as IMyCubeGrid;

                if(grid == null)
                    return;

                grid.OnBlockAdded += BlockAdded;
                grid.OnClose += GridClosed;

                grid.GetBlocks(tmpBlocks);

                foreach(var slim in tmpBlocks)
                {
                    BlockAdded(slim);
                }

                tmpBlocks.Clear();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void GridClosed(IMyEntity ent)
        {
            var grid = (IMyCubeGrid)ent;
            grid.OnBlockAdded -= BlockAdded;
            grid.OnClose -= GridClosed;
        }

        private void BlockAdded(IMySlimBlock slim)
        {
            try
            {
                if(slim.FatBlock == null)
                    return;

                var typeId = slim.BlockDefinition.Id.TypeId;
                var method = monitorBlockTypes.GetValueOrDefault(typeId, null);

                method?.Invoke(slim);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
