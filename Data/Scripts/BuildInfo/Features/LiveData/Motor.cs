using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Motor : BData_Base
    {
        public MyMotorStatorDefinition StatorDef;
        public MyCubeBlockDefinition TopDef;

        /// <summary>
        /// Local to stator/body.
        /// </summary>
        public Vector3I? TopDir;

        protected Vector3 DummyLocalPos;

        // from MyMotorBase.DummyPosition
        protected Vector3 GetDummyLocalPosition(Matrix localMatrix, float displacement)
        {
            if(StatorDef != null)
                displacement -= StatorDef.RotorDisplacementInModel;

            // TODO: StatorDef.MechanicalTopInitialPlacementOffset?

            Vector3 offset = Vector3.Zero;
            if(DummyLocalPos.LengthSquared() > 0f)
            {
                offset = Vector3.DominantAxisProjection(DummyLocalPos);
                offset.Normalize();
                offset *= displacement;
            }
            else
            {
                offset = new Vector3(0f, displacement, 0f);
            }

            return Vector3.Transform(DummyLocalPos + offset, localMatrix);
        }

        // from MyMotorBase.GetTopGridMatrix()
        public MatrixD GetRotorMatrix(Matrix localMatrix, MatrixD blockWorldMatrix, MatrixD gridWorldMatrix, float displacement)
        {
            Vector3 forward = localMatrix.Forward;
            Vector3 dummyPos = GetDummyLocalPosition(localMatrix, displacement);
            return MatrixD.CreateWorld(Vector3D.Transform(dummyPos, gridWorldMatrix), blockWorldMatrix.Forward, blockWorldMatrix.Up);
        }

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            bool success = false;

            // from MyMotorBase.LoadDummyPosition()
            Dictionary<string, IMyModelDummy> dummies = Utils.GetDummies(block.Model);
            foreach(KeyValuePair<string, IMyModelDummy> kv in dummies)
            {
                if(kv.Key.StartsWith("electric_motor", StringComparison.InvariantCultureIgnoreCase))
                {
                    DummyLocalPos = kv.Value.Matrix.Translation;
                    success = true;
                    break;
                }
            }

            Vector3I? topLocalDir = null;
            StatorDef = def as MyMotorStatorDefinition;
            if(StatorDef != null)
            {
                MyCubeBlockDefinitionGroup blockPair = MyDefinitionManager.Static.TryGetDefinitionGroup(StatorDef.TopPart);
                if(blockPair != null)
                {
                    TopDef = blockPair[def.CubeSize];

                    // find top part's direction from the largest mountpoint side
                    if(TopDef.MountPoints != null)
                    {
                        if(TopDef.MountPoints.Length == 1)
                        {
                            topLocalDir = TopDef.MountPoints[0].Normal;
                        }
                        else if(TopDef.MountPoints.Length > 1)
                        {
                            Dictionary<Vector3I, float> sizePerDir = new Dictionary<Vector3I, float>();

                            // combine all mount sizes per direction
                            for(int i = 0; i < TopDef.MountPoints.Length; i++)
                            {
                                MyCubeBlockDefinition.MountPoint mount = TopDef.MountPoints[i];

                                float size = (mount.End - mount.Start).Volume;
                                sizePerDir[mount.Normal] = sizePerDir.GetValueOrDefault(mount.Normal, 0) + size;
                            }

                            // get largest direction
                            Vector3I? largestMountNormal = null;
                            float largestSize = 0;
                            foreach(KeyValuePair<Vector3I, float> kv in sizePerDir)
                            {
                                if(kv.Value > largestSize)
                                {
                                    largestSize = kv.Value;
                                    largestMountNormal = kv.Key;
                                }
                            }

                            if(largestMountNormal.HasValue)
                            {
                                topLocalDir = largestMountNormal.Value;
                            }
                        }
                    }
                }
            }

            // transform to be relative to body instead of top
            if(topLocalDir.HasValue)
            {
                float displacement = 0;
                Vector3 dummyPos = GetDummyLocalPosition(Matrix.Identity, displacement);

                Vector3D topDirWorld = Vector3D.TransformNormal((Vector3D)topLocalDir.Value, MatrixD.CreateTranslation(dummyPos));
                TopDir = (Vector3I)Vector3D.TransformNormal(topDirWorld, MatrixD.Invert(MatrixD.Identity));
            }

            return base.IsValid(block, def) || success;
        }
    }
}
