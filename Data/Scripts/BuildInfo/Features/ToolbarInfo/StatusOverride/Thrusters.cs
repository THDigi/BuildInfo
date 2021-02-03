using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Thrusters : StatusOverrideBase
    {
        public Thrusters(ToolbarStatusProcessor processor) : base(processor)
        {
            var type = typeof(MyObjectBuilder_Thrust);

            Processor.AddStatus(type, Override, "IncreaseOverride", "DecreaseOverride");

            Processor.AddGroupStatus(type, GroupOverride, "IncreaseOverride", "DecreaseOverride");
        }

        bool Override(StringBuilder sb, ToolbarItem item)
        {
            var thrust = (IMyThrust)item.Block;

            Processor.AppendSingleStats(sb, item.Block);

            sb.Append((int)(thrust.ThrustOverridePercentage * 100)).Append(" %");
            return true;
        }

        bool GroupOverride(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyThrust>())
                return false;

            int broken = 0;
            int off = 0;
            float averagePercentage = 0;

            foreach(IMyThrust thrust in groupData.Blocks)
            {
                if(!thrust.IsFunctional)
                    broken++;

                if(!thrust.Enabled)
                    off++;

                averagePercentage += thrust.ThrustOverridePercentage;
            }

            Processor.AppendGroupStats(sb, broken, off);

            int total = groupData.Blocks.Count;

            if(averagePercentage > 0)
                averagePercentage /= total;

            sb.Append((int)(averagePercentage * 100)).Append(" %");

            return true;
        }
    }
}
