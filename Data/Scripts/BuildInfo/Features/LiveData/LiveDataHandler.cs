﻿using System;
using System.Collections.Generic;
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

        BData_Base DataCache;
        MyDefinitionId DataCacheForId;

        readonly HashSet<MyDefinitionId> BlockIdsSpawned = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
        readonly Dictionary<MyObjectBuilderType, Func<BData_Base>> BDataInstancer = new Dictionary<MyObjectBuilderType, Func<BData_Base>>();

        public LiveDataHandler(BuildInfoMod main) : base(main)
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

            AddType<BData_TargetDummy>(Constants.TargetDummyType);

            AddType<BData_LaserAntenna>(typeof(MyObjectBuilder_LaserAntenna));

            // every other block type is going to use BData_Base

            Main.BlockMonitor.BlockAdded += BlockMonitor_BlockAdded;
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
        }

        public override void UnregisterComponent()
        {
            Main.BlockMonitor.BlockAdded -= BlockMonitor_BlockAdded;
            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
        }

        public T Get<T>(MyCubeBlockDefinition def) where T : BData_Base
        {
            T data = null;

            if(DataCache != null && def.Id == DataCacheForId)
            {
                data = DataCache as T;
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

            DataCacheForId = def.Id;
            DataCache = data;
            return data;
        }

        public void InvalidateCache()
        {
            DataCache = null;
            DataCacheForId = default(MyDefinitionId);
        }

        void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            DataCache = null;
            DataCacheForId = default(MyDefinitionId);
        }

        void AddType<T>(MyObjectBuilderType blockType) where T : BData_Base, new()
        {
            BDataInstancer.Add(blockType, Instance<T>);
        }

        static T Instance<T>() where T : BData_Base, new() => new T();

        void BlockMonitor_BlockAdded(IMySlimBlock slimBlock)
        {
            var block = slimBlock?.FatBlock;
            if(block == null)
                return; // ignore deformable armor

            // separate process as it needs different kind of caching, and must happen before.
            CheckConveyorSupport(block);

            var defId = slimBlock.BlockDefinition.Id;
            if(BlockData.ContainsKey(defId))
                return; // already got data for this block type+subtype, ignore

            bool success = false;
            var internalBlock = (MyCubeBlock)block;
            if(internalBlock.IsBuilt) // it's what keen uses before getting subparts on turrets and such
            {
                // instance special type if available, otherwise the base one.
                var data = BDataInstancer.GetValueOrDefault(defId.TypeId, null)?.Invoke() ?? new BData_Base();
                success = data.CheckAndAdd(block);
            }

            if(success && Main.TextGeneration != null)
            {
                // reset caches and force block text recalc
                Main.TextGeneration.CachedBuildInfoTextAPI.Remove(slimBlock.BlockDefinition.Id);
                Main.TextGeneration.CachedBuildInfoNotification.Remove(slimBlock.BlockDefinition.Id);
                Main.TextGeneration.LastDefId = default(MyDefinitionId);
            }
        }

        Type ConveyorEndpointInterface;
        Type ConveyorSegmentInterface;

        void CheckConveyorSupport(IMyCubeBlock block)
        {
            if(ConveyorSupportTypes.ContainsKey(block.BlockDefinition.TypeId))
                return;

            var interfaces = MyAPIGateway.Reflection.GetInterfaces(block.GetType());
            bool supportsConveyors = false;

            if(ConveyorEndpointInterface == null || ConveyorSegmentInterface == null)
            {
                for(int i = (interfaces.Length - 1); i >= 0; i--)
                {
                    var iface = interfaces[i];
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
                    var iface = interfaces[i];
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
