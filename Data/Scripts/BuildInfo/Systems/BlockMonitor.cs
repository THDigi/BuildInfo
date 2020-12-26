using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Systems
{
    /// <summary>
    /// Executes callbacks for registered block types.
    /// </summary>
    public class BlockMonitor : ModComponent
    {
        public bool CanAddTypes { get; private set; } = true;

        public delegate void CallbackDelegate(IMySlimBlock block);

        public event CallbackDelegate BlockAdded;

        private readonly Dictionary<MyObjectBuilderType, List<CallbackDelegate>> monitorBlockTypes = new Dictionary<MyObjectBuilderType, List<CallbackDelegate>>(MyObjectBuilderType.Comparer);

        private readonly List<IMySlimBlock> tmpBlocks = new List<IMySlimBlock>();

        public BlockMonitor(BuildInfoMod main) : base(main)
        {
        }

        protected override void RegisterComponent()
        {
            CanAddTypes = false;

            // Not using MyVisualScriptLogicProvider.BlockBuilt because it doesn't get called for existing grids,
            //  requiring entities monitored and grids to be iterated for blocks on spawn anyway.
            //  (to detect blocks for spawned/streamed/pasted/etc grids)

            MyAPIGateway.Entities.OnEntityAdd += EntitySpawned;

            var ents = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(ents);

            foreach(var ent in ents)
            {
                EntitySpawned(ent);
            }
        }

        protected override void UnregisterComponent()
        {
            MyAPIGateway.Entities.OnEntityAdd -= EntitySpawned;
        }

        /// <summary>
        /// Adds callback for specified block type.
        /// Multiple callbacks per type are supported.
        /// </summary>
        public void MonitorType(MyObjectBuilderType blockType, CallbackDelegate callback)
        {
            if(!CanAddTypes)
                throw new Exception($"{GetType().Name} can not accept monitor requests at RegisterComponent() or later.");

            if(callback == null)
                throw new ArgumentException($"{GetType().Name}.MonitorType() does not accept a null callback.");

            List<CallbackDelegate> callbacks;

            if(!monitorBlockTypes.TryGetValue(blockType, out callbacks))
            {
                callbacks = new List<CallbackDelegate>(1);
                monitorBlockTypes.Add(blockType, callbacks);
            }

            callbacks.Add(callback);
        }

        void EntitySpawned(IMyEntity ent)
        {
            try
            {
                var grid = ent as IMyCubeGrid;
                if(grid == null)
                    return;

                grid.OnBlockAdded += BlockAdd;
                grid.OnClose += GridClosed;

                tmpBlocks.Clear();
                grid.GetBlocks(tmpBlocks);

                foreach(var slim in tmpBlocks)
                {
                    BlockAdd(slim);
                }

                tmpBlocks.Clear();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void GridClosed(IMyEntity ent)
        {
            var grid = (IMyCubeGrid)ent;
            grid.OnBlockAdded -= BlockAdd;
            grid.OnClose -= GridClosed;
        }

        void BlockAdd(IMySlimBlock slim)
        {
            try
            {
                try
                {
                    BlockAdded?.Invoke(slim);
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }

                var typeId = slim.BlockDefinition.Id.TypeId;

                List<CallbackDelegate> callbacks;
                if(!monitorBlockTypes.TryGetValue(typeId, out callbacks))
                    return;

                for(int i = 0; i < callbacks.Count; ++i)
                {
                    try
                    {
                        callbacks[i].Invoke(slim);
                    }
                    catch(Exception e)
                    {
                        Log.Error($"{GetType().Name}.BlockAdded() :: callback for {typeId.ToString()} @ index={i.ToString()} error:");
                        Log.Error(e);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
