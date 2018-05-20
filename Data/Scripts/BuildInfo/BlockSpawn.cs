using System;
using System.Collections.Generic;
using Digi.BuildInfo.Blocks;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo
{
    public class BlockSpawn<T> where T : BData_Base, new()
    {
        private readonly MyCubeBlockDefinition def;
        private readonly long entityId;

        public BlockSpawn(MyCubeBlockDefinition def)
        {
            this.def = def;
            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var spawnPos = camMatrix.Translation + camMatrix.Backward * 100;

            var blockObj = (MyObjectBuilder_CubeBlock)MyObjectBuilderSerializer.CreateNewObject(def.Id);

            var gridObj = new MyObjectBuilder_CubeGrid()
            {
                CreatePhysics = false,
                GridSizeEnum = def.CubeSize,
                PositionAndOrientation = new MyPositionAndOrientation(spawnPos, Vector3.Forward, Vector3.Up),
                PersistentFlags = MyPersistentEntityFlags2.InScene,
                IsStatic = true,
                Editable = false,
                DestructibleBlocks = false,
                IsRespawnGrid = false,
                Name = "BuildInfo_TemporaryGrid",
                CubeBlocks = new List<MyObjectBuilder_CubeBlock>(1) { blockObj },
            };

            MyAPIGateway.Entities.RemapObjectBuilder(gridObj);

            this.entityId = gridObj.EntityId;

            MyAPIGateway.Entities.CreateFromObjectBuilderParallel(gridObj, true, SpawnCompleted);
        }

        private void SpawnCompleted()
        {
            try
            {
                var ent = MyEntities.GetEntityById(entityId, true);

                if(ent == null)
                {
                    Log.Error($"Can't get spawned entity for block: {def.Id}");
                    return;
                }

                ent.IsPreview = true;
                ent.Save = false;
                ent.Flags = EntityFlags.None;
                ent.Render.Visible = false;

                var grid = (IMyCubeGrid)ent;
                var block = grid.GetCubeBlock(Vector3I.Zero)?.FatBlock as IMyCubeBlock;
                grid.Close();

                if(block == null)
                {
                    Log.Error($"Can't get block from spawned entity for block: {def.Id} (mod workshopId={def.Context.GetWorkshopID()})");
                    return;
                }

                var data = new T();
                var added = data.CheckAndAdd(block);

                var mod = BuildInfo.Instance;

                mod.BlockSpawnInProgress.Remove(def.Id);

                if(added)
                {
                    BuildInfo.Instance.BlockDataCache = null;
                    BuildInfo.Instance.BlockDataCacheValid = true;

                    // remove cache in order to use the newly aquired data
                    mod.CachedBuildInfoTextAPI.Remove(def.Id);
                    mod.CachedBuildInfoNotification.Remove(def.Id);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
