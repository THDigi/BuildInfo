using System.Collections.Generic;
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
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            bool success = false;

            foreach(KeyValuePair<string, IMyModelDummy> kv in dummies)
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