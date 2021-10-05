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
        public struct SubpartInfo
        {
            public readonly Matrix LocalMatrix;
            public readonly float Health;
            public readonly bool IsCritical;

            public SubpartInfo(Matrix localMatrix, float health, bool isCritical)
            {
                LocalMatrix = localMatrix;
                Health = health;
                IsCritical = isCritical;
            }
        }

        public Dictionary<string, SubpartInfo> Subparts = new Dictionary<string, SubpartInfo>();

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
                        Subparts.Add(kv.Key, new SubpartInfo(kv.Value.PositionComp.LocalMatrixRef, subpartDesc.Health, subpartDesc.IsCritical));
                        success = true;
                    }
                }
            }

            return base.IsValid(block, def) || success;
        }
    }
}