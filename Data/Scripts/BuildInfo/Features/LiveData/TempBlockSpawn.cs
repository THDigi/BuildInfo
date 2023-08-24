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
    public class TempBlockSpawn
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

            MyObjectBuilder_CubeGrid gridOB = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_CubeGrid>();
            gridOB.EntityId = 0;
            gridOB.DisplayName = gridOB.Name = "BuildInfo_TemporaryGrid";
            gridOB.CreatePhysics = false;
            gridOB.GridSizeEnum = def.CubeSize;
            gridOB.PositionAndOrientation = new MyPositionAndOrientation(spawnPos, Vector3.Forward, Vector3.Up);
            gridOB.PersistentFlags = MyPersistentEntityFlags2.InScene;
            gridOB.IsStatic = true;
            gridOB.Editable = false;
            gridOB.DestructibleBlocks = false;
            gridOB.IsRespawnGrid = false;
            gridOB.CubeBlocks.Add(blockOB);

            MyCubeGrid grid = (MyCubeGrid)MyAPIGateway.Entities.CreateFromObjectBuilderParallel(gridOB, true, SpawnCompleted);

            grid.IsPreview = true;
            grid.Save = false;
        }

        void SpawnCompleted(IMyEntity ent)
        {
            // has to be here if wanna do this, but not really important anyway
            // if done before it fully initializes, it can crash for certain blocks, like Holo LCD
            //ent.Render.Visible = false;

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