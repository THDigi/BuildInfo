﻿using System;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Thrusters : StatusOverrideBase
    {
        public Thrusters(ToolbarStatusProcessor processor) : base(processor)
        {
            Type type = typeof(MyObjectBuilder_Thrust);

            Processor.AddStatus(type, Override, "IncreaseOverride", "DecreaseOverride");

            Processor.AddGroupStatus(type, GroupOverride, "IncreaseOverride", "DecreaseOverride");
        }

        bool Override(StringBuilder sb, ToolbarItem item)
        {
            IMyThrust thrust = (IMyThrust)item.Block;

            Processor.AppendSingleStats(sb, item.Block);

            sb.Append(MathHelper.Clamp((int)(thrust.ThrustOverridePercentage * 100), 0, 100)).Append("%");
            return true;
        }

        bool GroupOverride(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
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

            sb.Append(MathHelper.Clamp((int)(averagePercentage * 100), 0, 100)).Append("%");

            return true;
        }
    }
}
