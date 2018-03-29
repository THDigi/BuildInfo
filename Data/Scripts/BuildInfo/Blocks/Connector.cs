using System.Collections.Generic;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Blocks
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ShipConnector), useEntityUpdate: false)]
    public class BlockConnector : BlockBase<BData_Connector> { }

    public class BData_Connector : BData_Base
    {
        public bool Connector = false;

        public override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var dummies = BuildInfo.Instance.dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            foreach(var name in dummies.Keys)
            {
                // HACK behavior from MyShipConnector.LoadDummies()
                if(name.ToLower().Contains("connector"))
                {
                    Connector = true;
                    break;
                }
            }

            dummies.Clear();
            return true;
        }
    }
}
