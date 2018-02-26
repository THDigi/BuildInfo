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
    public class BlockThruster : BlockBase<BlockDataThrust> { }

    public class BlockDataThrust : BlockDataBase
    {
        public float radius;
        public float distance;
        public int flames;

        public override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            double distSq = 0;

            var thrustDef = (MyThrustDefinition)def;
            var thrust = (MyThrust)block;

            // HACK hardcoded; from MyThrust.UpdateThrustFlame()
            thrust.ThrustLengthRand = 10f * thrustDef.FlameLengthScale; // make the GetDamageCapsuleLine() method think it thrusts at max and with no random

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
            return true;
        }
    }
}
