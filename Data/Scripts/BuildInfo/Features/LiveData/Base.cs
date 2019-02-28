using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.LiveData
{
    public abstract class BData_Base
    {
        private static LiveDataHandler Handler => BuildInfoMod.Client.LiveDataHandler;

        public BData_Base()
        {
        }

        public bool CheckAndAdd(IMyCubeBlock block)
        {
            var def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;

            if(IsValid(block, def))
            {
                Handler.BlockData.Add(def.Id, this);
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
            var data = Handler.BlockDataCache as T;

            if(data == null)
                data = TryGetData<T>(def);

            if(data == null)
                Handler.BlockDataCacheValid = false;

            Handler.BlockDataCache = data;
            return data;
        }

        private static T TryGetData<T>(MyCubeBlockDefinition def) where T : BData_Base, new()
        {
            BData_Base data;

            if(Handler.BlockData.TryGetValue(def.Id, out data))
                return (T)data;

            if(Handler.BlockSpawnInProgress.Add(def.Id)) // spawn block if it's not already in progress of being spawned
            {
                var spawn = new TempBlockSpawn(def);
                spawn.AfterSpawn += SpawnComplete<T>;
            }

            return null;
        }

        private static void SpawnComplete<T>(IMyCubeBlock block) where T : BData_Base, new()
        {
            var defId = (MyDefinitionId)block.BlockDefinition;

            Handler.BlockSpawnInProgress.Remove(defId);
        }

        public static bool TrySetData<T>(IMyCubeBlock block) where T : BData_Base, new()
        {
            var def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;

            if(Handler.BlockData.ContainsKey(def.Id))
                return true;

            if(block.Model.AssetName != def.Model)
                return false;

            var data = new T();
            return data.CheckAndAdd(block);
        }
    }
}