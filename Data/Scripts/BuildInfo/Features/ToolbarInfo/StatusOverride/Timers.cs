using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Timers : StatusOverrideBase
    {
        public Timers(ToolbarStatusProcessor processor) : base(processor)
        {
            var type = typeof(MyObjectBuilder_TimerBlock);

            processor.AddStatus(type, StartStop, "Start", "Stop");
            processor.AddStatus(type, Silent, "Silent");

            processor.AddGroupStatus(type, GroupStartStop, "Start", "Stop");
            processor.AddGroupStatus(type, GroupSilent, "Silent");
        }

        bool StartStop(StringBuilder sb, ToolbarItem item)
        {
            var timer = (IMyTimerBlock)item.Block;
            bool working = timer.IsWorking;

            if(working && timer.IsCountingDown)
            {
                // HACK must parse detailedInfo because there's no getter of the current time.
                string detailedInfo = timer.DetailedInfo;
                if(!string.IsNullOrEmpty(detailedInfo))
                {
                    // expected format "<Whatever language>: 00:00:00" in first line

                    int lineStartIndex = 0;

                    int endLineIndex = detailedInfo.IndexOf('\n');
                    if(endLineIndex == -1)
                        endLineIndex = detailedInfo.Length;

                    int separatorIndex = detailedInfo.IndexOf(':', lineStartIndex, endLineIndex - lineStartIndex);
                    if(separatorIndex != -1)
                    {
                        separatorIndex += 2; // move past ": "
                        separatorIndex += 3; // move past "00:"

                        if(separatorIndex < endLineIndex)
                        {
                            if(!Processor.AnimFlip)
                                sb.Append("  ");
                            else
                                sb.Append("ˇ ");

                            sb.Append(detailedInfo, separatorIndex, endLineIndex - separatorIndex);

                            if(Processor.AnimFlip)
                                sb.Append(" ˇ");
                            return true;
                        }
                    }
                }

                // fallback to vanilla status if detailinfo couldn't be parsed
                return false;
            }
            else
            {
                sb.Append(working ? "Stopped" : "Off");
                return true;
            }
        }

        bool Silent(StringBuilder sb, ToolbarItem item)
        {
            var timer = (IMyTimerBlock)item.Block;
            sb.Append(timer.Silent ? "Silent" : "Loud");
            return true;
        }

        bool GroupStartStop(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyTimerBlock>())
                return false;

            bool allOn = true;
            int counting = 0;

            foreach(IMyTimerBlock timer in groupData.Blocks)
            {
                if(allOn && !timer.IsWorking)
                    allOn = false;

                if(timer.IsCountingDown)
                    counting++;
            }

            int total = groupData.Blocks.Count;

            if(Processor.AnimFlip && !allOn)
                sb.Append("OFF!\n");

            if(counting == total)
                sb.Append("Running");
            else if(counting == 0)
                sb.Append("Stopped");
            else
                sb.Append("Mixed");

            return true;
        }

        bool GroupSilent(StringBuilder sb, ToolbarItem item, GroupData groupData)
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

            if(total == silent)
                sb.Append("All\nSilent");
            else if(silent == 0)
                sb.Append("All\nLoud");
            else
                sb.Append("Mixed");

            return true;
        }
    }
}
