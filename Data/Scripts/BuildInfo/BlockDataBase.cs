using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
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

        public void CheckAndAdd(IMyCubeBlock block)
        {
            var def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;

            if(IsValid(block, def))
                BuildInfo.instance.blockData.Add(def.Id, this);
        }

        public virtual bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            return false;
        }

        public static T TryGetData<T>(MyCubeBlockDefinition def) where T : BlockDataBase, new()
        {
            var data = (T)BuildInfo.instance.blockData.GetValueOrDefault(def.Id, null);

            if(data == null)
            {
                var fakeBlock = SpawnFakeBlock(def);

                if(fakeBlock == null)
                {
                    var error = "Couldn't get block data from fake entity!";
                    Log.Error(error, error);
                }
                else
                {
                    data = new T();
                    data.CheckAndAdd(fakeBlock);
                }
            }

            if(data == null)
            {
                var error = "Couldn't get block data for: " + def.Id;
                Log.Error(error, error);
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
        private static IMyCubeBlock SpawnFakeBlock(MyCubeBlockDefinition def)
        {
            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var spawnPos = camMatrix.Translation + camMatrix.Backward * 10;
            var fakeGridObj = new MyObjectBuilder_CubeGrid()
            {
                CreatePhysics = false,
                PersistentFlags = MyPersistentEntityFlags2.None,
                IsStatic = true,
                GridSizeEnum = def.CubeSize,
                Editable = false,
                DestructibleBlocks = false,
                IsRespawnGrid = false,
                PositionAndOrientation = new MyPositionAndOrientation(spawnPos, Vector3.Forward, Vector3.Up),
                CubeBlocks = new List<MyObjectBuilder_CubeBlock>()
                {
                    new MyObjectBuilder_CubeBlock()
                    {
                        SubtypeName = def.Id.SubtypeName,
                    }
                },
            };

            MyEntities.RemapObjectBuilder(fakeGridObj);
            var fakeEnt = MyEntities.CreateFromObjectBuilderNoinit(fakeGridObj);
            fakeEnt.IsPreview = true;
            fakeEnt.Save = false;
            fakeEnt.Render.Visible = false;
            fakeEnt.Flags = EntityFlags.None;
            MyEntities.InitEntity(fakeGridObj, ref fakeEnt);

            var fakeGrid = (IMyCubeGrid)fakeEnt;
            var fakeBlock = fakeGrid.GetCubeBlock(Vector3I.Zero)?.FatBlock as IMyCubeBlock;
            fakeGrid.Close();
            return fakeBlock;
        }
    }
}
