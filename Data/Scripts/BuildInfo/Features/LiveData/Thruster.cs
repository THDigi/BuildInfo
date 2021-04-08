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
        public float LongestFlamePastEdge;
        public float LongestFlame;
        public float DamagePerTickToBlocks;
        public float DamagePerTickToOther;
        public List<FlameInfo> Flames = new List<FlameInfo>();

        public struct FlameInfo
        {
            public readonly Vector3 LocalFrom;
            public readonly Vector3 LocalTo;
            public readonly Vector3 Direction;
            public readonly float DummyRadius;
            public readonly float CapsuleRadius;
            public readonly float CapsuleLength;

            public FlameInfo(Vector3 localFrom, Vector3 localTo, float dummyRadius, float capsuleRadius)
            {
                LocalFrom = localFrom;
                LocalTo = localTo;
                DummyRadius = dummyRadius;
                CapsuleRadius = capsuleRadius;

                Direction = (LocalTo - LocalFrom);
                CapsuleLength = Direction.Normalize();
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

            var dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            Flames.Clear();
            LongestFlame = 0;
            LongestFlamePastEdge = 0;
            DamagePerTickToBlocks = 0;
            DamagePerTickToOther = 0;

            thrust.Model.GetDummies(dummies);

            foreach(var dummy in dummies.Values)
            {
                if(dummy.Name.StartsWith("thruster_flame", StringComparison.InvariantCultureIgnoreCase))
                {
                    Vector3 startPosition = dummy.Matrix.Translation;
                    Vector3 direction = Vector3.Normalize(dummy.Matrix.Forward);
                    float dummyRadius = Math.Max(dummy.Matrix.Scale.X, dummy.Matrix.Scale.Y) * 0.5f; // from MyThrust.LoadDummies()

                    DamagePerTickToBlocks += thrustDef.FlameDamage; // from MyThrust.DamageGrid()
                    DamagePerTickToOther += dummyRadius * thrustDef.FlameDamage; // from MyThrust.ThrustDamageDealDamage()

                    float flameLength = 2f * ((thrustMaxLength * dummyRadius * 0.5f) * thrustDef.FlameDamageLengthScale) - dummyRadius; // from MyThrust.GetDamageCapsuleLine()
                    Vector3 endPosition = startPosition + direction * flameLength;

                    LongestFlame = Math.Max(LongestFlame, flameLength);

                    float capsuleRadius = dummyRadius * thrustDef.FlameDamageLengthScale; // from MyThrust.ThrustDamageShapeCast()
                    Flames.Add(new FlameInfo(startPosition, endPosition, dummyRadius, capsuleRadius));

                    // compute how far the flame goes outside of block's boundingbox
                    Vector3 capsuleEdgeStart = startPosition - direction * capsuleRadius;
                    float capsuleEdgesLength = flameLength + capsuleRadius * 2;

                    Vector3 blockHalfSize = def.Size * (0.5f * MyDefinitionManager.Static.GetCubeSize(def.CubeSize));
                    var blockBB = new BoundingBox(-blockHalfSize, blockHalfSize);

                    const float RayStartDistance = 1000; // arbitrary large value
                    var ray = new Ray(capsuleEdgeStart + direction * RayStartDistance, -direction);
                    float? hitDist = blockBB.Intersects(ray);

                    float pastEdge = 0;
                    if(hitDist.HasValue)
                    {
                        float penetrating = (RayStartDistance - hitDist.Value);
                        pastEdge = capsuleEdgesLength - penetrating; // if it penetrates more than the flame length then it's be negative
                    }

                    LongestFlamePastEdge = Math.Max(LongestFlamePastEdge, pastEdge);
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) || true;
        }
    }
}
