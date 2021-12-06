using System;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class GenericFallback : StatusOverrideBase
    {
        public GenericFallback(ToolbarStatusProcessor processor) : base(processor)
        {
            ToolbarStatusProcessor.StatusDel funcShowIn = new ToolbarStatusProcessor.StatusDel(ShowIn);

            processor.AddFallback(OnOff, "OnOff", "OnOff_On", "OnOff_Off");
            processor.AddFallback(UseConveyor, "UseConveyor");
            processor.AddFallback(funcShowIn, "ShowOnHUD", "ShowOnHUD_On", "ShowOnHUD_Off");
            // these don't seem to have actions
            //processor.AddFallback(funcShowIn, "ShowInTerminal", "ShowInTerminal_On", "ShowInTerminal_Off");
            //processor.AddFallback(funcShowIn, "ShowInInventory", "ShowInInventory_On", "ShowInInventory_Off");
            //processor.AddFallback(funcShowIn, "ShowInToolbarConfig", "ShowInToolbarConfig_On", "ShowInToolbarConfig_Off");

            processor.AddGroupFallback(GroupOnOff, "OnOff", "OnOff_On", "OnOff_Off");
            processor.AddGroupFallback(GroupUseConveyor, "UseConveyor");
        }

        bool UseConveyor(StringBuilder sb, ToolbarItem item)
        {
            bool useConveyor = item.Block.GetValue<bool>("UseConveyor"); // TODO: replace when GetProperty() no longer necessary
            sb.Append(useConveyor ? "Share" : "Isolate");
            return true;
        }

        bool OnOff(StringBuilder sb, ToolbarItem item)
        {
            IMyFunctionalBlock block = item.Block as IMyFunctionalBlock;
            if(block == null)
                return false;

            // to differentiate at a glance between block on/off and other toggle.
            sb.Append("Turned\n").Append(block.Enabled ? "ON" : "OFF");
            return true;
        }

        bool ShowIn(StringBuilder sb, ToolbarItem item)
        {
            sb.Append(item.Block.ShowOnHUD ? "Shown" : "Hidden");
            return true;
        }

        bool GroupOnOff(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyFunctionalBlock>())
                return false;

            int on = 0;

            foreach(IMyFunctionalBlock b in groupData.Blocks)
            {
                if(b.Enabled)
                    on++;
            }

            int total = groupData.Blocks.Count;

            if(on == total)
            {
                sb.Append("All on");
            }
            else if(on == 0)
            {
                sb.Append("All off");
            }
            else
            {
                sb.NumberCapped(on).Append(" on\n");
                sb.NumberCapped(total - on).Append(" off");
            }

            return true;
        }

        bool GroupUseConveyor(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyTerminalBlock>())
                return false;

            int useConveyor = 0;

            foreach(IMyTerminalBlock b in groupData.Blocks)
            {
                ITerminalProperty prop = b.GetProperty("UseConveyor"); // TODO: replace when GetProperty() no longer necessary
                if(prop != null)
                {
                    if(prop.AsBool().GetValue(b))
                        useConveyor++;
                }
            }

            int total = groupData.Blocks.Count;
            int noConveyor = (total - useConveyor);

            if(useConveyor == total)
            {
                sb.Append("All share");
            }
            else if(noConveyor == total)
            {
                sb.Append("All isolate");
            }
            else
            {
                sb.NumberCapped(noConveyor).Append(" iso\n");
                sb.NumberCapped(useConveyor).Append(" share"); // NOTE: this line is too wide with "99+ share", must be last.
            }

            return true;
        }
    }
}
