using System;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Warheads : StatusOverrideBase
    {
        public Warheads(ToolbarStatusProcessor processor) : base(processor)
        {
            Type type = typeof(MyObjectBuilder_Warhead);

            processor.AddStatus(type, Countdown, "IncreaseDetonationTime", "DecreaseDetonationTime", "StartCountdown", "StopCountdown");
            processor.AddStatus(type, Safety, "Safety", "Detonate");

            processor.AddGroupStatus(type, GroupCountdown, "IncreaseDetonationTime", "DecreaseDetonationTime", "StartCountdown", "StopCountdown");
            processor.AddGroupStatus(type, GroupSafety, "Safety", "Detonate");
        }

        bool Countdown(StringBuilder sb, ToolbarItem item)
        {
            IMyWarhead warhead = (IMyWarhead)item.Block;

            TimeSpan span = TimeSpan.FromSeconds(warhead.DetonationTime);
            int minutes = span.Minutes;
            if(span.Hours > 0)
                minutes += span.Hours * 60;

            //sb.Append(blink ? "ˇ" : " ");
            sb.Append(warhead.IsCountingDown && Processor.AnimFlip ? IconAlert : ' ');

            sb.Append(minutes.ToString("00")).Append(':').Append(span.Seconds.ToString("00"));

            sb.Append(warhead.IsCountingDown && !Processor.AnimFlip ? IconAlert : ' ');
            //sb.Append(blink ? "ˇ" : " ");

            return true;
        }

        bool GroupCountdown(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyWarhead>())
                return false;

            int countingDown = 0;
            float shortestTime = float.MaxValue;
            float longestTime = float.MinValue;

            foreach(IMyWarhead warhead in groupData.Blocks)
            {
                shortestTime = Math.Min(shortestTime, warhead.DetonationTime);
                longestTime = Math.Max(longestTime, warhead.DetonationTime);

                if(warhead.IsCountingDown)
                    countingDown++;
            }

            int total = groupData.Blocks.Count;

            TimeSpan span = TimeSpan.FromSeconds(shortestTime);
            int minutes = span.Minutes;
            if(span.Hours > 0)
                minutes += span.Hours * 60;

            bool timersEqual = Math.Abs(longestTime - shortestTime) <= 0.1f;

            if(!timersEqual || (0 < countingDown && countingDown < total))
            {
                sb.Append("(Mixed)\n");
            }

            //sb.Append(blink ? "ˇ" : " ");
            sb.Append(countingDown > 0 && Processor.AnimFlip ? IconAlert : ' ');

            sb.Append(minutes.ToString("00")).Append(':').Append(span.Seconds.ToString("00"));

            sb.Append(countingDown > 0 && !Processor.AnimFlip ? IconAlert : ' ');
            //sb.Append(blink ? "ˇ" : " ");

            return true;
        }

        bool Safety(StringBuilder sb, ToolbarItem item)
        {
            IMyWarhead warhead = (IMyWarhead)item.Block;
            if(warhead.IsArmed)
            {
                bool isTrigger = (item.ActionId == "Detonate");

                sb.Append(Processor.AnimFlip ? IconAlert : ' ');
                sb.Append(isTrigger ? "BOOM" : "Armed");
                sb.Append(Processor.AnimFlip ? ' ' : IconAlert);
            }
            else
            {
                sb.Append("Safe");
            }
            return true;
        }

        bool GroupSafety(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
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

            bool isTrigger = (groupToolbarItem.ActionId == "Detonate");

            if(armed == total)
            {
                sb.Append("All\n");

                sb.Append(Processor.AnimFlip ? IconAlert : ' ');
                sb.Append("Armed");
                sb.Append(!Processor.AnimFlip ? IconAlert : ' ');
            }
            else if(armed == 0)
            {
                sb.Append("All\nSafe");
            }
            else
            {
                if(Processor.AnimFlip)
                    sb.Append(IconAlert).Append('\n');

                sb.NumberCapped(total - armed, MaxChars - 4).Append("safe");
                sb.Append('\n');
                sb.NumberCapped(armed, MaxChars - 5).Append("armed");
            }

            return true;
        }
    }
}
