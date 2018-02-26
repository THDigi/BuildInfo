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
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), useEntityUpdate: false)]
    public class BlockThruster : BlockBase<BlockDataThrust> { }

    public class BlockDataThrust : BlockDataBase
    {
        public float radius;
        public float distance;
        public int flamesCount;
        public List<FlameInfo> damageFlames = new List<FlameInfo>();

        public struct FlameInfo
        {
            public readonly Vector3 LocalFrom;
            public readonly Vector3 LocalTo;
            public readonly float Radius;
            public readonly Vector3 Direction;
            public readonly float Height;

            public FlameInfo(Vector3 localFrom, Vector3 localTo, float radius)
            {
                LocalFrom = localFrom;
                LocalTo = localTo;
                Radius = radius;

                Direction = (LocalTo - LocalFrom);
                Height = Direction.Normalize();
            }
        }

        public override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            double distSq = 0;

            var thrustDef = (MyThrustDefinition)def;
            var thrust = (MyThrust)block;
            var thrustMatrix = thrust.WorldMatrix;
            var invMatrix = block.WorldMatrixInvScaled;

            // HACK copied from MyThrust.UpdateThrustFlame()
            // HACK make the GetDamageCapsuleLine() method think it thrusts at max and with no random
            thrust.ThrustLengthRand = /* CurrentStrength * */ 10f * /* MyUtils.GetRandomFloat(0.6f, 1f) * */ thrustDef.FlameLengthScale;

            damageFlames.Clear();

            // HACK hardcoded; from MyThrust.ThrustDamageAsync()
            foreach(var flame in thrust.Flames)
            {
                var flameLine = thrust.GetDamageCapsuleLine(flame, ref thrustMatrix);

                //var capsule = new CapsuleD(Vector3D.Transform(flameLine.From, block.WorldMatrixInvScaled), Vector3D.Transform(flameLine.To, block.WorldMatrixInvScaled), flame.Radius * thrustDef.FlameDamageLengthScale);
                damageFlames.Add(new FlameInfo(Vector3D.Transform(flameLine.From, invMatrix), Vector3D.Transform(flameLine.To, invMatrix), flame.Radius * thrustDef.FlameDamageLengthScale));

                var flameDistSq = (flameLine.From - flameLine.To).LengthSquared();

                if(flameDistSq > distSq)
                {
                    distSq = flameDistSq;
                    radius = flame.Radius;
                }
            }

            distance = (float)Math.Sqrt(distSq);
            flamesCount = thrust.Flames.Count;
            return true;
        }
    }
}
