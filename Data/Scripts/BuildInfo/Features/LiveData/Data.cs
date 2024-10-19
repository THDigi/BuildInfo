using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class TurretInfo
    {
        public Vector3 YawLocalPos;
        public Vector3 YawModelCenter;
        public Vector3 PitchLocalPos;

        public void AssignData(MyCubeBlock block, MyEntity subpartYaw, MyEntity subpartPitch)
        {
            if(subpartYaw != null)
            {
                YawLocalPos = (Vector3)Vector3D.Transform(subpartYaw.WorldMatrix.Translation, block.PositionComp.WorldMatrixInvScaled);
                YawModelCenter = (Vector3)Vector3D.Transform(subpartYaw.PositionComp.WorldAABB.Center, block.PositionComp.WorldMatrixInvScaled);

                // avoid y-fighting if it's a multiple of grid size
                int y = (int)(YawLocalPos.Y * 100);
                int gs = (int)(block.CubeGrid.GridSize * 100);
                if(y % gs == 0)
                    YawLocalPos += new Vector3(0, 0.05f, 0);
            }

            if(subpartPitch != null)
            {
                PitchLocalPos = (Vector3)Vector3D.Transform(subpartPitch.WorldMatrix.Translation, block.PositionComp.WorldMatrixInvScaled);
            }
        }
    }

    public class TurretAttachmentInfo
    {
        public Matrix? RelativeSubpart;
        public Matrix? RelativePreview;

        public void AssignData(MyEntity subpart, MyCubeBlock block, string dummyName)
        {
            if(subpart?.Model != null)
            {
                IMyModelDummy cameraDummy = subpart.Model.GetDummies().GetValueOrDefault(dummyName, null);
                if(cameraDummy != null)
                {
                    RelativeSubpart = Matrix.Normalize(cameraDummy.Matrix);

                    MatrixD worldspace = RelativeSubpart.Value * subpart.WorldMatrix;
                    RelativePreview = worldspace * block.PositionComp.WorldMatrixInvScaled; // to block-local
                }
            }
        }
    }

    public class LightLogicData
    {
        public readonly string DummyName;

        public readonly bool HasSubpartLights = false;
        public readonly List<LightInfo> Lights = new List<LightInfo>(0);

        public readonly MyBounds LightOffset;
        public readonly MyBounds LightRadius;

        /// <summary>
        /// Used to calculate point light radius reduction for spotlights
        /// </summary>
        public readonly float LightReflectorRadiusMax;

        public readonly bool IsSpotlight = false;
        public readonly float SpotlightFOVRad;
        public readonly float SpotlightConeTan;

        public struct LightInfo
        {
            /// <summary>
            /// Transformed to be relative to block entity instead of subpart
            /// </summary>
            public Matrix BlockLocalMatrix;

            /// <summary>
            /// Dummy's normalized matrix to be transformed by below parent
            /// </summary>
            public Matrix DummyMatrix;

            /// <summary>
            /// Use only when calling <see cref="GetSubpartLightDataRecursive"/> on a block to update this to a proper value!
            /// </summary>
            public MyEntity Subpart;
        }

        public LightLogicData(IMyCubeBlock block, string dummyName, MyBounds lightRadius, float lightReflectorRadiusMax, MyBounds lightOffset, float? coneDegrees = null)
        {
            DummyName = dummyName;
            LightRadius = lightRadius;
            LightReflectorRadiusMax = lightReflectorRadiusMax;
            LightOffset = lightOffset;

            IsSpotlight = coneDegrees != null;
            if(IsSpotlight)
            {
                // HACK: all this because searchlight has 0 degrees and it's capped elsewhere in the game code
                float coneRad = MathHelper.ToRadians(coneDegrees.Value);
                float coneMaxAngleCos = 1f - (float)Math.Cos(coneRad / 2f); // MyLight.ConeRadiansToConeMaxAngleCos()
                float apertureCos = (float)Math.Min(Math.Max(1f - coneMaxAngleCos, 0.01), 0.99000000953674316); // MyLight.UpdateLight()

                SpotlightFOVRad = (float)(Math.Acos(apertureCos) * 2);
                SpotlightConeTan = (float)Math.Tan(SpotlightFOVRad / 2);
            }

            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            // from MyLightingLogic.UpdateLightData()
            foreach(IMyModelDummy dummy in dummies.Values)
            {
                if(dummy.Name.IndexOf("subpart", StringComparison.OrdinalIgnoreCase) != -1)
                    continue; // skip subparts

                if(dummy.Name.IndexOf(DummyName, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    Lights.Add(new LightInfo()
                    {
                        BlockLocalMatrix = Matrix.Normalize(dummy.Matrix),
                    });
                }
            }

            int countBeforeSubparts = Lights.Count;
            MatrixD transformToParent = block.WorldMatrixInvScaled;
            GetSubpartLightDataRecursive(Lights, (MyEntity)block, DummyName, ref transformToParent, isDefinition: true);
            HasSubpartLights = Lights.Count > countBeforeSubparts;
        }

        public static void GetSubpartLightDataRecursive(List<LightInfo> addTo, MyEntity entity, string dummyName, ref MatrixD transformToParent, bool isDefinition = false)
        {
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;

            foreach(KeyValuePair<string, MyEntitySubpart> kv in entity.Subparts)
            {
                dummies.Clear();
                ((IMyModel)kv.Value.Model).GetDummies(dummies);

                foreach(IMyModelDummy dummy in dummies.Values)
                {
                    if(dummy.Name.IndexOf(dummyName, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        Matrix dm = Matrix.Normalize(dummy.Matrix);
                        Matrix blockLocal = (dm * kv.Value.WorldMatrix) * transformToParent;

                        addTo.Add(new LightInfo()
                        {
                            BlockLocalMatrix = blockLocal,
                            DummyMatrix = dm,
                            Subpart = (isDefinition ? null : kv.Value),
                        });
                    }
                }

                dummies.Clear();
                GetSubpartLightDataRecursive(addTo, kv.Value, dummyName, ref transformToParent, isDefinition);
            }
        }
    }
}