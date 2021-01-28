using System.Text;
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
            var piston = (IMyPistonBase)item.Block;
            sb.Append(piston.IsAttached ? "Atached" : "Detached");
            return true;
        }

        bool Reverse(StringBuilder sb, ToolbarItem item)
        {
            var piston = (IMyPistonBase)item.Block;
            float min = piston.MinLimit;
            float max = piston.MaxLimit;
            float travelRatio = (piston.CurrentPosition - min) / (max - min);

            if(Processor.AnimFlip && !piston.IsWorking)
                sb.Append("OFF!\n");

            if(!Processor.AnimFlip && piston.Velocity == 0)
                sb.Append("No Vel\n");

            sb.Append((int)(travelRatio * 100)).Append("%");
            return true;
        }

        bool GroupAttached(StringBuilder sb, ToolbarItem item, GroupData groupData)
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

            if(attached < total)
            {
                sb.Append("Attached:\n").Append(attached).Append(" / ").Append(total);
            }
            else if(attached == total)
            {
                sb.Append("All\nattached");
            }
            else
            {
                sb.Append("All\ndetached");
            }

            return true;
        }

        bool GroupReverse(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyPistonBase>())
                return false;

            float travelAverage = 0;
            bool allOn = true;
            bool allCanMove = true;

            foreach(IMyPistonBase piston in groupData.Blocks)
            {
                if(allOn && !piston.IsWorking)
                    allOn = false;

                if(allCanMove && piston.Velocity == 0)
                    allCanMove = false;

                float min = piston.MinLimit;
                float max = piston.MaxLimit;
                float travelRatio = (piston.CurrentPosition - min) / (max - min);
                travelAverage += travelRatio;
            }

            if(travelAverage > 0)
                travelAverage /= groupData.Blocks.Count;

            if(Processor.AnimFlip && !allOn)
                sb.Append("OFF!\n");

            if(!Processor.AnimFlip && !allCanMove)
                sb.Append("No Vel\n");

            sb.Append((int)(travelAverage * 100)).Append("%");

            return true;
        }
    }
}
