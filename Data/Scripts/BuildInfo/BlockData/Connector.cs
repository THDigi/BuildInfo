using Sandbox.Definitions;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.BlockData
{
    public class BData_Connector : BData_Base
    {
        public bool Connector = false;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
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
            return base.IsValid(block, def) || true;
        }
    }
}
