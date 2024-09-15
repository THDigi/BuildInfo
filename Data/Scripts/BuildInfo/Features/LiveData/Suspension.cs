using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Suspension : BData_Motor
    {
        public MyMotorSuspensionDefinition SuspensionDef;

        // from MyMotorSuspension.GetTopGridMatrix()
        public MatrixD GetWheelMatrix(ref Matrix localMatrix, ref MatrixD blockWorldMatrix, ref MatrixD gridWorldMatrix, float height)
        {
            Vector3 forward = localMatrix.Forward;
            Vector3 dummyPos = GetDummyLocalPosition(localMatrix, displacement: 0);
            return MatrixD.CreateWorld(Vector3D.Transform(dummyPos + forward * height, gridWorldMatrix), blockWorldMatrix.Forward, blockWorldMatrix.Up);
        }

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            SuspensionDef = def as MyMotorSuspensionDefinition;

            return base.IsValid(block, def);
        }
    }
}
