﻿using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.MultiTool;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Features.LiveData
{
    /// <summary>
    /// Handles grabbing and catching live block data which requires spawning a temporary grid with block to get that data from the block, because it's unavailable from definitions.
    /// </summary>
    public class LiveDataHandler : ModComponent
    {
        public readonly Dictionary<MyDefinitionId, BData_Base> BlockData = new Dictionary<MyDefinitionId, BData_Base>(MyDefinitionId.Comparer);
        public readonly Dictionary<MyObjectBuilderType, bool> ConveyorSupportTypes = new Dictionary<MyObjectBuilderType, bool>(MyObjectBuilderType.Comparer);

        public event Action<MyDefinitionId, BData_Base> DataGenerated;

        Type ConveyorEndpointInterface = null;
        Type ConveyorSegmentInterface = null;

        readonly Cache DefaultCache = new Cache();

        readonly BData_Base SlimBlockData = new BData_Base();

        public class Cache
        {
            public BData_Base Data;
            public MyDefinitionBase ForDef;
        }

        readonly HashSet<MyDefinitionId> BlockIdsSpawned = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
        readonly Dictionary<MyObjectBuilderType, Func<BData_Base>> BDataInstancer = new Dictionary<MyObjectBuilderType, Func<BData_Base>>();

        public LiveDataHandler(BuildInfoMod main) : base(main)
        {
            AddType<BData_Collector>(typeof(MyObjectBuilder_Collector));

            AddType<BData_ButtonPanel>(typeof(MyObjectBuilder_ButtonPanel));

            AddType<BData_LandingGear>(typeof(MyObjectBuilder_LandingGear));

            AddType<BData_Connector>(typeof(MyObjectBuilder_ShipConnector));

            AddType<BData_Camera>(typeof(MyObjectBuilder_CameraBlock));

            AddType<BData_Merge>(typeof(MyObjectBuilder_MergeBlock));

            AddType<BData_Ladder>(typeof(MyObjectBuilder_Ladder2));

            AddType<BData_Light>(typeof(MyObjectBuilder_LightingBlock));
            AddType<BData_Light>(typeof(MyObjectBuilder_InteriorLight));
            AddType<BData_Light>(typeof(MyObjectBuilder_ReflectorLight));
            AddType<BData_HeatVent>(typeof(MyObjectBuilder_HeatVentBlock));
            AddType<BData_Searchlight>(typeof(MyObjectBuilder_Searchlight));

            AddType<BData_ShipTool>(typeof(MyObjectBuilder_ShipWelder));
            AddType<BData_ShipTool>(typeof(MyObjectBuilder_ShipGrinder));

            AddType<BData_Piston>(typeof(MyObjectBuilder_PistonBase));
            AddType<BData_Piston>(typeof(MyObjectBuilder_ExtendedPistonBase));

            AddType<BData_Motor>(typeof(MyObjectBuilder_MotorStator));
            AddType<BData_Motor>(typeof(MyObjectBuilder_MotorAdvancedStator));

            AddType<BData_Suspension>(typeof(MyObjectBuilder_MotorSuspension));

            AddType<BData_Wheel>(typeof(MyObjectBuilder_RealWheel));
            AddType<BData_Wheel>(typeof(MyObjectBuilder_Wheel));

            AddType<BData_Thrust>(typeof(MyObjectBuilder_Thrust));

            AddType<BData_GatlingGun>(typeof(MyObjectBuilder_SmallGatlingGun));
            AddType<BData_MissileLauncher>(typeof(MyObjectBuilder_SmallMissileLauncher));
            AddType<BData_MissileLauncher>(typeof(MyObjectBuilder_SmallMissileLauncherReload));
            AddType<BData_GatlingTurret>(typeof(MyObjectBuilder_LargeGatlingTurret));
            AddType<BData_MissileTurret>(typeof(MyObjectBuilder_LargeMissileTurret));
            AddType<BData_InteriorTurret>(typeof(MyObjectBuilder_InteriorTurret));

            AddType<BData_TargetDummy>(Hardcoded.TargetDummyType);

            AddType<BData_LaserAntenna>(typeof(MyObjectBuilder_LaserAntenna));

            AddType<BData_AdvancedDoor>(typeof(MyObjectBuilder_AdvancedDoor));


            // no point in getting more info on these blocks
            foreach(var id in Main.Caches.UnplaceableBlocks)
            {
                BlockIdsSpawned.Add(id);
            }

            // every other block type is going to use BData_Base
            Main.BlockMonitor.BlockAdded += BlockMonitor_BlockAdded;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            Main.BlockMonitor.BlockAdded -= BlockMonitor_BlockAdded;
        }

        public T Get<T>(MyCubeBlockDefinition def, Cache cache = null) where T : BData_Base
        {
            if(typeof(T) == typeof(BData_Base) && string.IsNullOrEmpty(def.Model))
                return (T)SlimBlockData;

            T data = null;

            if(cache == null)
                cache = DefaultCache;

            if(cache.ForDef == def)
            {
                data = cache.Data as T;
                if(data != null)
                    return data;
            }

            BData_Base baseData;
            if(BlockData.TryGetValue(def.Id, out baseData))
            {
                data = baseData as T;
            }
            else
            {
                // spawn only once per block type+subtype, to avoid spamming if it's not valid.
                if(BlockIdsSpawned.Add(def.Id)) // returns true if it was added, false if it exists
                    TempBlockSpawn.Spawn(def);

                data = null; // will work next time
            }

            cache.Data = data;
            cache.ForDef = def;
            return data;
        }

        void AddType<T>(MyObjectBuilderType blockType) where T : BData_Base, new()
        {
            BDataInstancer.Add(blockType, Instance<T>);
        }

        static T Instance<T>() where T : BData_Base, new() => new T();

        void BlockMonitor_BlockAdded(IMySlimBlock slimBlock)
        {
            IMyCubeBlock block = slimBlock?.FatBlock;
            if(block == null)
                return; // ignore deformable armor

            // separate process as it needs different kind of caching, and must happen before.
            CheckConveyorSupport(block);

            MyDefinitionId defId = slimBlock.BlockDefinition.Id;
            if(BlockData.ContainsKey(defId))
                return; // already got data for this block type+subtype, ignore

            bool success = false;
            MyCubeBlock internalBlock = (MyCubeBlock)block;
            // IsBuilt is what keen uses before getting subparts on turrets and such
            if(internalBlock.IsBuilt && internalBlock.Model != null)
            {
                BData_Base data = null;
                try
                {
                    // instance special type if available, otherwise the base one.
                    data = BDataInstancer.GetValueOrDefault(defId.TypeId, null)?.Invoke() ?? new BData_Base();
                    success = data.CheckAndAdd(block);
                }
                catch(Exception e)
                {
                    Log.Error($"Error in BData for {defId.ToString()} :: {e}");
                    success = false;
                }

                if(success)
                    DataGenerated?.Invoke(defId, data);
            }

            if(success && Main.TextGeneration != null)
            {
                // reset caches and force block text recalc
                Main.TextGeneration.CachedBuildInfoTextAPI.Remove(defId);
                Main.TextGeneration.CachedBuildInfoNotification.Remove(defId);
                Main.TextGeneration.LastDefId = default(MyDefinitionId);
            }

            //BlocksToCheck.Add(new CheckBlock()
            //{
            //    Block = slimBlock,
            //    CheckAtTick = Main.Tick + 10
            //});
            //
            //SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        //struct CheckBlock
        //{
        //    public IMySlimBlock Block;
        //    public int CheckAtTick;
        //}
        //
        //List<CheckBlock> BlocksToCheck = new List<CheckBlock>();
        //HashSet<IMyCubeGrid> GridsToDelete = new HashSet<IMyCubeGrid>();
        //
        //public override void UpdateAfterSim(int tick)
        //{
        //    for(int i = BlocksToCheck.Count - 1; i >= 0; i--)
        //    {
        //        CheckBlock check = BlocksToCheck[i];
        //        if(check.CheckAtTick <= tick)
        //        {
        //            BlocksToCheck.RemoveAtFast(i);
        //            DoCheck(check.Block);
        //        }
        //    }
        //
        //    if(BlocksToCheck.Count == 0)
        //    {
        //        foreach(IMyCubeGrid grid in GridsToDelete)
        //        {
        //            grid.Close();
        //        }
        //        GridsToDelete.Clear();
        //
        //        SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
        //    }
        //}
        //
        //void DoCheck(IMySlimBlock slimBlock)
        //{
        //    IMyCubeBlock block = slimBlock.FatBlock;
        //    MyDefinitionId defId = slimBlock.BlockDefinition.Id;
        //
        //}

        void CheckConveyorSupport(IMyCubeBlock block)
        {
            if(ConveyorSupportTypes.ContainsKey(block.BlockDefinition.TypeId))
                return;

            Type[] interfaces = MyAPIGateway.Reflection.GetInterfaces(block.GetType());
            bool supportsConveyors = false;

            if(ConveyorEndpointInterface == null || ConveyorSegmentInterface == null)
            {
                for(int i = (interfaces.Length - 1); i >= 0; i--)
                {
                    Type iface = interfaces[i];
                    if(iface.Name == "IMyConveyorEndpointBlock")
                    {
                        ConveyorEndpointInterface = iface;
                        supportsConveyors = true;
                        break;
                    }
                    else if(iface.Name == "IMyConveyorSegmentBlock")
                    {
                        ConveyorSegmentInterface = iface;
                        supportsConveyors = true;
                        break;
                    }
                }
            }
            else
            {
                for(int i = (interfaces.Length - 1); i >= 0; i--)
                {
                    Type iface = interfaces[i];
                    if(iface == ConveyorEndpointInterface || iface == ConveyorSegmentInterface)
                    {
                        supportsConveyors = true;
                        break;
                    }
                }
            }

            ConveyorSupportTypes.Add(block.BlockDefinition.TypeId, supportsConveyors);
        }
    }
}