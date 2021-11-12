using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Pistons : StatusOverrideBase
    {
        public Pistons(ToolbarStatusProcessor processor) : base(processor)
        {
            RegisterFor(typeof(MyObjectBuilder_PistonBase));
            RegisterFor(typeof(MyObjectBuilder_ExtendedPistonBase));
        }

        void RegisterFor(MyObjectBuilderType type)
        {
            Processor.AddStatus(type, Attached, "Attach", "Detach", "Add Top Part");
            Processor.AddStatus(type, Reverse, "Reverse", "Extend", "Retract");

            Processor.AddGroupStatus(type, GroupAttached, "Attach", "Detach", "Add Top Part");
            Processor.AddGroupStatus(type, GroupReverse, "Reverse", "Extend", "Retract");
        }

        bool Attached(StringBuilder sb, ToolbarItem item)
        {
            IMyPistonBase piston = (IMyPistonBase)item.Block;
            sb.Append(piston.IsAttached ? "Atached" : "Detached");
            return true;
        }

        bool Reverse(StringBuilder sb, ToolbarItem item)
        {
            IMyPistonBase piston = (IMyPistonBase)item.Block;
            float min = piston.MinLimit;
            float max = piston.MaxLimit;
            float travelRatio = (piston.CurrentPosition - min) / (max - min);

            Processor.AppendSingleStats(sb, item.Block);

            if(piston.Velocity == 0)
                sb.Append("No Vel\n");

            sb.Append((int)(travelRatio * 100)).Append("%");
            return true;
        }

        bool GroupAttached(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyPistonBase>())
                return false;

            int attached = 0;

            foreach(IMyPistonBase piston in groupData.Blocks)
            {
                if(piston.IsAttached)
                    attached++;
            }

            int total = groupData.Blocks.Count;

            if(attached == total)
            {
                sb.Append("Attached");
            }
            else if(attached == 0)
            {
                sb.Append("Detached");
            }
            else
            {
                sb.NumberCapped(attached).Append(" att\n");
                sb.NumberCapped(total - attached).Append(" det");
            }

            return true;
        }

        bool GroupReverse(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyPistonBase>())
                return false;

            int broken = 0;
            int off = 0;
            float travelAverage = 0;
            bool allCanMove = true;

            foreach(IMyPistonBase piston in groupData.Blocks)
            {
                if(!piston.IsFunctional)
                    broken++;

                if(!piston.Enabled)
                    off++;

                if(allCanMove && piston.Velocity == 0)
                    allCanMove = false;

                float min = piston.MinLimit;
                float max = piston.MaxLimit;
                float travelRatio = (piston.CurrentPosition - min) / (max - min);
                travelAverage += travelRatio;
            }

            bool shownWarnings = Processor.AppendGroupStats(sb, broken, off);

            if(!shownWarnings && !allCanMove)
                sb.Append("No Vel\n");

            if(travelAverage > 0)
                travelAverage /= groupData.Blocks.Count;

            sb.Append((int)(travelAverage * 100)).Append("%");

            return true;
        }
    }
}
