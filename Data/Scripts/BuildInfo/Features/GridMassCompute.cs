using System;
using System.Collections.Generic;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Digi.BuildInfo.Features
{
    public class GridMassCompute : ModComponent
    {
        public const int MassDataExpireTicks = (Constants.TICKS_PER_SECOND * 60 * 5);
        public const int MassDataCheckTicks = (Constants.TICKS_PER_SECOND * 60);

        readonly MyConcurrentPool<MassData> DataPool = new MyConcurrentPool<MassData>();
        readonly Dictionary<IMyCubeGrid, MassData> Grids = new Dictionary<IMyCubeGrid, MassData>();
        readonly HashSet<IMyCubeGrid> KeysToRemove = new HashSet<IMyCubeGrid>();

        public GridMassCompute(BuildInfoMod main) : base(main)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        public override void UpdateAfterSim(int tick)
        {
            if(tick % MassDataCheckTicks == 0 && Grids.Count > 0)
            {
                foreach(var kv in Grids)
                {
                    int expiresAt = kv.Value.LastReadAtTick + MassDataExpireTicks;
                    if(expiresAt <= tick)
                    {
                        KeysToRemove.Add(kv.Key);
                    }
                }

                if(KeysToRemove.Count > 0)
                {
                    foreach(var key in KeysToRemove)
                    {
                        RemoveEntry(key);
                    }

                    KeysToRemove.Clear();
                }
            }
        }

        public float GetGridMass(IMyCubeGrid grid)
        {
            var internalGrid = (MyCubeGrid)grid;
            float mass = internalGrid.Mass;
            if(mass > 0)
                return mass;

            MassData data;
            if(!Grids.TryGetValue(grid, out data))
            {
                data = DataPool.Get();
                data.Init(grid);
                grid.OnMarkForClose += GridMarkedForClose;

                Grids[grid] = data;
            }

            return data.GetMass();
        }

        void RemoveEntry(IMyCubeGrid grid)
        {
            try
            {
                if(grid == null)
                    return;

                grid.OnMarkForClose -= GridMarkedForClose;

                var data = Grids.GetValueOrDefault(grid, null);
                if(data != null)
                {
                    Grids.Remove(grid);

                    data.Reset();
                    DataPool.Return(data);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void GridMarkedForClose(IMyEntity ent)
        {
            RemoveEntry(ent as IMyCubeGrid);
        }
    }

    internal class MassData
    {
        public int LastReadAtTick { get; private set; }

        IMyCubeGrid Grid;
        float BlockBaseMass;
        float InventoryTotalMass;
        HashSet<IMyCubeBlock> BlocksWithInventory = new HashSet<IMyCubeBlock>();

        public void Init(IMyCubeGrid grid)
        {
            if(Grid == grid)
                return;

            if(Grid != null)
                Reset();

            BlockBaseMass = 0;
            InventoryTotalMass = -1;
            Grid = grid;
            Grid.OnBlockAdded += BlockAdded;
            Grid.OnBlockRemoved += BlockRemoved;

            var internalGrid = (MyCubeGrid)Grid;
            float mass = 0f;

            foreach(IMySlimBlock slimBlock in internalGrid.GetBlocks())
            {
                // HACK: game doesn't use mass from blocks with HasPhysics=false
                var def = (MyCubeBlockDefinition)slimBlock.BlockDefinition;

                var fatBlock = slimBlock.FatBlock;
                if(fatBlock != null)
                {
                    if(fatBlock.InventoryCount > 0)
                    {
                        for(int i = (fatBlock.InventoryCount - 1); i >= 0; --i)
                        {
                            var inv = (MyInventory)fatBlock.GetInventory(i);
                            if(inv != null)
                                inv.InventoryContentChanged += InventoryContentChanged;
                        }

                        BlocksWithInventory.Add(fatBlock);
                    }

                    if(def.HasPhysics)
                        mass += fatBlock.Mass;
                }
                else
                {
                    if(def.HasPhysics)
                        mass += slimBlock.Mass;
                }
            }

            BlockBaseMass = mass;
        }

        public void Reset()
        {
            if(Grid != null)
            {
                Grid.OnBlockAdded -= BlockAdded;
                Grid.OnBlockRemoved -= BlockRemoved;
            }

            if(!Grid.MarkedForClose)
            {
                foreach(var fatBlock in BlocksWithInventory)
                {
                    for(int i = (fatBlock.InventoryCount - 1); i >= 0; --i)
                    {
                        var inv = (MyInventory)fatBlock.GetInventory(i);
                        if(inv != null)
                            inv.InventoryContentChanged -= InventoryContentChanged;
                    }
                }
            }

            BlocksWithInventory.Clear();
            Grid = null;
            BlockBaseMass = 0;
            InventoryTotalMass = -1;
        }

        public float GetMass()
        {
            if(Grid == null)
                throw new Exception("MassData not initialized!");

            if(InventoryTotalMass < 0)
            {
                InventoryTotalMass = 0;
                float cargoMassMultiplier = 1f / MyAPIGateway.Session.SessionSettings.BlocksInventorySizeMultiplier;

                foreach(var block in BlocksWithInventory)
                {
                    for(int i = (block.InventoryCount - 1); i >= 0; --i)
                    {
                        var inv = block.GetInventory(i);
                        if(inv != null)
                            InventoryTotalMass += (float)inv.CurrentMass * cargoMassMultiplier;
                    }
                }
            }

            float mass = BlockBaseMass + InventoryTotalMass;

            var internalGrid = (MyCubeGrid)Grid;
            if(internalGrid.OccupiedBlocks.Count > 0)
            {
                foreach(var seat in internalGrid.OccupiedBlocks)
                {
                    var pilot = seat?.Pilot as IMyCharacter;
                    if(pilot != null)
                    {
                        // character inventory mass seems to not be added to grid physical mass
                        mass += pilot.BaseMass;
                    }
                }
            }

            LastReadAtTick = BuildInfoMod.Instance.Tick;
            return mass;
        }

        void BlockAdded(IMySlimBlock slimBlock)
        {
            try
            {
                var def = (MyCubeBlockDefinition)slimBlock.BlockDefinition;

                var fatBlock = slimBlock.FatBlock;
                if(fatBlock != null)
                {
                    if(fatBlock.InventoryCount > 0)
                    {
                        float cargoMassMultiplier = 1f / MyAPIGateway.Session.SessionSettings.BlocksInventorySizeMultiplier;

                        for(int i = (fatBlock.InventoryCount - 1); i >= 0; --i)
                        {
                            var inv = (MyInventory)fatBlock.GetInventory(i);
                            if(inv != null)
                            {
                                inv.InventoryContentChanged += InventoryContentChanged;

                                if(InventoryTotalMass > 0)
                                    InventoryTotalMass += (float)inv.CurrentMass * cargoMassMultiplier;
                            }
                        }

                        BlocksWithInventory.Add(fatBlock);
                    }

                    if(def.HasPhysics)
                        BlockBaseMass += fatBlock.Mass;
                }
                else
                {
                    if(def.HasPhysics)
                        BlockBaseMass += slimBlock.Mass;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void BlockRemoved(IMySlimBlock slimBlock)
        {
            try
            {
                var def = (MyCubeBlockDefinition)slimBlock.BlockDefinition;

                var fatBlock = slimBlock.FatBlock;
                if(fatBlock != null)
                {
                    if(fatBlock.InventoryCount > 0)
                    {
                        float cargoMassMultiplier = 1f / MyAPIGateway.Session.SessionSettings.BlocksInventorySizeMultiplier;

                        for(int i = (fatBlock.InventoryCount - 1); i >= 0; --i)
                        {
                            var inv = (MyInventory)fatBlock.GetInventory(i);
                            if(inv != null)
                            {
                                inv.InventoryContentChanged -= InventoryContentChanged;

                                if(InventoryTotalMass > 0)
                                    InventoryTotalMass -= (float)inv.CurrentMass * cargoMassMultiplier;
                            }
                        }
                    }

                    BlocksWithInventory.Remove(fatBlock);

                    if(def.HasPhysics)
                        BlockBaseMass -= fatBlock.Mass;
                }
                else
                {
                    if(def.HasPhysics)
                        BlockBaseMass -= slimBlock.Mass;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void InventoryContentChanged(MyInventoryBase inv, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            try
            {
                InventoryTotalMass = -1;
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
