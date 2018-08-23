using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.BlockData
{
    public class BData_ShipTool : BData_Base
    {
        public Matrix DummyMatrix;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var dummies = BuildInfo.Instance.dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            bool success = false;

            foreach(var kv in dummies)
            {
                // HACK: copied from Sandbox.Game.Weapons.MyShipToolBase.LoadDummies()
                if(kv.Key.ToUpper().Contains("DETECTOR_SHIPTOOL"))
                {
                    DummyMatrix = kv.Value.Matrix;
                    success = true;
                    break;
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) || success;
        }
    }
}