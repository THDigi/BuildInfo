using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo
{
    public class BlockBase<T> : MyGameLogicComponent where T : BlockDataBase, new()
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                if(BuildInfo.instance != null && !BuildInfo.instance.isThisDS) // only rendering players need to use this, DS has none so skipping it; also instance is null on DS but checking just in case
                {
                    var block = (IMyCubeBlock)Entity;
                    BlockDataBase.SetData<T>(block);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

    public class BlockDataBase
    {
        public BlockDataBase()
        {
        }

        public bool CheckAndAdd(IMyCubeBlock block)
        {
            var def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;

            if(IsValid(block, def))
            {
                BuildInfo.instance.blockData.Add(def.Id, this);
                return true;
            }

            return false;
        }

        public virtual bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            return false;
        }

        public static T TryGetDataCached<T>(MyCubeBlockDefinition def) where T : BlockDataBase, new()
        {
            var data = BuildInfo.instance.blockDataCache as T;

            if(data == null)
                data = TryGetData<T>(def);

            BuildInfo.instance.blockDataCache = data;
            return data;
        }

        public static T TryGetData<T>(MyCubeBlockDefinition def) where T : BlockDataBase, new()
        {
            var data = (T)BuildInfo.instance.blockData.GetValueOrDefault(def.Id, null);

            if(data == null)
            {
                var fakeBlock = SpawnTemporaryBlock(def);

                if(fakeBlock == null)
                {
                    var error = "Couldn't create fake block!";
                    Log.Error(error, error);
                    return null;
                }

                data = new T();

                if(!data.CheckAndAdd(fakeBlock))
                    return null;
            }

            if(data == null)
            {
                var error = "Couldn't get block data for: " + def.Id;
                Log.Error(error, error);
                return null;
            }

            return data;
        }

        public static void SetData<T>(IMyCubeBlock block) where T : BlockDataBase, new()
        {
            var def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;

            if(BuildInfo.instance.blockData.ContainsKey(def.Id) || block.Model.AssetName != def.Model)
                return;

            new T().CheckAndAdd(block);
        }

        /// <summary>
        /// Spawns a ghost grid with the requested block definition, used for getting data that is only obtainable from a placed block.
        /// </summary>
        private static IMyCubeBlock SpawnTemporaryBlock(MyCubeBlockDefinition def)
        {
            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var spawnPos = camMatrix.Translation + camMatrix.Backward * 10;
            var gridObj = new MyObjectBuilder_CubeGrid()
            {
                CreatePhysics = false,
                PersistentFlags = MyPersistentEntityFlags2.None,
                IsStatic = true,
                GridSizeEnum = def.CubeSize,
                Editable = false,
                DestructibleBlocks = false,
                IsRespawnGrid = false,
                PositionAndOrientation = new MyPositionAndOrientation(spawnPos, Vector3.Forward, Vector3.Up),
                CubeBlocks = new List<MyObjectBuilder_CubeBlock>(),
            };

            var blockObj = (MyObjectBuilder_CubeBlock)MyObjectBuilderSerializer.CreateNewObject(def.Id);
            gridObj.CubeBlocks.Add(blockObj);

            MyEntities.RemapObjectBuilder(gridObj);
            var ent = MyEntities.CreateFromObjectBuilderNoinit(gridObj);
            ent.IsPreview = true;
            ent.Save = false;
            ent.Render.Visible = false;
            ent.Flags = EntityFlags.None;
            MyEntities.InitEntity(gridObj, ref ent);

            var grid = (IMyCubeGrid)ent;
            var block = grid.GetCubeBlock(Vector3I.Zero)?.FatBlock as IMyCubeBlock;
            grid.Close();
            return block;
        }
    }
}