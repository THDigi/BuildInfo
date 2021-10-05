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

            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();

            block.Model.GetDummies(dummies);

            // logic from MyLandingGear.LoadDummies()
            //var lockPositions = (from s in dummies
            //                     where s.Key.ToLower().Contains("gear_lock")
            //                     select s.Value.Matrix).ToArray<Matrix>();

            foreach(IMyModelDummy dummy in dummies.Values)
            {
                if(!dummy.Name.ContainsIgnoreCase("gear_lock"))
                    continue;

                // HACK: copied from MyLandingGear.GetBoxFromMatrix()
                MatrixD mn = MatrixD.Normalize(dummy.Matrix);
                Quaternion orientation = Quaternion.CreateFromRotationMatrix(mn);
                Vector3 halfExtents = Vector3.Abs(dummy.Matrix.Scale) / 2f;

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
