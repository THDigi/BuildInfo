using Sandbox.Definitions;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Connector : BData_Base
    {
        public bool Connector = false;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var dummies = BuildInfoMod.Caches.Dummies;
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
