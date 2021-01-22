using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class GenericFallback : StatusOverrideBase
    {
        public GenericFallback(ToolbarStatusProcessor processor) : base(processor)
        {
            processor.AddFallback(UseConveyor, "UseConveyor");

            processor.AddGroupFallback(GroupOnOff, "OnOff", "OnOff_On", "OnOff_Off");
            processor.AddGroupFallback(GroupUseConveyor, "UseConveyor");
        }

        bool UseConveyor(StringBuilder sb, ToolbarItem item)
        {
            bool useConveyor = item.Block.GetValueBool("UseConveyor"); // TODO: replace when GetProperty() no longer necessary
            sb.Append(useConveyor ? "Share" : "Isolate");
            return true;
        }

        bool GroupOnOff(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyFunctionalBlock>())
                return false;

            int on = 0;

            foreach(IMyFunctionalBlock b in groupData.Blocks)
            {
                if(b.Enabled)
                    on++;
            }

            int off = (groupData.Blocks.Count - on);
            bool tooMany = (groupData.Blocks.Count) > 99;

            if(off == 0)
            {
                if(tooMany)
                    sb.Append("All On");
                else
                    sb.Append("All\n").Append(on).Append(" On");
            }
            else if(on == 0)
            {
                if(tooMany)
                    sb.Append("All Off");
                else
                    sb.Append("All\n").Append(off).Append(" Off");
            }
            else
            {
                if(tooMany)
                    sb.Append("Mixed");
                else
                    sb.Append(on).Append(" On\n").Append(off).Append(" Off");
            }

            return true;
        }

        bool GroupUseConveyor(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyFunctionalBlock>())
                return false;

            int useConveyor = 0;

            foreach(IMyTerminalBlock b in groupData.Blocks)
            {
                var prop = b.GetProperty("UseConveyor"); // TODO: replace when GetProperty() no longer necessary
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
                sb.Append("All\nShared");
            }
            else if(noConveyor == total)
            {
                sb.Append("All\nIsolate");
            }
            else
            {
                sb.Append(useConveyor).Append(" shared\n");
                sb.Append(noConveyor).Append(" isolate");
            }

            return true;
        }
    }
}
