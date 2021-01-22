using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
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
            var door = (IMyDoor)item.Block;

            if(Processor.AnimFlip && !door.IsWorking)
                sb.Append("OFF!\n");

            // TODO: does AdvancedDoor need special treatment for OpenRatio?

            switch(door.Status)
            {
                case DoorStatus.Opening:
                {
                    float ratio = MathHelper.Clamp(door.OpenRatio, 0f, 1f);
                    sb.Append("Opening\n").Append((int)(ratio * 100)).Append("%");
                    break;
                }

                case DoorStatus.Closing:
                {
                    float ratio = 1f - MathHelper.Clamp(door.OpenRatio, 0f, 1f);
                    sb.Append("Closing\n").Append((int)(ratio * 100)).Append("%");
                    break;
                }

                case DoorStatus.Open: sb.Append("Open"); break;
                case DoorStatus.Closed: sb.Append("Closed"); break;
                default: return false;
            }

            return true;
        }

        bool GroupOpen(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyDoor>())
                return false;

            // TODO include off state?

            int open = 0;
            int closed = 0;
            int opening = 0;
            int closing = 0;

            float averageOpenRatio = 0;

            foreach(IMyDoor door in groupData.Blocks)
            {
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

            int doors = groupData.Blocks.Count;
            bool tooMany = (doors > 99);
            int moving = (closing + opening);

            if(moving == 0)
            {
                if(closed > 0 && open > 0)
                {
                    if(tooMany)
                        sb.Append("Mixed");
                    else
                    {
                        sb.Append(open).Append(" open\n");
                        sb.Append(closed).Append(" closed");
                    }
                    return true;
                }
                else if(open > 0)
                {
                    if(tooMany)
                        sb.Append("All\nopen");
                    else
                        sb.Append("All\n").Append(open).Append(" open");
                    return true;
                }
                else if(closed > 0)
                {
                    if(tooMany)
                        sb.Append("All\nclosed");
                    else
                        sb.Append("All\n").Append(closed).Append(" closed");
                    return true;
                }
            }
            else if(moving > 0)
            {
                averageOpenRatio /= moving;

                if(closing == 0 && opening > 0)
                {
                    sb.Append("Opening\n").Append((int)(averageOpenRatio * 100)).Append("%");
                    return true;
                }
                else if(opening == 0 && closing > 0)
                {
                    sb.Append('\n').Append("Closing\n").Append((int)((1 - averageOpenRatio) * 100)).Append("%");
                    return true;
                }
            }

            if(tooMany)
            {
                sb.Append("InPrgrs\nMixed");
            }
            else
            {
                sb.Append(opening).Append("/").Append(opening + open).Append(" o\n");
                sb.Append(closing).Append("/").Append(closing + closed).Append(" c");
            }

            return true;
        }
    }
}
