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

            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D spawnPos = camMatrix.Translation + camMatrix.Backward * 100;

            MyObjectBuilder_CubeBlock blockOB = (MyObjectBuilder_CubeBlock)MyObjectBuilderSerializer.CreateNewObject(def.Id);
            blockOB.EntityId = 0;
            blockOB.Min = Vector3I.Zero;

            MyObjectBuilder_CubeGrid gridOB = new MyObjectBuilder_CubeGrid()
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

            MyCubeGrid grid = (MyCubeGrid)MyAPIGateway.Entities.CreateFromObjectBuilderParallel(gridOB, true, SpawnCompleted);
            grid.DisplayName = grid.Name;
            grid.IsPreview = true;
            grid.Save = false;
            grid.Flags = EntityFlags.None;
            grid.Render.Visible = false;
        }

        void SpawnCompleted(IMyEntity ent)
        {
            IMyCubeGrid grid = ent as IMyCubeGrid;

            try
            {
                IMySlimBlock block = grid?.GetCubeBlock(Vector3I.Zero);
                if(block == null)
                {
                    MyObjectBuilder_Checkpoint.ModItem mod = BlockDef.Context.ModItem;
                    Log.Error($"Can't get block from spawned entity for block: {BlockDef.Id.ToString()}; grid={grid?.EntityId.ToString() ?? "(NULL)"}; mod={mod.GetNameAndId()}");
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