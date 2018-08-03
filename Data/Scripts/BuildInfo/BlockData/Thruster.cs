using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.BlockData
{
    public class BData_Thrust : BData_Base
    {
        public float HighestRadius;
        public float HighestLength;
        public List<FlameInfo> Flames = new List<FlameInfo>();

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

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var thrust = (IMyThrust)block;
            var thrustDef = (MyThrustDefinition)def;
            var thrustMatrix = thrust.WorldMatrix;
            var invMatrix = block.WorldMatrixInvScaled;

            // HACK copied from MyThrust's ThrustDamageAsync(), UpdateThrustFlame(), GetDamageCapsuleLine()
            var thrustLength = /* CurrentStrength * */ 10f * /* MyUtils.GetRandomFloat(0.6f, 1f) * */ thrustDef.FlameLengthScale;

            var dummies = BuildInfo.Instance.dummies;
            dummies.Clear();
            Flames.Clear();
            HighestLength = 0;
            HighestRadius = 0;

            thrust.Model.GetDummies(dummies);

            foreach(var dummy in dummies.Values)
            {
                if(dummy.Name.StartsWith("thruster_flame", StringComparison.InvariantCultureIgnoreCase))
                {
                    var startPosition = dummy.Matrix.Translation;
                    var direction = Vector3.Normalize(dummy.Matrix.Forward);
                    var radius = Math.Max(dummy.Matrix.Scale.X, dummy.Matrix.Scale.Y) * 0.5f;
                    var length = thrustLength * radius * thrustDef.FlameDamageLengthScale - radius;
                    var endPosition = startPosition + direction * length;

                    Flames.Add(new FlameInfo(startPosition, endPosition, radius));

                    HighestLength = Math.Max(HighestLength, length);
                    HighestRadius = Math.Max(HighestRadius, radius);
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) || true;
        }
    }
}
