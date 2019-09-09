using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Thrust : BData_Base
    {
        public float HighestRadius;
        public float HighestLength;
        public float TotalBlockDamage;
        public float TotalOtherDamage;
        public List<FlameInfo> Flames = new List<FlameInfo>();

        public struct FlameInfo
        {
            public readonly Vector3 LocalFrom;
            public readonly Vector3 LocalTo;
            public readonly Vector3 Direction;
            public readonly float Radius;
            public readonly float CapsuleRadius;
            public readonly float Length;

            public FlameInfo(Vector3 localFrom, Vector3 localTo, float radius, float capsuleRadius)
            {
                LocalFrom = localFrom;
                LocalTo = localTo;
                Radius = radius;
                CapsuleRadius = capsuleRadius;

                Direction = (LocalTo - LocalFrom);
                Length = Direction.Normalize();
            }
        }

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var thrust = (IMyThrust)block;
            var thrustDef = (MyThrustDefinition)def;
            var thrustMatrix = thrust.WorldMatrix;
            var invMatrix = block.WorldMatrixInvScaled;

            // HACK data copied from MyThrust

            // ThrustLengthRand = CurrentStrength * 10f * MyUtils.GetRandomFloat(0.6f, 1f) * BlockDefinition.FlameLengthScale;
            var thrustMaxLength = 10f * thrustDef.FlameLengthScale;

            var dummies = BuildInfoMod.Caches.Dummies;
            dummies.Clear();
            Flames.Clear();
            HighestLength = 0;
            HighestRadius = 0;
            TotalBlockDamage = 0;
            TotalOtherDamage = 0;
            const int DAMAGE_TICKS = 60;

            thrust.Model.GetDummies(dummies);

            foreach(var dummy in dummies.Values)
            {
                if(dummy.Name.StartsWith("thruster_flame", StringComparison.InvariantCultureIgnoreCase))
                {
                    var startPosition = dummy.Matrix.Translation;
                    var direction = Vector3.Normalize(dummy.Matrix.Forward);
                    var radius = Math.Max(dummy.Matrix.Scale.X, dummy.Matrix.Scale.Y) * 0.5f; // from MyThrust.LoadDummies()

                    float length = 2f * ((thrustMaxLength * radius * 0.5f) * thrustDef.FlameDamageLengthScale) - radius; // from MyThrust.GetDamageCapsuleLine()

                    if(thrustDef.SlowdownFactor > 1) // if dampeners are stronger than normal thrust then the flame will be longer.
                        length *= thrustDef.SlowdownFactor;

                    var endPosition = startPosition + direction * length;

                    float capsuleRadius = radius * thrustDef.FlameDamageLengthScale; // from MyThrust.ThrustDamageShapeCast()
                    Flames.Add(new FlameInfo(startPosition, endPosition, radius, capsuleRadius));

                    #region calculating damage line from block bounding box
                    float RAY_OFFSET = 1000;
                    var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                    var half = def.Size * (0.5f * gridSize);
                    var bb = new BoundingBox(-half, half);
                    var ray = new Ray(startPosition + direction * RAY_OFFSET, -direction);
                    var hitDist = bb.Intersects(ray);

                    if(hitDist.HasValue)
                        length += (hitDist.Value - RAY_OFFSET);
                    #endregion calculating damage line from block bounding box

                    HighestLength = Math.Max(HighestLength, length);
                    HighestRadius = Math.Max(HighestRadius, radius);

                    TotalBlockDamage += DAMAGE_TICKS * thrustDef.FlameDamage; // from MyThrust.DamageGrid()
                    TotalOtherDamage += DAMAGE_TICKS * radius * thrustDef.FlameDamage; // from MyThrust.ThrustDamageDealDamage()
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) || true;
        }
    }
}
