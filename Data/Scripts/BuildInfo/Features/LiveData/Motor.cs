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
        protected Vector3 DummyLocalPos;

        // from MyMotorBase.DummyPosition
        public Vector3 GetDummyLocalPosition(Matrix localMatrix, float displacement)
        {
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
                    DummyLocalPos = Matrix.Normalize(kv.Value.Matrix).Translation;
                    success = true;
                    break;
                }
            }

            return base.IsValid(block, def) || success;
        }
    }
}
