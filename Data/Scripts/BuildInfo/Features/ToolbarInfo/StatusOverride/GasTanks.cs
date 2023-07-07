using System;
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
            Register(Refill, GroupRefill, "Refill");

            Register(Stockpile, GroupStockpile, "Stockpile", "Stockpile_On", "Stockpile_Off");
        }

        void Register(StatusDel singleDel, GroupStatusDel groupDel, string actionId1, string actionId2 = null, string actionId3 = null, string actionId4 = null, string actionId5 = null, string actionId6 = null)
        {
            if(singleDel != null)
            {
                Processor.AddStatus(typeof(MyObjectBuilder_GasTank), singleDel, actionId1, actionId2, actionId3, actionId4, actionId5, actionId6);
                Processor.AddStatus(typeof(MyObjectBuilder_OxygenTank), singleDel, actionId1, actionId2, actionId3, actionId4, actionId5, actionId6);
            }

            if(groupDel != null)
            {
                Processor.AddGroupStatus(typeof(MyObjectBuilder_GasTank), groupDel, actionId1, actionId2, actionId3, actionId4, actionId5, actionId6);
                Processor.AddGroupStatus(typeof(MyObjectBuilder_OxygenTank), groupDel, actionId1, actionId2, actionId3, actionId4, actionId5, actionId6);
            }
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

        // TODO: better stockpile on/off text?
        // TODO: consistency between fillable blocks (batteries, tanks, jumpdrives) in text for single and group! (esp percentage position)

        bool Stockpile(StringBuilder sb, ToolbarItem item)
        {
            IMyGasTank tank = (IMyGasTank)item.Block;

            int filledPercent = (int)Math.Round(tank.FilledRatio * 100);
            sb.NumberCapped(filledPercent, 4).Append('%');

            sb.Append('\n');

            sb.Append(tank.Stockpile ? "Fill" : "Auto");

            return true;
        }

        bool GroupStockpile(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyGasTank>())
                return false;

            int off = 0;
            int broken = 0;
            int stockpile = 0;
            double filled = 0;
            double capacity = 0;

            foreach(IMyGasTank tank in groupData.Blocks)
            {
                if(!tank.IsFunctional)
                    broken++;

                if(!tank.Enabled)
                    off++;

                if(tank.Stockpile)
                    stockpile++;

                filled += tank.FilledRatio * tank.Capacity;
                capacity += tank.Capacity;
            }

            int total = groupData.Blocks.Count;

            Processor.AppendGroupStats(sb, broken, off);

            int filledPercent = (int)Math.Round((filled / capacity) * 100);
            sb.NumberCapped(filledPercent, 4).Append('%');

            sb.Append('\n');

            if(stockpile == total)
                sb.Append("Fill");
            else if(stockpile == 0)
                sb.Append("Auto");
            else
                sb.Append("(Mixed)");

            return true;
        }
    }
}
