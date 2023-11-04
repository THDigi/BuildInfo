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
        public Matrix BoxLocalMatrix;

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
                    // from MyCollector.LoadDummies()
                    //Matrix matrix = dummy.Value.Matrix;
                    //GetBoxFromMatrix(matrix, out Vector3 halfExtents, out Vector3 _, out Quaternion _);
                    //HkBvShape shape = CreateFieldShape(halfExtents);
                    //base.Physics = new MyPhysicsBody(this, RigidBodyFlag.RBF_UNLOCKED_SPEEDS);
                    //base.Physics.IsPhantom = true;
                    //base.Physics.CreateFromCollisionObject(shape, matrix.Translation, base.WorldMatrix, null, 26);
                    //base.Physics.Enabled = true;
                    //base.Physics.RigidBody.ContactPointCallbackEnabled = false;
                    //shape.Base.RemoveReference();
                    //break;

                    Matrix dummyMatrix = dummy.Value.Matrix;

                    //Vector3 halfExtents;
                    //Vector3 pos;
                    //Quaternion orient;
                    //GetBoxFromMatrix(block, dummyMatrix, out halfExtents, out pos, out orient);

                    Vector3 size = Vector3.Abs(dummyMatrix.Scale);
                    BoxLocalMatrix = Matrix.CreateTranslation(dummyMatrix.Translation);
                    Matrix.Rescale(ref BoxLocalMatrix, ref size);
                    break;
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) || true;
        }

        // from MyCollector
        //void GetBoxFromMatrix(IMyEntity entity, Matrix m, out Vector3 halfExtents, out Vector3 position, out Quaternion orientation)
        //{
        //    MatrixD matrix = Matrix.Normalize(m) * entity.WorldMatrix;
        //    orientation = Quaternion.CreateFromRotationMatrix(matrix);
        //    halfExtents = Vector3.Abs(m.Scale) / 2f;
        //    halfExtents = new Vector3(halfExtents.X, halfExtents.Y, halfExtents.Z);
        //    position = matrix.Translation;
        //}
    }
}
