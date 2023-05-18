using System;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Entity;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class GasGenerators : StatusOverrideBase
    {
        public GasGenerators(ToolbarStatusProcessor processor) : base(processor)
        {
            Type type = typeof(MyObjectBuilder_OxygenGenerator);

            processor.AddStatus(type, Refill, "Refill");
            processor.AddGroupStatus(type, GroupRefill, "Refill");

            // TODO: some status for "Auto-Refill" toggle too?
        }

        bool Refill(StringBuilder sb, ToolbarItem item)
        {
            IMyGasGenerator generator = (IMyGasGenerator)item.Block;

            MyFixedPoint ice = 0;
            int emptyBottles = 0;
            int fullBottles = 0;

            MyInventory inv = generator.GetInventory(0) as MyInventory;
            if(inv != null)
            {
                foreach(MyPhysicalInventoryItem physItem in inv.GetItems())
                {
                    MyObjectBuilder_GasContainerObject bottleOB = physItem.Content as MyObjectBuilder_GasContainerObject;

                    // HACK: according to game's internals, anything non-bottle is fuel.
                    if(bottleOB == null)
                    {
                        ice += physItem.Amount;
                    }
                    else
                    {
                        if(bottleOB.GasLevel < 1f)
                            emptyBottles += (int)physItem.Amount;
                        else
                            fullBottles += (int)physItem.Amount;
                    }
                }
            }

            if(!Processor.AppendSingleStats(sb, generator) && inv != null)
            {
                if(ice < (inv.MaxVolume * 0.1f))
                    sb.Append(IconAlert).Append("LowIce\n");
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
            if(!groupData.GetGroupBlocks<IMyGasGenerator>())
                return false;

            int off = 0;
            int broken = 0;
            int haveIce = 0;
            int emptyBottles = 0;

            foreach(IMyGasGenerator generator in groupData.Blocks)
            {
                if(!generator.IsFunctional)
                    broken++;

                if(!generator.Enabled)
                    off++;

                MyInventory inv = generator.GetInventory(0) as MyInventory;
                if(inv == null)
                    continue;

                bool hasIce = false;

                foreach(MyPhysicalInventoryItem physItem in inv.GetItems())
                {
                    MyObjectBuilder_GasContainerObject bottleOB = physItem.Content as MyObjectBuilder_GasContainerObject;

                    // HACK: according to game's internals, anything non-bottle is fuel.
                    if(bottleOB == null)
                    {
                        hasIce = true;
                    }
                    else
                    {
                        if(bottleOB.GasLevel < 1f)
                            emptyBottles += (int)physItem.Amount;
                    }
                }

                if(hasIce)
                    haveIce++;
            }

            int total = groupData.Blocks.Count;

            if(haveIce == total)
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
            else if(haveIce == 0)
            {
                Processor.AppendGroupStats(sb, broken, off);

                sb.Append("All:\n");
                sb.Append("No Ice");
            }
            else
            {
                if(!Processor.AppendGroupStats(sb, broken, off))
                    sb.Append("(Mixed)\n");

                int noIce = total - haveIce;
                sb.NumberCappedSpaced((int)noIce, MaxChars - 5).Append("NoIce\n");
                sb.NumberCappedSpaced((int)emptyBottles, MaxChars - 5).Append("Bottl"); // typo on purpose to have 3 spaces for "99+" numbers
            }

            return true;
        }
    }
}
