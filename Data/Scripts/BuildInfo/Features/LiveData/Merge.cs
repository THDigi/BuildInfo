using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Merge : BData_Base
    {
        public Base6Directions.Direction Forward;
        public Base6Directions.Direction Right;
        public Matrix SensorMatrix;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            foreach(IMyModelDummy dummy in dummies.Values)
            {
                // from MyShipMergeBlock.LoadDummies()
                if(dummy.Name.ContainsIgnoreCase(Hardcoded.Merge_DummyName))
                {
                    Vector3 halfExtents = dummy.Matrix.Scale / 2f;
                    Vector3 dominantAxis = Vector3.DominantAxisProjection(dummy.Matrix.Translation / halfExtents);
                    dominantAxis.Normalize();

                    Forward = Base6Directions.GetDirection(dominantAxis);
                    Right = Base6Directions.GetPerpendicular(Forward);

                    SensorMatrix = dummy.Matrix; // no normalizing because it just uses the dummy's size as the shape
                    break;
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) || true;
        }
    }
}
