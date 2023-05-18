using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.Entity;
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

            GroupStatusDel groupFunc = GroupRefill;
            processor.AddGroupStatus(typeof(MyObjectBuilder_GasTank), groupFunc, "Refill");
            processor.AddGroupStatus(typeof(MyObjectBuilder_OxygenTank), groupFunc, "Refill");
        }

        bool Refill(StringBuilder sb, ToolbarItem item)
        {
            IMyGasTank tank = (IMyGasTank)item.Block;

            int emptyBottles = 0;
            int fullBottles = 0;

            MyInventory inv = tank.GetInventory(0) as MyInventory;
            if(inv != null)
            {
                foreach(MyPhysicalInventoryItem physItem in inv.GetItems())
                {
                    MyObjectBuilder_GasContainerObject bottleOB = physItem.Content as MyObjectBuilder_GasContainerObject;
                    if(bottleOB != null)
                    {
                        if(bottleOB.GasLevel < 1f)
                            emptyBottles += (int)physItem.Amount;
                        else
                            fullBottles += (int)physItem.Amount;
                    }
                }
            }

            if(!Processor.AppendSingleStats(sb, tank) && tank.FilledRatio < 0.1f)
            {
                sb.Append(IconAlert).Append("LowGas\n");
            }

            if(fullBottles > 0)
                sb.NumberCappedSpaced(fullBottles, MaxChars - 4).Append("full\n");

            if(emptyBottles > 0)
                sb.NumberCappedSpaced(emptyBottles, MaxChars - 5).Append("empty\n");

            sb.TrimEndWhitespace();
            return true;
        }

        bool GroupRefill(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyGasTank>())
                return false;

            int off = 0;
            int broken = 0;
            int haveGas = 0;
            int emptyBottles = 0;

            foreach(IMyGasTank tank in groupData.Blocks)
            {
                if(!tank.IsFunctional)
                    broken++;

                if(!tank.Enabled)
                    off++;

                MyInventory inv = tank.GetInventory(0) as MyInventory;
                if(inv == null)
                    continue;

                if(tank.FilledRatio > 0)
                    haveGas++;

                foreach(MyPhysicalInventoryItem physItem in inv.GetItems())
                {
                    MyObjectBuilder_GasContainerObject bottleOB = physItem.Content as MyObjectBuilder_GasContainerObject;
                    if(bottleOB != null)
                    {
                        if(bottleOB.GasLevel < 1f)
                            emptyBottles += (int)physItem.Amount;
                    }
                }
            }

            int total = groupData.Blocks.Count;

            if(haveGas == total)
            {
                Processor.AppendGroupStats(sb, broken, off);

                if(emptyBottles > 0)
                {
                    sb.Append("Refill:\n");
                    sb.NumberCappedSpaced((int)emptyBottles, MaxChars);
                }
                else
                {
                    sb.Append("All:\n");
                    sb.Append("NoBottle");
                }
            }
            else if(haveGas == 0)
            {
                Processor.AppendGroupStats(sb, broken, off);

                sb.Append("All:\n");
                sb.Append("No Gas");
            }
            else
            {
                if(!Processor.AppendGroupStats(sb, broken, off))
                    sb.Append("(Mixed)\n");

                int noGas = total - haveGas;
                sb.NumberCappedSpaced((int)noGas, MaxChars - 5).Append("NoGas\n");
                sb.NumberCappedSpaced((int)emptyBottles, MaxChars - 5).Append("Bottl"); // typo on purpose to have 3 spaces for "99+" numbers
            }

            return true;
        }
    }
}
