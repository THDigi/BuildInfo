﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using ParallelTasks;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo.Blocks
{
    public class BlockBase<T> : MyGameLogicComponent where T : BData_Base, new()
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                if(BuildInfo.Instance != null && BuildInfo.Instance.IsPlayer) // only rendering players need to use this
                {
                    var block = (IMyCubeBlock)Entity;
                    BData_Base.TrySetData<T>(block);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

    public class BData_Base
    {
        public BData_Base()
        {
        }

        public bool CheckAndAdd(IMyCubeBlock block)
        {
            var def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;

            if(IsValid(block, def))
            {
                BuildInfo.Instance.BlockData.Add(def.Id, this);
                return true;
            }

            return false;
        }

        public virtual bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            return false;
        }

        public static T TryGetDataCached<T>(MyCubeBlockDefinition def) where T : BData_Base, new()
        {
            var data = BuildInfo.Instance.BlockDataCache as T;

            if(data == null)
                data = TryGetData<T>(def);

            if(data == null)
                BuildInfo.Instance.BlockDataCacheValid = false;

            BuildInfo.Instance.BlockDataCache = data;
            return data;
        }

        private static T TryGetData<T>(MyCubeBlockDefinition def) where T : BData_Base, new()
        {
            var mod = BuildInfo.Instance;
            BData_Base data;

            if(mod.BlockData.TryGetValue(def.Id, out data))
                return (T)data;

            if(mod.BlockSpawnInProgress.Add(def.Id)) // spawn block if it's not already in progress of being spawned
            {
                new BlockSpawn<T>(def);
            }

            return null;
        }

        public static void TrySetData<T>(IMyCubeBlock block) where T : BData_Base, new()
        {
            var def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;

            if(BuildInfo.Instance.BlockData.ContainsKey(def.Id) || block.Model.AssetName != def.Model)
                return;

            new T().CheckAndAdd(block);
        }
    }
}