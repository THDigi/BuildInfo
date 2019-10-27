using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_ShipTool : BData_Base
    {
        public Matrix DummyMatrix;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            bool success = false;

            foreach(var kv in dummies)
            {
                // dummy name from Sandbox.Game.Weapons.MyShipToolBase.LoadDummies()
                if(kv.Key.ContainsIgnoreCase("detector_shiptool"))
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