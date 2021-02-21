using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
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

            Processor.AppendSingleStats(sb, item.Block);

            if(timer.IsCountingDown)
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
                            bool working = timer.IsWorking;
                            if(working && Processor.AnimFlip)
                                sb.Append("ˇ ");
                            else
                                sb.Append("  ");

                            sb.Append(detailedInfo, separatorIndex, endLineIndex - separatorIndex);

                            if(working && Processor.AnimFlip)
                                sb.Append(" ˇ");
                            else
                                sb.Append("  ");

                            return true;
                        }
                    }
                }

                // fallback to vanilla status if detailinfo couldn't be parsed
                return false;
            }
            else
            {
                sb.Append("Stopped");
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
                sb.NumberCapped(counting).Append(" run\n");
                sb.NumberCapped(total - counting).Append(" stop");
            }

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

            if(silent == total)
            {
                sb.Append("All silent");
            }
            else if(silent == 0)
            {
                sb.Append("All loud");
            }
            else
            {
                sb.NumberCapped(silent).Append(" silent\n");
                sb.NumberCapped(total - silent).Append(" loud");
            }

            return true;
        }
    }
}
