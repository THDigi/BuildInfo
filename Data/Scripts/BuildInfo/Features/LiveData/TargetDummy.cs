using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_TargetDummy : BData_Base
    {
        public struct PartInfo
        {
            public readonly Matrix LocalMatrix;
            public readonly float Health;
            public readonly bool IsCritical;

            public PartInfo(Matrix localMatrix, float health, bool isCritical)
            {
                LocalMatrix = localMatrix;
                Health = health;
                IsCritical = isCritical;
            }
        }

        public Dictionary<string, PartInfo> ShootableParts = new Dictionary<string, PartInfo>();

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            bool success = false;

            MyCubeBlock internalBlock = (MyCubeBlock)block;
            MyTargetDummyBlockDefinition dummyDef = def as MyTargetDummyBlockDefinition;
            if(dummyDef != null)
            {
                foreach(KeyValuePair<string, MyEntitySubpart> kv in internalBlock.Subparts)
                {
                    MyTargetDummyBlockDefinition.MyDummySubpartDescription subpartDesc;
                    if(dummyDef.SubpartDefinitions.TryGetValue(kv.Key, out subpartDesc))
                    {
                        ShootableParts.Add(kv.Key, new PartInfo(kv.Value.PositionComp.LocalMatrixRef, subpartDesc.Health, subpartDesc.IsCritical));
                        success = true;
                    }
                }
            }

            return base.IsValid(block, def) || success;
        }
    }
}