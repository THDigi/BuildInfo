using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Library.Utils;
using VRage.ObjectBuilders;
using VRageMath;
using DoorStatus = Sandbox.ModAPI.Ingame.DoorStatus;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Doors : StatusOverrideBase
    {
        public Doors(ToolbarStatusProcessor processor) : base(processor)
        {
            RegisterFor(typeof(MyObjectBuilder_DoorBase));
            RegisterFor(typeof(MyObjectBuilder_Door));
            RegisterFor(typeof(MyObjectBuilder_AdvancedDoor));
            RegisterFor(typeof(MyObjectBuilder_AirtightDoorGeneric));
            RegisterFor(typeof(MyObjectBuilder_AirtightHangarDoor));
            RegisterFor(typeof(MyObjectBuilder_AirtightSlideDoor));
        }

        void RegisterFor(MyObjectBuilderType type)
        {
            Processor.AddStatus(type, Open, "Open", "Open_On", "Open_Off");

            Processor.AddGroupStatus(type, GroupOpen, "Open", "Open_On", "Open_Off");
        }

        bool Open(StringBuilder sb, ToolbarItem item)
        {
            IMyDoor door = (IMyDoor)item.Block;

            Processor.AppendSingleStats(sb, item.Block);

            // TODO: does AdvancedDoor need special treatment for OpenRatio?

            switch(door.Status)
            {
                case DoorStatus.Opening:
                {
                    float ratio = MathHelper.Clamp(door.OpenRatio, 0f, 1f);
                    sb.Append("Opening\n");
                    sb.Append((int)(ratio * 100)).Append('%');
                    break;
                }

                case DoorStatus.Closing:
                {
                    float ratio = 1f - MathHelper.Clamp(door.OpenRatio, 0f, 1f);
                    sb.Append("Closing\n");
                    sb.Append((int)(ratio * 100)).Append('%');
                    break;
                }

                case DoorStatus.Open: sb.Append("Open"); break;
                case DoorStatus.Closed: sb.Append("Closed"); break;
                default: sb.AppendMaxLength(MyEnum<DoorStatus>.GetName(door.Status), MaxChars, addDots: false); break;
            }

            return true;
        }

        bool GroupOpen(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyDoor>())
                return false;

            int broken = 0;
            int off = 0;
            int open = 0;
            int closed = 0;
            int opening = 0;
            int closing = 0;

            float averageOpenRatio = 0;

            foreach(IMyDoor door in groupData.Blocks)
            {
                if(!door.IsFunctional)
                    broken++;

                if(!door.Enabled)
                    off++;

                switch(door.Status)
                {
                    case DoorStatus.Open: open++; break;
                    case DoorStatus.Closed: closed++; break;

                    case DoorStatus.Opening:
                        averageOpenRatio += MathHelper.Clamp(door.OpenRatio, 0f, 1f);
                        opening++;
                        break;

                    case DoorStatus.Closing:
                        averageOpenRatio += MathHelper.Clamp(door.OpenRatio, 0f, 1f);
                        closing++;
                        break;
                }
            }

            Processor.AppendGroupStats(sb, broken, off);

            int doors = groupData.Blocks.Count;
            int moving = (closing + opening);

            if(moving == 0)
            {
                if(closed > 0 && open > 0)
                {
                    sb.NumberCapped(open, MaxChars - 4).Append("open\n");
                    sb.NumberCapped(closed, MaxChars - 5).Append("close");
                    return true;
                }
                else if(open > 0)
                {
                    sb.Append("All:\nopen");
                    return true;
                }
                else if(closed > 0)
                {
                    sb.Append("All:\nclosed");
                    return true;
                }
            }
            else
            {
                averageOpenRatio /= moving;

                if(closing == 0 && opening > 0)
                {
                    sb.Append("Opening\n");
                    sb.Append((int)(averageOpenRatio * 100)).Append('%');
                    return true;
                }
                else if(opening == 0 && closing > 0)
                {
                    sb.Append("Closing\n");
                    sb.Append((int)((1 - averageOpenRatio) * 100)).Append('%');
                    return true;
                }
            }

            sb.NumberCapped(opening, 3).Append('/').NumberCappedSpaced(opening + open, 3).Append("o\n");
            sb.NumberCapped(closing, 3).Append('/').NumberCappedSpaced(closing + closed, 3).Append("c");

            return true;
        }
    }
}
