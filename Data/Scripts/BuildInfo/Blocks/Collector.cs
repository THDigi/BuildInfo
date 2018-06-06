using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Blocks
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Collector), useEntityUpdate: false)]
    public class BlockCollector : BlockBase<BData_Collector> { }

    public class BData_Collector : BData_Base
    {
        public Matrix boxLocalMatrix;

        public override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var dummies = BuildInfo.Instance.dummies;
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

            return true;
        }
    }
}
