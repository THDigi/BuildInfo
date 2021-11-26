using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Suspension : BData_Motor
    {
        // from MyMotorSuspension.GetTopGridMatrix()
        public MatrixD GetWheelMatrix(Matrix localMatrix, MatrixD blockWorldMatrix, MatrixD gridWorldMatrix, float height)
        {
            Vector3 forward = localMatrix.Forward;
            Vector3 dummyPos = GetDummyLocalPosition(localMatrix, displacement: 0);
            return MatrixD.CreateWorld(Vector3D.Transform(dummyPos + forward * height, gridWorldMatrix), blockWorldMatrix.Forward, blockWorldMatrix.Up);
        }
    }
}
