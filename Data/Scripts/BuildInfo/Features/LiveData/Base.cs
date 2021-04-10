using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.LiveData
{
    public abstract class BData_Base
    {
        public BData_Base()
        {
        }

        public bool CheckAndAdd(IMyCubeBlock block)
        {
            var def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;

            if(IsValid(block, def))
            {
                BuildInfoMod.Instance.LiveDataHandler.BlockData.Add(def.Id, this);
                return true;
            }

            return false;
        }

        protected virtual bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            return false;
        }

        public static T TryGetDataCached<T>(MyCubeBlockDefinition def) where T : BData_Base, new()
        {
            var handler = BuildInfoMod.Instance.LiveDataHandler;
            var data = handler.BlockDataCache as T;

            if(data == null)
                data = TryGetData<T>(def);

            if(data == null)
                handler.BlockDataCacheValid = false;

            handler.BlockDataCache = data;
            return data;
        }

        private static T TryGetData<T>(MyCubeBlockDefinition def) where T : BData_Base, new()
        {
            var handler = BuildInfoMod.Instance.LiveDataHandler;
            BData_Base data;
            if(handler.BlockData.TryGetValue(def.Id, out data))
                return data as T;

            // spawn only once per block type+subtype, to avoid spamming if it's not valid.
            if(handler.BlockIdsSpawned.Add(def.Id)) // returns true if it was added, false if it exists
            {
                new TempBlockSpawn(def, callback: SpawnComplete<T>);
            }

            return null;
        }

        private static void SpawnComplete<T>(IMySlimBlock block) where T : BData_Base, new()
        {
            var handler = BuildInfoMod.Instance.LiveDataHandler;
            var defId = block.BlockDefinition.Id;
        }

        public static bool TrySetData<T>(IMyCubeBlock block) where T : BData_Base, new()
        {
            var handler = BuildInfoMod.Instance.LiveDataHandler;
            var def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;

            if(handler.BlockData.ContainsKey(def.Id))
                return true;

            var internalBlock = (MyCubeBlock)block;
            if(!internalBlock.IsBuilt) // it's what keen uses before getting subparts on turrets and such
                return false;

            //if(block.Model.AssetName != def.Model)
            //    return false;

            var data = new T();
            return data.CheckAndAdd(block);
        }
    }
}