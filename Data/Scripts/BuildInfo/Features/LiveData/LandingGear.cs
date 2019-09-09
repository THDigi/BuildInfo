using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_LandingGear : BData_Base
    {
        public readonly List<MyOrientedBoundingBoxD> Magents = new List<MyOrientedBoundingBoxD>();

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            // HACK copied from MyLandingGear.LoadDummies()
            var lockPositions = (from s in dummies
                                 where s.Key.ToLower().Contains("gear_lock")
                                 select s.Value.Matrix).ToArray<Matrix>();

            dummies.Clear();

            bool success = false;

            if(lockPositions.Length > 0)
            {
                for(int i = 0; i < lockPositions.Length; ++i)
                {
                    var m = lockPositions[i];

                    // HACK copied from MyLandingGear.GetBoxFromMatrix()
                    var mn = MatrixD.Normalize(m);
                    var orientation = Quaternion.CreateFromRotationMatrix(mn);
                    var halfExtents = Vector3.Abs(m.Scale) / 2f;
                    halfExtents *= new Vector3(2f, 1f, 2f);
                    orientation.Normalize();

                    Magents.Add(new MyOrientedBoundingBoxD(mn.Translation, halfExtents, orientation));
                }

                success = true;
            }

            return base.IsValid(block, def) || success;
        }
    }
}
