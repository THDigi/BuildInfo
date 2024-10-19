using System;
using System.Collections.Generic;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Digi.BuildInfo.Features
{
    public class GridMassCompute : ModComponent
    {
        public const int MassDataExpireTicks = (Constants.TicksPerSecond * 60 * 5);
        public const int MassDataCheckTicks = (Constants.TicksPerSecond * 60);

        readonly Stack<MassData> DataPool = new Stack<MassData>();
        readonly Dictionary<IMyCubeGrid, MassData> Grids = new Dictionary<IMyCubeGrid, MassData>();
        readonly HashSet<IMyCubeGrid> KeysToRemove = new HashSet<IMyCubeGrid>();

        public GridMassCompute(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            DataPool.Clear();
            Grids.Clear();
            KeysToRemove.Clear();
        }

        public override void UpdateAfterSim(int tick)
        {
            if(tick % MassDataCheckTicks == 0 && Grids.Count > 0)
            {
                foreach(KeyValuePair<IMyCubeGrid, MassData> kv in Grids)
                {
                    int expiresAt = kv.Value.LastReadAtTick + MassDataExpireTicks;
                    if(expiresAt <= tick)
                    {
                        KeysToRemove.Add(kv.Key);
                    }
                }

                if(KeysToRemove.Count > 0)
                {
                    foreach(IMyCubeGrid key in KeysToRemove)
                    {
                        RemoveEntry(key);
                    }

                    KeysToRemove.Clear();
                }
            }
        }

        public float GetGridMass(IMyCubeGrid grid)
        {
            MyCubeGrid internalGrid = (MyCubeGrid)grid;
            float mass = internalGrid.Mass;
            if(mass > 0)
                return mass;

            MassData data;
            if(!Grids.TryGetValue(grid, out data))
            {
                data = (DataPool.Count > 0 ? DataPool.Pop() : new MassData());
                data.Init(grid);
                grid.OnMarkForClose += GridMarkedForClose;

                Grids[grid] = data;

                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
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

                MassData data = Grids.GetValueOrDefault(grid, null);
                if(data != null)
                {
                    Grids.Remove(grid);

                    data.Reset();
                    DataPool.Push(data);
                }

                if(Grids.Count == 0)
                    SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
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

        MyCubeGrid Grid;
        float BlockBaseMass;
        float InventoryTotalMass;

        public void Init(IMyCubeGrid grid)
        {
            if(Grid == grid)
                return;

            if(Grid != null)
                Reset();

            BlockBaseMass = 0;
            InventoryTotalMass = -1;
            Grid = (MyCubeGrid)grid;
            Grid.OnBlockAdded += BlockAdded;
            Grid.OnBlockRemoved += BlockRemoved;

            BlockBaseMass = 0f;

            foreach(IMySlimBlock slimBlock in Grid.GetBlocks())
            {
                MyCubeBlockDefinition def = (MyCubeBlockDefinition)slimBlock.BlockDefinition;

                // HACK: game doesn't use mass from blocks without a collider
                if(def.HasPhysics && def.PhysicsOption != MyPhysicsOption.None) // HACK: inlined HasCollider()
                    BlockBaseMass += def.Mass;

                // HACK: not using IMySlimBlock/IMyCubeBlock .Mass because it's not actually used by the game in mass calc
                //       and spaceball will return its virtual mass for that, screwing up results ever so slightly

                //IMyCubeBlock fatBlock = slimBlock.FatBlock;
                //if(fatBlock != null)
                //{
                //    if(def.HasPhysics)
                //        mass += fatBlock.Mass;
                //}
                //else
                //{
                //    if(def.HasPhysics)
                //        mass += slimBlock.Mass;
                //}
            }

            foreach(MyCubeBlock block in Grid.Inventories)
            {
                if(block.InventoryCount > 0)
                {
                    for(int i = (block.InventoryCount - 1); i >= 0; --i)
                    {
                        MyInventory inv = block.GetInventory(i);
                        if(inv != null)
                            inv.InventoryContentChanged += InventoryContentChanged;
                    }
                }
            }
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
                foreach(MyCubeBlock block in Grid.Inventories)
                {
                    for(int i = (block.InventoryCount - 1); i >= 0; --i)
                    {
                        MyInventory inv = block.GetInventory(i);
                        if(inv != null)
                            inv.InventoryContentChanged -= InventoryContentChanged;
                    }
                }
            }

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

                foreach(MyCubeBlock block in Grid.Inventories)
                {
                    for(int i = (block.InventoryCount - 1); i >= 0; --i)
                    {
                        MyInventory inv = block.GetInventory(i);
                        if(inv != null)
                            InventoryTotalMass += (float)inv.CurrentMass * cargoMassMultiplier;
                    }
                }
            }

            float mass = BlockBaseMass + InventoryTotalMass;

            if(Grid.OccupiedBlocks.Count > 0)
            {
                foreach(MyCockpit seat in Grid.OccupiedBlocks)
                {
                    IMyCharacter pilot = seat?.Pilot;
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
                MyCubeBlockDefinition def = (MyCubeBlockDefinition)slimBlock.BlockDefinition;
                bool hasCollider = def.HasPhysics && def.PhysicsOption != MyPhysicsOption.None; // HACK: inlined def.HasCollider();

                IMyCubeBlock fatBlock = slimBlock.FatBlock;
                if(fatBlock != null)
                {
                    if(fatBlock.InventoryCount > 0)
                    {
                        float cargoMassMultiplier = 1f / MyAPIGateway.Session.SessionSettings.BlocksInventorySizeMultiplier;

                        for(int i = (fatBlock.InventoryCount - 1); i >= 0; --i)
                        {
                            MyInventory inv = (MyInventory)fatBlock.GetInventory(i);
                            if(inv != null)
                            {
                                inv.InventoryContentChanged += InventoryContentChanged;

                                if(InventoryTotalMass > 0)
                                    InventoryTotalMass += (float)inv.CurrentMass * cargoMassMultiplier;
                            }
                        }
                    }

                    if(hasCollider)
                        BlockBaseMass += fatBlock.Mass;
                }
                else
                {
                    if(hasCollider)
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
                MyCubeBlockDefinition def = (MyCubeBlockDefinition)slimBlock.BlockDefinition;
                bool hasCollider = def.HasPhysics && def.PhysicsOption != MyPhysicsOption.None; // HACK: inlined def.HasCollider();

                IMyCubeBlock fatBlock = slimBlock.FatBlock;
                if(fatBlock != null)
                {
                    if(fatBlock.InventoryCount > 0)
                    {
                        float cargoMassMultiplier = 1f / MyAPIGateway.Session.SessionSettings.BlocksInventorySizeMultiplier;

                        for(int i = (fatBlock.InventoryCount - 1); i >= 0; --i)
                        {
                            MyInventory inv = (MyInventory)fatBlock.GetInventory(i);
                            if(inv != null)
                            {
                                inv.InventoryContentChanged -= InventoryContentChanged;

                                if(InventoryTotalMass > 0)
                                    InventoryTotalMass -= (float)inv.CurrentMass * cargoMassMultiplier;
                            }
                        }
                    }

                    if(hasCollider)
                        BlockBaseMass -= fatBlock.Mass;
                }
                else
                {
                    if(hasCollider)
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
