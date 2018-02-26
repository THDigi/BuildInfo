using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ShipWelder), useEntityUpdate: false)]
    public class BlockShipWelder : MyGameLogicComponent
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
                    var def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;

                    if(!BuildInfo.instance.blockData.ContainsKey(def.Id) && block.Model.AssetName == def.Model)
                    {
                        new BlockDataShipWelder(block);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

    public class BlockDataShipWelder : IBlockData
    {
        public BoundingSphere sphereDummy;

        public BlockDataShipWelder(IMyCubeBlock block)
        {
            var def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;
            var dummies = new Dictionary<string, IMyModelDummy>();

            if(block.Model.GetDummies(dummies) == 0)
                return;

            // HACK: copied from Sandbox.Game.Weapons.MyShipToolBase.LoadDummies()

            foreach(var kv in dummies)
            {
                if(kv.Key.ToUpper().Contains("DETECTOR_SHIPTOOL"))
                {
                    var matrix = kv.Value.Matrix;
                    var radius = matrix.Scale.AbsMin();
                    sphereDummy = new BoundingSphere(matrix.Translation, radius);
                    BuildInfo.instance.blockData.Add(def.Id, this);
                    break;
                }
            }
        }
    }
}
