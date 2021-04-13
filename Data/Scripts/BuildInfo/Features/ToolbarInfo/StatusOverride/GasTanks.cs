using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using static Digi.BuildInfo.Features.ToolbarInfo.ToolbarStatusProcessor;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class GasTanks : StatusOverrideBase
    {
        public GasTanks(ToolbarStatusProcessor processor) : base(processor)
        {
            StatusDel func = Refill;
            processor.AddStatus(typeof(MyObjectBuilder_GasTank), func, "Refill");
            processor.AddStatus(typeof(MyObjectBuilder_OxygenTank), func, "Refill");

            // TODO: group status for gas tank
            //GroupStatusDel groupFunc = StatusGroup;
            //processor.AddGroupStatus(typeof(MyObjectBuilder_GasTank), groupFunc, "Refill");
            //processor.AddGroupStatus(typeof(MyObjectBuilder_OxygenTank), groupFunc, "Refill");
        }

        bool Refill(StringBuilder sb, ToolbarItem item)
        {
            var tank = (IMyGasTank)item.Block;

            bool canFill = false;

            if(tank.IsWorking)
            {
                var tankDef = (MyGasTankDefinition)tank.SlimBlock.BlockDefinition;
                canFill = !(!MyAPIGateway.Session.SessionSettings.EnableOxygen && tankDef.StoredGasId == MyResourceDistributorComponent.OxygenId);
            }

            int itemsToFill = 0;

            if(canFill)
            {
                var inv = tank.GetInventory(0) as MyInventory;
                if(inv != null)
                {
                    foreach(var physItem in inv.GetItems())
                    {
                        var containerOB = physItem.Content as MyObjectBuilder_GasContainerObject;
                        if(containerOB != null && containerOB.GasLevel < 1f)
                            itemsToFill++;
                    }
                }
            }

            if(itemsToFill > 0)
            {
                sb.Append("Refill:").Append('\n').Append(itemsToFill);
            }
            else
            {
                sb.Append(tank.FilledRatio > 0 ? "No Bottles" : "No Gas");
            }

            return true;
        }

        //bool StatusGroup(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        //{
        //    if(!groupData.GetGroupBlocks<IMyGasTank>())
        //        return false;
        //
        //    return true;
        //}
    }
}
