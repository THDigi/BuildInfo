using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Collector : BData_Base
    {
        public Matrix boxLocalMatrix;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            // HACK copied from MyCollector.LoadDummies()
            foreach(var dummy in dummies)
            {
                if(dummy.Key.ToLower().Contains("collector"))
                {
                    Matrix dummyMatrix = dummy.Value.Matrix;

                    boxLocalMatrix = dummyMatrix;

                    //MatrixD matrix = Matrix.Normalize(dummyMatrix) * block.WorldMatrix;
                    //var orientation = Quaternion.CreateFromRotationMatrix(matrix);
                    //var halfExtents = Vector3.Abs(dummyMatrix.Scale) / 2f;
                    //halfExtents = new Vector3(halfExtents.X, halfExtents.Y, halfExtents.Z);
                    //var position = matrix.Translation;

                    break;
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) || true;
        }
    }
}
