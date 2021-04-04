using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
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
            bool success = false;

            var dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();

            block.Model.GetDummies(dummies);

            // logic from MyLandingGear.LoadDummies()
            //var lockPositions = (from s in dummies
            //                     where s.Key.ToLower().Contains("gear_lock")
            //                     select s.Value.Matrix).ToArray<Matrix>();

            foreach(var dummy in dummies.Values)
            {
                if(!dummy.Name.ContainsIgnoreCase("gear_lock"))
                    continue;

                // HACK: copied from MyLandingGear.GetBoxFromMatrix()
                var mn = MatrixD.Normalize(dummy.Matrix);
                var orientation = Quaternion.CreateFromRotationMatrix(mn);
                var halfExtents = Vector3.Abs(dummy.Matrix.Scale) / 2f;

                // ... and from MyLandingGear.FindBody()
                halfExtents *= new Vector3(2f, 1f, 2f);
                orientation.Normalize();

                Magents.Add(new MyOrientedBoundingBoxD(mn.Translation, halfExtents, orientation));
                success = true;
            }

            dummies.Clear();
            return base.IsValid(block, def) || success;
        }
    }
}
