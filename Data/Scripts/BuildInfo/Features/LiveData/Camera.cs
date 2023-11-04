using System.Collections.Generic;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Camera : BData_Base
    {
        public Vector3 DummyLocalAdditive;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            // from MyCameraBlock.GetViewMatrix()
            foreach(KeyValuePair<string, IMyModelDummy> dummy in dummies)
            {
                if(dummy.Key == Hardcoded.Camera_DummyName)
                {
                    DummyLocalAdditive = dummy.Value.Matrix.Translation;
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) || true;
        }
    }
}
