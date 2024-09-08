using System;
using System.Collections.Generic;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Thrust : BData_Base
    {
        /// <summary>
        /// Longest past the block edge. NOTE: This is for blocks only, only uses the cylinder! Add the <see cref="LongestFlameCapsuleRadius"/> to get the damage-all range.
        /// </summary>
        public float LongestFlame;
        public float LongestFlameCapsuleRadius;

        public float DamagePerTickToBlocks;
        public float DamagePerTickToOther;
        public List<FlameInfo> Flames = new List<FlameInfo>();

        public struct FlameInfo
        {
            public readonly Vector3 LocalFrom;
            public readonly Vector3 LocalDirection;
            public readonly float CapsuleRadius;
            public readonly float CapsuleLength;
            //public readonly float DummyRadius;

            public FlameInfo(Vector3 localFrom, Vector3 localDirection, float capsuleRadius, float capsuleLength)
            {
                LocalFrom = localFrom;
                LocalDirection = localDirection;
                CapsuleRadius = capsuleRadius;
                CapsuleLength = capsuleLength;
            }
        }

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            IMyThrust thrust = (IMyThrust)block;
            MyThrustDefinition thrustDef = (MyThrustDefinition)def;
            MatrixD thrustMatrix = thrust.WorldMatrix;
            MatrixD invMatrix = block.WorldMatrixInvScaled;

            // ThrustLengthRand = CurrentStrength * 10f * MyUtils.GetRandomFloat(0.6f, 1f) * BlockDefinition.FlameLengthScale;
            float thrustMaxLength = 10f * thrustDef.FlameLengthScale;

            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            Flames.Clear();
            LongestFlame = 0;
            LongestFlameCapsuleRadius = 0;
            DamagePerTickToBlocks = 0;
            DamagePerTickToOther = 0;

            thrust.Model.GetDummies(dummies);

            foreach(IMyModelDummy dummy in dummies.Values)
            {
                // from MyThrust.LoadDummies()
                if(dummy.Name.StartsWith(Hardcoded.Thrust_DummyPrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    if(dummy.Name.EndsWith(Hardcoded.Thrust_DummyNoDamageSuffix))
                        continue;

                    Vector3 startPosition = dummy.Matrix.Translation;
                    Vector3 direction = Vector3.Normalize(dummy.Matrix.Forward);
                    float dummyRadius = Math.Max(dummy.Matrix.Scale.X, dummy.Matrix.Scale.Y) * 0.5f;

                    DamagePerTickToBlocks += thrustDef.FlameDamage; // from MyThrust.DamageGrid()
                    DamagePerTickToOther += dummyRadius * thrustDef.FlameDamage; // from MyThrust.ThrustDamageDealDamage()

                    float flameLength = 2f * ((thrustMaxLength * dummyRadius * 0.5f) * thrustDef.FlameDamageLengthScale) - dummyRadius; // from MyThrust.GetDamageCapsuleLine()
                    float capsuleRadius = dummyRadius * thrustDef.FlameDamageLengthScale; // from MyThrust.ThrustDamageShapeCast()
                    Flames.Add(new FlameInfo(startPosition, direction, capsuleRadius, flameLength));

                    #region flame length outside the block

                    // HACK: not including capsule radius because game doesn't for blocks only, and blocks are main thing we care about
                    Vector3 capsuleEdgeStart = startPosition; // - direction * capsuleRadius;
                    float capsuleEdgesLength = flameLength; // + capsuleRadius * 2;

                    Vector3 blockHalfSize = def.Size * (0.5f * MyDefinitionManager.Static.GetCubeSize(def.CubeSize));
                    BoundingBox blockBB = new BoundingBox(-blockHalfSize, blockHalfSize);

                    // TODO: thrusters with angled flames?

                    const float RayStartDistance = 10000; // arbitrary large value
                    Ray ray = new Ray(capsuleEdgeStart + direction * RayStartDistance, -direction);
                    float? hitDist = blockBB.Intersects(ray);

                    float pastEdge = float.NaN;
                    if(hitDist.HasValue)
                    {
                        float penetrating = (RayStartDistance - hitDist.Value);
                        pastEdge = capsuleEdgesLength - penetrating; // if it penetrates more than the flame length then it's negative
                    }
                    else
                    {
                        Log.Error($"Unexpected: {def.Id} has flame that doesn't intersect the block itself, please report to BuildInfo author with the mod!");
                    }

                    if(pastEdge > LongestFlame)
                    {
                        LongestFlame = pastEdge;
                        LongestFlameCapsuleRadius = capsuleRadius;
                    }
                    #endregion
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) || true;
        }
    }
}
