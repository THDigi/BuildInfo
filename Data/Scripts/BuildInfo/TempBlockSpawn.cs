using System;
using Digi.BuildInfo.Extensions;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo
{
    public class TempBlockSpawn
    {
        public readonly MyCubeGrid SpawnedGrid;
        public readonly bool DeleteGrid;
        public readonly MyCubeBlockDefinition BlockDef;
        public event Action<IMyCubeBlock> AfterSpawn;

        public TempBlockSpawn(MyCubeBlockDefinition def, bool deleteGridOnSpawn = true)
        {
            BlockDef = def;
            DeleteGrid = deleteGridOnSpawn;

            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var spawnPos = camMatrix.Translation + camMatrix.Backward * 100;

            var blockObj = GetBlockObjectBuilder(def.Id);

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
            };

            gridObj.CubeBlocks.Add(blockObj);

            MyAPIGateway.Entities.RemapObjectBuilder(gridObj);

            SpawnedGrid = (MyCubeGrid)MyAPIGateway.Entities.CreateFromObjectBuilderParallel(gridObj, true, SpawnCompleted);
            SpawnedGrid.IsPreview = true;
            SpawnedGrid.Save = false;
            SpawnedGrid.Flags = EntityFlags.None;
            SpawnedGrid.Render.Visible = false;
        }

        private MyObjectBuilder_CubeBlock GetBlockObjectBuilder(MyDefinitionId defId)
        {
            var blockObj = (MyObjectBuilder_CubeBlock)MyObjectBuilderSerializer.CreateNewObject(defId);

            blockObj.EntityId = 0;
            blockObj.Min = Vector3I.Zero;

            // HACK these types do not check if their fields are null in their Remap() method.
            var timer = blockObj as MyObjectBuilder_TimerBlock;
            if(timer != null)
            {
                timer.Toolbar = new MyObjectBuilder_Toolbar();
                return blockObj;
            }

            var button = blockObj as MyObjectBuilder_ButtonPanel;
            if(button != null)
            {
                button.Toolbar = new MyObjectBuilder_Toolbar();
                return blockObj;
            }

            var sensor = blockObj as MyObjectBuilder_SensorBlock;
            if(sensor != null)
            {
                sensor.Toolbar = new MyObjectBuilder_Toolbar();
                return blockObj;
            }

            return blockObj;
        }

        private void SpawnCompleted()
        {
            try
            {
                var block = ((IMyCubeGrid)SpawnedGrid).GetCubeBlock(Vector3I.Zero)?.FatBlock as IMyCubeBlock;

                if(block == null)
                {
                    Log.Error($"Can't get block from spawned entity for block: {BlockDef.Id} (mod workshopId={BlockDef.Context.GetWorkshopID()})");
                    return;
                }

                AfterSpawn?.Invoke(block);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                if(DeleteGrid && SpawnedGrid != null)
                {
                    SpawnedGrid.Close();
                }
            }
        }
    }
}