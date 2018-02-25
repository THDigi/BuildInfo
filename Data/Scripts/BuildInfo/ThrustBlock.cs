using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), useEntityUpdate: false)]
    public class ThrustBlock : MyGameLogicComponent
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
                    var block = (MyThrust)Entity;

                    if(!BuildInfo.instance.blockData.ContainsKey(block.BlockDefinition.Id) && ((IMyModel)block.Model).AssetName == block.BlockDefinition.Model)
                        new BuildInfo.BlockDataThrust(block);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
