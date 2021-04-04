using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using MyJumpDriveStatus = Sandbox.ModAPI.Ingame.MyJumpDriveStatus;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class JumpDrives : StatusOverrideBase
    {
        public JumpDrives(ToolbarStatusProcessor processor) : base(processor)
        {
            var type = typeof(MyObjectBuilder_TimerBlock);

            processor.AddStatus(type, Recharge, "Jump", "Recharge", "Recharge_On", "Recharge_Off");
            processor.AddStatus(type, JumpDistance, "IncreaseJumpDistance", "DecreaseJumpDistance");

            processor.AddGroupStatus(type, GroupRecharge, "Recharge", "Recharge_On", "Recharge_Off");
        }

        bool Recharge(StringBuilder sb, ToolbarItem item)
        {
            var jd = (IMyJumpDrive)item.Block;
            bool jumpAction = (item.ActionId == "Jump");

            if(jumpAction)
            {
                float countdown = BuildInfoMod.Instance.JumpDriveMonitor.GetJumpCountdown(jd.CubeGrid.EntityId);
                if(countdown > 0)
                {
                    sb.Append("Jumping\n");
                    sb.TimeFormat(countdown);
                    return true;
                }
            }

            Processor.AppendSingleStats(sb, item.Block);

            switch(jd.Status)
            {
                case MyJumpDriveStatus.Charging:
                {
                    var recharge = jd.GetValueBool("Recharge");
                    sb.Append(recharge ? "Charge" : "Stop");
                    break;
                }
                case MyJumpDriveStatus.Ready: sb.Append("Ready"); break;
                case MyJumpDriveStatus.Jumping: sb.Append("Jump..."); break;
                default: return false;
            }

            sb.Append('\n');

            int filledPercent = (int)((jd.CurrentStoredPower / jd.MaxStoredPower) * 100);
            sb.Append(filledPercent).Append("% ");

            var sink = jd.Components.Get<MyResourceSinkComponent>();
            if(sink != null)
            {
                const float RatioOfMaxForDoubleArrows = 0.9f;

                float input = sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
                float maxInput = sink.MaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId);
                bool highFlow = (input > (maxInput * RatioOfMaxForDoubleArrows));

                if(input > 0)
                    sb.Append(highFlow ? "++" : "+   ");
                else
                    sb.Append("     ");
            }

            return true;
        }

        bool JumpDistance(StringBuilder sb, ToolbarItem item)
        {
            var jd = (IMyJumpDrive)item.Block;

            if(Processor.AnimFlip)
            {
                // HACK: JumpDistance slider is disabled if a GPS target is selected.
                var prop = jd.GetProperty("JumpDistance") as IMyTerminalControlSlider;
                if(prop != null && !prop.Enabled.Invoke(jd))
                {
                    sb.Append("GPS!\n");
                }
            }

            if(item.ActionWrapper.OriginalWriter != null)
            {
                int startIndex = sb.Length;

                // vanilla writer but with some alterations as it's easier than re-doing the entire math for jump distance.
                item.ActionWrapper.OriginalWriter.Invoke(jd, sb);

                for(int i = startIndex; i < sb.Length; i++)
                {
                    char c = sb[i];
                    if(c == '%' && sb.Length > (i + 2))
                    {
                        sb[i + 2] = '\n'; // replace starting paranthesis with newline
                        sb.Length -= 1; // remove ending paranthesis
                        return true;
                    }
                }
            }

            return false;
        }

        bool GroupRecharge(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyJumpDrive>())
                return false;

            int broken = 0;
            int off = 0;
            float averageFilled = 0f;
            float input = 0f;
            float maxInput = 0f;
            int ready = 0;
            int charge = 0;

            foreach(IMyJumpDrive jd in groupData.Blocks)
            {
                if(!jd.IsFunctional)
                    broken++;

                if(!jd.Enabled)
                    off++;

                averageFilled += (jd.CurrentStoredPower / jd.MaxStoredPower);

                var sink = jd.Components.Get<MyResourceSinkComponent>();
                if(sink != null)
                {
                    input += sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
                    maxInput += sink.MaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId);
                }

                switch(jd.Status)
                {
                    case MyJumpDriveStatus.Ready: ready++; break;
                    case MyJumpDriveStatus.Charging: charge++; break;
                }
            }

            int total = groupData.Blocks.Count;

            Processor.AppendGroupStats(sb, broken, off);

            if(averageFilled > 0)
            {
                averageFilled /= total;
                averageFilled *= 100;
            }

            float averageInput = 0;
            if(input > 0)
                averageInput = input / total;

            if(ready == total)
                sb.Append("Ready\n");
            else if(charge == total)
                sb.Append("Charge\n");
            else
                sb.Append("(Mixed)\n");

            sb.Append((int)averageFilled).Append("% ");

            const float RatioOfMaxForDoubleArrows = 0.9f;
            bool highFlow = (input > (maxInput * RatioOfMaxForDoubleArrows));

            if(input > 0)
                sb.Append(highFlow ? "++" : "+   ");
            else
                sb.Append("     ");

            return true;
        }
    }
}
