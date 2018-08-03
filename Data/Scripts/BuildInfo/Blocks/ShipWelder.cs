using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.BlockData
{
    public class BData_ShipTool : BData_Base
    {
        public BoundingSphere SphereDummy;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            bool success = false;
            var dummies = BuildInfo.Instance.dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            foreach(var kv in dummies)
            {
                // HACK: copied from Sandbox.Game.Weapons.MyShipToolBase.LoadDummies()
                if(kv.Key.ToUpper().Contains("DETECTOR_SHIPTOOL"))
                {
                    var matrix = kv.Value.Matrix;
                    var radius = matrix.Scale.AbsMin();
                    SphereDummy = new BoundingSphere(matrix.Translation, radius);
                    success = true;
                    break;
                }
            }

            dummies.Clear();
            return success;
        }
    }
}