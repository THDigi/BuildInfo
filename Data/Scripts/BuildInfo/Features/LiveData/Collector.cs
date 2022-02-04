using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
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
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            // from MyCollector.LoadDummies()
            foreach(KeyValuePair<string, IMyModelDummy> dummy in dummies)
            {
                if(dummy.Key.ContainsIgnoreCase(Hardcoded.Collector_DummyName))
                {
                    Matrix dummyMatrix = dummy.Value.Matrix;
                    boxLocalMatrix = Matrix.Normalize(dummyMatrix);

                    // from GetBoxFromMatrix()
                    //MatrixD matrix = Matrix.Normalize(dummyMatrix) * block.WorldMatrix;
                    //Quaternion orientation = Quaternion.CreateFromRotationMatrix(matrix);
                    //Vector3 halfExtents = Vector3.Abs(dummyMatrix.Scale) / 2f;
                    //halfExtents = new Vector3(halfExtents.X, halfExtents.Y, halfExtents.Z);
                    //Vector3D position = matrix.Translation;

                    break;
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) || true;
        }
    }
}
