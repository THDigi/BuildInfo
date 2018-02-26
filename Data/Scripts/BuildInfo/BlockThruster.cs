using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), useEntityUpdate: false)]
    public class BlockThruster : MyGameLogicComponent
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
                        new BlockDataThrust(block);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

    public class BlockDataThrust : IBlockData
    {
        public readonly float radius;
        public readonly float distance;
        public readonly int flames;

        public BlockDataThrust(MyThrust thrust)
        {
            var def = thrust.BlockDefinition;
            double distSq = 0;

            // HACK hardcoded; from MyThrust.UpdateThrustFlame()
            thrust.ThrustLengthRand = 10f * def.FlameLengthScale; // make the GetDamageCapsuleLine() method think it thrusts at max and with no random

            var m = thrust.WorldMatrix;

            foreach(var flame in thrust.Flames)
            {
                var flameLine = thrust.GetDamageCapsuleLine(flame, ref m);
                var flameDistSq = (flameLine.From - flameLine.To).LengthSquared();

                if(flameDistSq > distSq)
                {
                    distSq = flameDistSq;
                    radius = flame.Radius;
                }
            }

            distance = (float)Math.Sqrt(distSq);
            flames = thrust.Flames.Count;

            BuildInfo.instance.blockData.Add(def.Id, this);
        }

        public static BlockDataThrust GetData(MyCubeBlockDefinition def)
        {
            var data = (BlockDataThrust)BuildInfo.instance.blockData.GetValueOrDefault(def.Id, null);

            if(data == null)
            {
                var fakeBlock = BuildInfo.SpawnFakeBlock(def);

                if(fakeBlock == null)
                {
                    var error = "Couldn't get block data from fake entity!";
                    Log.Error(error, error);
                }
                else
                {
                    data = new BlockDataThrust((MyThrust)fakeBlock);
                }
            }

            if(data == null)
            {
                var error = "Couldn't get block data for: " + def.Id;
                Log.Error(error, error);
            }

            return data;
        }
    }
}
