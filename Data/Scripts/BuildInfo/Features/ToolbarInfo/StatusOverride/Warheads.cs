using System;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Warheads : StatusOverrideBase
    {
        public Warheads(ToolbarStatusProcessor processor) : base(processor)
        {
            var type = typeof(MyObjectBuilder_Warhead);

            processor.AddStatus(type, Countdown, "IncreaseDetonationTime", "DecreaseDetonationTime", "StartCountdown", "StopCountdown");
            processor.AddStatus(type, Safety, "Safety", "Detonate");

            processor.AddGroupStatus(type, GroupSafety, "Safety", "Detonate");
        }

        bool Countdown(StringBuilder sb, ToolbarItem item)
        {
            var warhead = (IMyWarhead)item.Block;
            var span = TimeSpan.FromSeconds(warhead.DetonationTime);
            int minutes = span.Minutes;

            if(span.Hours > 0)
                minutes += span.Hours * 60;

            bool blink = (warhead.IsCountingDown && Processor.AnimFlip);
            sb.Append(blink ? "ˇ " : "  ");
            sb.Append(minutes.ToString("00")).Append(':').Append(span.Seconds.ToString("00"));
            sb.Append(blink ? " ˇ" : "  ");

            return true;
        }

        bool Safety(StringBuilder sb, ToolbarItem item)
        {
            var warhead = (IMyWarhead)item.Block;
            if(warhead.IsArmed)
            {
                bool isTrigger = (item.ActionId == "Detonate");
                sb.Append(Processor.AnimFlip ? "! " : "  ");
                sb.Append(isTrigger ? "BOOM" : "Armed");
                sb.Append(Processor.AnimFlip ? " !" : "  ");
            }
            else
            {
                sb.Append("Safe");
            }
            return true;
        }

        bool GroupSafety(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyWarhead>())
                return false;

            int armed = 0;

            foreach(IMyWarhead warhead in groupData.Blocks)
            {
                if(warhead.IsArmed)
                    armed++;
            }

            int total = groupData.Blocks.Count;

            bool isTrigger = (item.ActionId == "Detonate");

            if(armed == total)
            {
                sb.Append("All\n");

                sb.Append(Processor.AnimFlip ? "!" : " ");
                sb.Append(isTrigger ? "EXPLODE" : "Armed");

                if(Processor.AnimFlip)
                    sb.Append("!");
            }
            else if(armed == 0)
            {
                sb.Append("All\nSafe");
            }
            else
            {
                sb.Append(Processor.AnimFlip ? "!!!\n" : "");
                sb.Append("SAF:").Append(total - armed);
                sb.Append("\nARM:").Append(armed);
            }

            return true;
        }
    }
}
