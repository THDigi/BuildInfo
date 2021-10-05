using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Connector : BData_Base
    {
        /// <summary>
        /// False = ejector.
        /// </summary>
        public bool CanConnect;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            foreach(string name in dummies.Keys)
            {
                // HACK: behavior from MyShipConnector.LoadDummies()
                if(name.ContainsIgnoreCase("connector"))
                {
                    CanConnect = true;
                    break;
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) || true;
        }
    }
}
