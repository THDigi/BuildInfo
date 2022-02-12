using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Systems
{
    public static class SplitCheckingExtensions
    {
        public static bool IsSet(this SplitFlags flags, SplitFlags flag) => (flags & flag) != 0;
    }

    [Flags]
    public enum SplitFlags
    {
        None = 0,
        Split = (1 << 1),
        Disconnect = (1 << 2),
        BlockLoss = (1 << 3),
    }

    public class SplitChecking : ModComponent
    {
        long LastGridId;
        Vector3I LastBlockVec;
        SplitFlags LastFlags;

        readonly List<IMySlimBlock> TempNeighbours = new List<IMySlimBlock>();

        public SplitChecking(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        public SplitFlags GetSplitInfo(IMySlimBlock blockToRemove)
        {
            if(blockToRemove.CubeGrid.EntityId == LastGridId && blockToRemove.Position == LastBlockVec)
            {
                return LastFlags;
            }

            LastGridId = blockToRemove.CubeGrid.EntityId;
            LastBlockVec = blockToRemove.Position;
            LastFlags = SplitFlags.None;

            bool willSplit = blockToRemove.CubeGrid.WillRemoveBlockSplitGrid(blockToRemove);
            if(willSplit)
            {
                LastFlags |= SplitFlags.Split;

                // determine if split will result in blocks vanishing, from !IsStandAlone
                try
                {
                    TempNeighbours.Clear();
                    blockToRemove.GetNeighbours(TempNeighbours);

                    foreach(IMySlimBlock block in TempNeighbours)
                    {
                        MyCubeBlockDefinition def = (MyCubeBlockDefinition)block.BlockDefinition;
                        if(!def.IsStandAlone)
                        {
                            // TODO: need to scan deeper to work for multi-mount blocks
                            // this should do for single-mount (vanilla) ones for now.
                            LastFlags |= SplitFlags.BlockLoss;
                            break;
                        }
                    }
                }
                finally
                {
                    TempNeighbours.Clear();
                }
            }

            // check various blocks that hold onto other grids
            if(blockToRemove.FatBlock != null)
            {
                IMyMechanicalConnectionBlock mechAttach = blockToRemove.FatBlock as IMyMechanicalConnectionBlock;
                if(mechAttach != null && mechAttach.Top != null)
                {
                    LastFlags |= SplitFlags.Disconnect;
                }

                IMyAttachableTopBlock mechTop = blockToRemove.FatBlock as IMyAttachableTopBlock;
                if(mechTop != null && mechTop.Base != null)
                {
                    LastFlags |= SplitFlags.Disconnect;
                }

                IMyShipConnector connector = blockToRemove.FatBlock as IMyShipConnector;
                if(connector != null && connector.OtherConnector != null)
                {
                    LastFlags |= SplitFlags.Disconnect;
                }

                IMyLandingGear lg = blockToRemove.FatBlock as IMyLandingGear;
                if(lg != null && lg.LockMode == SpaceEngineers.Game.ModAPI.Ingame.LandingGearMode.Locked)
                {
                    // not sure if it's necessary to check... player can just check visually once they're alerted that LG is holding onto something.
                    //IMyEntity lockedToEnt = lg.GetAttachedEntity(); 
                    //if(lockedToEnt is IMyCubeGrid)

                    LastFlags |= SplitFlags.Disconnect;
                }
            }

            return LastFlags;
        }

        // TODO: finish the optimized but complicated way...
#if false
        
        readonly Dictionary<IMyCubeGrid, SplitData> PerGridData = new Dictionary<IMyCubeGrid, SplitData>();

        readonly HashSet<IMyCubeGrid> KeysToRemove = new HashSet<IMyCubeGrid>();

        class SplitData
        {
            public readonly IMyCubeGrid Grid;
            public readonly Dictionary<IMySlimBlock, SplitInfo> Results = new Dictionary<IMySlimBlock, SplitInfo>();

            public SplitData(IMyCubeGrid grid)
            {
            }
        }
        
        public SplitInfo GetSplitInfo(IMySlimBlock blockToRemove)
        {
            Utils.AssertMainThread();

            IMyCubeGrid grid = blockToRemove.CubeGrid;
            SplitData data;
            if(!PerGridData.TryGetValue(grid, out data))
            {
                data = new SplitData(grid);
                PerGridData[grid] = data;

                grid.OnBlockAdded += BlockAdded;
                grid.OnBlockRemoved += BlockRemoved;
                grid.OnMarkForClose += GridMarkedForClose;
            }

            SplitInfo splitInfo;
            if(!data.Results.TryGetValue(blockToRemove, out splitInfo))
            {
                bool willSplit = blockToRemove.CubeGrid.WillRemoveBlockSplitGrid(blockToRemove);
                splitInfo = (willSplit ? SplitInfo.Split : SplitInfo.NoSplit);

                if(willSplit)
                {
                    try
                    {
                        _BlockToRemove = blockToRemove;
                        List<IMySlimBlock> neighbours = blockToRemove.Neighbours; // DEBUG TODO fix alloc

                        foreach(IMySlimBlock block in neighbours)
                        {
                            MyCubeBlockDefinition def = (MyCubeBlockDefinition)block.BlockDefinition;
                            if(!def.IsStandAlone)
                                RecursiveNeighbourCheck(block);
                        }
                    }
                    finally
                    {
                        _BlockToRemove = null;
                    }
                }
            }
        }

        IMySlimBlock _BlockToRemove;
        bool RecursiveNeighbourCheck(IMySlimBlock startFromBlock)
        {
            List<IMySlimBlock> neighbours = startFromBlock.Neighbours; // DEBUG TODO fix alloc

            foreach(IMySlimBlock block in neighbours)
            {
                if(block == _BlockToRemove)
                    continue; // ignore the supposedly removed block

                MyCubeBlockDefinition def = (MyCubeBlockDefinition)block.BlockDefinition;
                if(def.IsStandAlone)
                    return true;
                else
                    RecursiveNeighbourCheck(block);
            }

            return false;
        }

        void BlockAdded(IMySlimBlock block)
        {
            KeysToRemove.Add(block.CubeGrid);
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        void BlockRemoved(IMySlimBlock block)
        {
            KeysToRemove.Add(block.CubeGrid);
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        void GridMarkedForClose(IMyEntity ent)
        {
            KeysToRemove.Add((IMyCubeGrid)ent);
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void UpdateAfterSim(int tick)
        {
            foreach(IMyCubeGrid grid in KeysToRemove)
            {
                grid.OnBlockAdded -= BlockAdded;
                grid.OnBlockRemoved -= BlockRemoved;
                grid.OnMarkForClose -= GridMarkedForClose;

                PerGridData.Remove(grid);
            }

            KeysToRemove.Clear();

            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
        }
#endif
    }
}
