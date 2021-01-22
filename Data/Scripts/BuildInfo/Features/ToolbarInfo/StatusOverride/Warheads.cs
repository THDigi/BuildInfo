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
            processor.AddStatus(type, Countdown, "Safety");
            processor.AddStatus(type, Countdown, "Detonate");

            // TODO: group support
            //processor.AddGroupStatus(type, StatusGroup, "Run", "RunWithDefaultArgument", "", "");
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

            if(blink)
                sb.Append(" ˇ");

            return true;
        }

        bool Safety(StringBuilder sb, ToolbarItem item)
        {
            var warhead = (IMyWarhead)item.Block;
            sb.Append(warhead.IsArmed ? "Armed" : "Safe");
            return true;
        }

        bool Detonate(StringBuilder sb, ToolbarItem item)
        {
            var warhead = (IMyWarhead)item.Block;
            sb.Append(warhead.IsArmed ? "Ready!" : "Safe");
            return true;
        }

        //bool StatusGroup(StringBuilder sb, ToolbarItem item, GroupData groupData)
        //{
        //    if(!groupData.GetGroupBlocks<Status_Warhead>())
        //        return false;

        //    return true;
        //}
    }
}
