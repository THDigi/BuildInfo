using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Wheel : BData_Base
    {
        public Vector3 WheelDummy;
        public BoundingBox ModelBB;
        public float WheelRadius;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            bool success = false;

            ModelBB = block.LocalAABB;
            WheelRadius = Math.Max(ModelBB.Depth, ModelBB.Width);

            // from MyAttachableTopBlockBase.LoadDummies()
            Dictionary<string, IMyModelDummy> dummies = Utils.GetDummies(block.Model);
            foreach(KeyValuePair<string, IMyModelDummy> kv in dummies)
            {
                if(kv.Key.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    WheelDummy = Matrix.Normalize(kv.Value.Matrix).Translation;
                    // not sure why this one places it in a corner, but it does... so, out with it.
                    //WheelDummy = (Matrix.Normalize(kv.Value.Matrix) * block.PositionComp.LocalMatrixRef).Translation;
                    success = true;
                    break;
                }
            }

            return base.IsValid(block, def) || success;
        }
    }
}
