using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
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

        /// <summary>
        /// WARNING: gets called for grid merge/unmerge which means the same entity can be added multiple times.
        /// </summary>
        public event CallbackDelegate BlockAdded;

        private readonly Dictionary<MyObjectBuilderType, List<CallbackDelegate>> MonitorBlockTypes = new Dictionary<MyObjectBuilderType, List<CallbackDelegate>>(MyObjectBuilderType.Comparer);

        public BlockMonitor(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            CanAddTypes = false;

            // Not using MyVisualScriptLogicProvider.BlockBuilt because it doesn't get called for existing grids,
            //  requiring entities monitored and grids to be iterated for blocks on spawn anyway.
            //  (to detect blocks for spawned/streamed/pasted/etc grids)

            MyAPIGateway.Entities.OnEntityAdd += EntitySpawned;

            foreach(MyEntity ent in MyEntities.GetEntities())
            {
                EntitySpawned(ent);
            }
        }

        public override void UnregisterComponent()
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
            if(!MonitorBlockTypes.TryGetValue(blockType, out callbacks))
            {
                callbacks = new List<CallbackDelegate>(1);
                MonitorBlockTypes.Add(blockType, callbacks);
            }

            callbacks.Add(callback);
        }

        /// <summary>
        /// Unregisters specified callback
        /// </summary>
        public void RemoveMonitor(MyObjectBuilderType blockType, CallbackDelegate callback)
        {
            List<CallbackDelegate> callbacks;
            if(!MonitorBlockTypes.TryGetValue(blockType, out callbacks))
                return;

            callbacks.Remove(callback);

            if(callbacks.Count == 0)
                MonitorBlockTypes.Remove(blockType);
        }

        void EntitySpawned(IMyEntity ent)
        {
            try
            {
                IMyCubeGrid grid = ent as IMyCubeGrid;
                if(grid == null)
                    return;

                grid.OnBlockAdded += BlockAdd;
                grid.OnClose += GridClosed;

                MyCubeGrid internalGrid = (MyCubeGrid)grid;
                foreach(IMySlimBlock slim in internalGrid.CubeBlocks)
                {
                    BlockAdd(slim);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void GridClosed(IMyEntity ent)
        {
            IMyCubeGrid grid = (IMyCubeGrid)ent;
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

                MyObjectBuilderType typeId = slim.BlockDefinition.Id.TypeId;

                List<CallbackDelegate> callbacks;
                if(!MonitorBlockTypes.TryGetValue(typeId, out callbacks))
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
