using System;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public struct TempBlockSpawn
    {
        public static void Spawn(MyCubeBlockDefinition def, bool deleteGridOnSpawn = true, Action<IMySlimBlock> callback = null)
        {
            new TempBlockSpawn(def, deleteGridOnSpawn, callback);
        }

        readonly bool DeleteGrid;
        readonly MyCubeBlockDefinition BlockDef;
        readonly Action<IMySlimBlock> Callback;

        TempBlockSpawn(MyCubeBlockDefinition def, bool deleteGridOnSpawn = true, Action<IMySlimBlock> callback = null)
        {
            BlockDef = def;
            DeleteGrid = deleteGridOnSpawn;
            Callback = callback;

            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var spawnPos = camMatrix.Translation + camMatrix.Backward * 100;

            var blockOB = CreateBlockOB(def.Id);

            var gridOB = new MyObjectBuilder_CubeGrid()
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
            };

            gridOB.CubeBlocks.Add(blockOB);

            // not really required for a single grid.
            //MyAPIGateway.Entities.RemapObjectBuilder(gridOB);

            var grid = (MyCubeGrid)MyAPIGateway.Entities.CreateFromObjectBuilderParallel(gridOB, true, SpawnCompleted);
            grid.IsPreview = true;
            grid.Save = false;
            grid.Flags = EntityFlags.None;
            grid.Render.Visible = false;
        }

        MyObjectBuilder_CubeBlock CreateBlockOB(MyDefinitionId defId)
        {
            var blockObj = (MyObjectBuilder_CubeBlock)MyObjectBuilderSerializer.CreateNewObject(defId);

            blockObj.EntityId = 0;
            blockObj.Min = Vector3I.Zero;

#if false
            // NOTE these types do not check if their fields are null in their Remap() method.
            var timer = blockObj as MyObjectBuilder_TimerBlock;
            if(timer != null)
            {
                timer.Toolbar = BuildInfoMod.Instance.Caches.EmptyToolbarOB;
                return blockObj;
            }

            var button = blockObj as MyObjectBuilder_ButtonPanel;
            if(button != null)
            {
                button.Toolbar = BuildInfoMod.Instance.Caches.EmptyToolbarOB;
                return blockObj;
            }

            var sensor = blockObj as MyObjectBuilder_SensorBlock;
            if(sensor != null)
            {
                sensor.Toolbar = BuildInfoMod.Instance.Caches.EmptyToolbarOB;
                return blockObj;
            }

            // prohibited...
            //var targetDummy = blockObj as MyObjectBuilder_TargetDummyBlock;
            //if(targetDummy != null)
            //{
            //    targetDummy.Toolbar = BuildInfoMod.Instance.Caches.EmptyToolbarOB;
            //    return blockObj;
            //}
#endif

            return blockObj;
        }

        void SpawnCompleted(IMyEntity ent)
        {
            var grid = ent as IMyCubeGrid;

            try
            {
                var block = grid?.GetCubeBlock(Vector3I.Zero);
                if(block == null)
                {
                    Log.Error($"Can't get block from spawned entity for block: {BlockDef.Id.ToString()}; grid={grid?.EntityId.ToString() ?? "(NULL)"} (mod workshopId={BlockDef.Context.GetWorkshopID().ToString()})");
                    return;
                }

                Callback?.Invoke(block);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                if(DeleteGrid && grid != null)
                {
                    grid.Close();
                }
            }
        }
    }
}