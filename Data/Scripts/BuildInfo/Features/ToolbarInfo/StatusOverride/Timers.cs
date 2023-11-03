using System;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Localization;
using SpaceEngineers.Game.ModAPI;
using VRage;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Timers : StatusOverrideBase
    {
        public Timers(ToolbarStatusProcessor processor) : base(processor)
        {
            Type type = typeof(MyObjectBuilder_TimerBlock);

            processor.AddStatus(type, StartStop, "Start", "Stop");
            processor.AddStatus(type, Silent, "Silent");

            processor.AddGroupStatus(type, GroupStartStop, "Start", "Stop");
            processor.AddGroupStatus(type, GroupSilent, "Silent");
        }

        bool StartStop(StringBuilder sb, ToolbarItem item)
        {
            IMyTimerBlock timer = (IMyTimerBlock)item.Block;

            Processor.AppendSingleStats(sb, item.Block);

            if(!timer.IsCountingDown)
            {
                sb.Append("Stopped");
                return true;
            }

            // HACK: must parse detailedInfo because there's no getter of the current time.
            string detailedInfo = timer.DetailedInfo;
            if(string.IsNullOrEmpty(detailedInfo))
                return false;

            // expected format "<Whatever language>: [Dd ]00:00:00" as in MyValueFormatter.AppendTimeExact()

            string labelPrefix = MyTexts.GetString(MySpaceTexts.BlockPropertyTitle_TimerToTrigger);

            int startIndex = detailedInfo.IndexOf(labelPrefix);
            if(startIndex == -1)
                return false;

            startIndex += labelPrefix.Length; // move past the label prefix

            int endIndex = detailedInfo.IndexOf('\n', startIndex);
            if(endIndex == -1)
                endIndex = detailedInfo.Length;

            int days = 0;
            int daysIndex = detailedInfo.IndexOf("d ", startIndex);
            if(daysIndex != -1 && daysIndex < endIndex)
            {
                string daysString = detailedInfo.Substring(startIndex, daysIndex - startIndex);

                if(!int.TryParse(daysString, out days))
                {
                    //Log.Error($"can't parse days: '{daysString}' - entire string: '{detailedInfo}'");
                    return false;
                }

                startIndex = daysIndex + 2; // move past "d "
            }

            if(days > 0)
                sb.NumberCapped(days, MaxChars - 4).Append("d ");

            if(detailedInfo.IndexOf(':', startIndex) != startIndex + 2)
            {
                //Log.Error($"Unexpected format! expected ':' at 3rd char in '{detailedInfo.Substring(startIndex, endIndex - startIndex)}'; entire string: '{detailedInfo}'");
                return false;
            }

            string hoursString = detailedInfo.Substring(startIndex, 2);
            int hours;
            if(!int.TryParse(hoursString, out hours))
            {
                //Log.Error($"can't parse hours: '{hoursString}' - entire string: '{detailedInfo}'");
                return false;
            }

            if(days > 0 || hours > 0)
                sb.Append(hours).Append("h\n");

            startIndex += 3; // move past "00:"

            bool working = timer.IsWorking;
            if(working && Processor.AnimFlip)
                sb.Append("ˇ");
            else
                sb.Append(" ");

            sb.Append(detailedInfo, startIndex, endIndex - startIndex);

            if(working && Processor.AnimFlip)
                sb.Append("ˇ");
            else
                sb.Append(" ");

            return true;
        }

        bool Silent(StringBuilder sb, ToolbarItem item)
        {
            IMyTimerBlock timer = (IMyTimerBlock)item.Block;
            sb.Append(timer.Silent ? "Silent" : "Loud");
            return true;
        }

        bool GroupStartStop(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyTimerBlock>())
                return false;

            int broken = 0;
            int off = 0;
            int counting = 0;

            foreach(IMyTimerBlock timer in groupData.Blocks)
            {
                if(!timer.IsFunctional)
                    broken++;

                if(!timer.Enabled)
                    off++;

                if(timer.IsWorking && timer.IsCountingDown)
                    counting++;
            }

            int total = groupData.Blocks.Count;

            Processor.AppendGroupStats(sb, broken, off);

            if(counting == total)
            {
                sb.Append("All run");
            }
            else if(counting == 0)
            {
                sb.Append("All stop");
            }
            else
            {
                sb.NumberCappedSpaced(counting, MaxChars - 3).Append("run\n");
                sb.NumberCappedSpaced(total - counting, MaxChars - 4).Append("stop");
            }

            return true;
        }

        bool GroupSilent(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyTimerBlock>())
                return false;

            int silent = 0;

            foreach(IMyTimerBlock timer in groupData.Blocks)
            {
                if(timer.Silent)
                    silent++;
            }

            int total = groupData.Blocks.Count;

            if(silent == total)
            {
                sb.Append("All:\nSilent");
            }
            else if(silent == 0)
            {
                sb.Append("All:\nLoud");
            }
            else
            {
                sb.NumberCappedSpaced(silent, MaxChars - 5).Append("quiet\n");
                sb.NumberCappedSpaced(total - silent, MaxChars - 4).Append("loud");
            }

            return true;
        }
    }
}
