using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features;
using Digi.BuildInfo.Utilities;
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

        public interface ICallback
        {
            void BlockSpawned(IMySlimBlock slim);
        }

        /// <summary>
        /// WARNING: gets called for grid merge/unmerge which means the same entity can be added multiple times.
        /// </summary>
        public event Action<IMySlimBlock> BlockAdded;

        readonly HashSet<IMyCubeGrid> HookedGrids = new HashSet<IMyCubeGrid>();

        readonly Dictionary<MyObjectBuilderType, List<ICallback>> MonitorBlockTypes = new Dictionary<MyObjectBuilderType, List<ICallback>>(MyObjectBuilderType.Comparer);

        public BlockMonitor(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            CanAddTypes = false;

            // Not using MyVisualScriptLogicProvider.BlockBuilt because it doesn't get called for existing grids,
            //  requiring entities monitored and grids to be iterated for blocks on spawn anyway.
            //  (to detect blocks for spawned/streamed/pasted/etc grids)

            foreach(MyEntity ent in MyEntities.GetEntities())
            {
                EntitySpawned(ent);
            }

            MyAPIGateway.Entities.OnEntityAdd += EntitySpawned;
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.Entities.OnEntityAdd -= EntitySpawned;
        }

        /// <summary>
        /// Adds callback for specified block type.
        /// Multiple callbacks per type are supported.
        /// </summary>
        public void MonitorType(MyObjectBuilderType blockType, ICallback callback)
        {
            if(!CanAddTypes)
                throw new Exception($"{GetType().Name} can not accept monitor requests at RegisterComponent() or later.");

            if(callback == null)
                throw new ArgumentException($"{GetType().Name}.MonitorType() does not accept a null callback.");

            List<ICallback> callbacks;
            if(!MonitorBlockTypes.TryGetValue(blockType, out callbacks))
            {
                callbacks = new List<ICallback>(1);
                MonitorBlockTypes.Add(blockType, callbacks);
            }

            callbacks.Add(callback);
        }

        /// <summary>
        /// Unregisters specified callback
        /// </summary>
        public void RemoveMonitor(MyObjectBuilderType blockType, ICallback callback)
        {
            List<ICallback> callbacks;
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

                if(BuildInfoMod.IsDevMod && !Utils.AssertMainThread(false))
                {
                    Log.Info($"[WARNING] {grid.DisplayName} ({grid.EntityId}) got added from a thread!", Log.PRINT_MESSAGE, 5000);
                }

                if(HookedGrids.Add(grid))
                {
                    grid.OnBlockAdded += BlockAdd;
                    grid.OnMarkForClose += GridMarkedForClose;

                    MyCubeGrid internalGrid = (MyCubeGrid)grid;
                    foreach(IMySlimBlock slim in internalGrid.CubeBlocks)
                    {
                        BlockAdd(slim);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void GridMarkedForClose(IMyEntity ent)
        {
            IMyCubeGrid grid = (IMyCubeGrid)ent;
            HookedGrids.Remove(grid);
            grid.OnBlockAdded -= BlockAdd;
            grid.OnMarkForClose -= GridMarkedForClose;
        }

        void BlockAdd(IMySlimBlock slim)
        {
            try
            {
                BlockAdded?.Invoke(slim);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            try
            {
                MyObjectBuilderType typeId = slim.BlockDefinition.Id.TypeId;

                List<ICallback> callbacks;
                if(!MonitorBlockTypes.TryGetValue(typeId, out callbacks))
                    return;

                for(int i = 0; i < callbacks.Count; ++i)
                {
                    try
                    {
                        callbacks[i].BlockSpawned(slim);
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
