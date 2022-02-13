using System.Collections.Generic;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class TurretBase : StatusOverrideBase
    {
        public TurretBase(ToolbarStatusProcessor processor) : base(processor)
        {
            List<MyObjectBuilderType> blockTypes = new List<MyObjectBuilderType>()
            {
                typeof(MyObjectBuilder_InteriorTurret),
                typeof(MyObjectBuilder_LargeGatlingTurret),
                typeof(MyObjectBuilder_LargeMissileTurret),
                typeof(MyObjectBuilder_TurretControlBlock),
                typeof(MyObjectBuilder_Searchlight),
            };

            ToolbarStatusProcessor.StatusDel cycleTargetFunc = CycleTarget;
            ToolbarStatusProcessor.GroupStatusDel groupCycleTargetFunc = GroupCycleTarget;

            foreach(MyObjectBuilderType type in blockTypes)
            {
                Processor.AddStatus(type, cycleTargetFunc, "TargetingGroup_CycleSubsystems");
                Processor.AddGroupStatus(type, groupCycleTargetFunc, "TargetingGroup_CycleSubsystems");
            }

            foreach(MyTargetingGroupDefinition def in Processor.Main.Caches.OrderedTargetGroups)
            {
                string actionId = $"TargetingGroup_{def.Id.SubtypeName}";

                foreach(MyObjectBuilderType type in blockTypes)
                {
                    Processor.AddStatus(type, cycleTargetFunc, actionId);
                    Processor.AddGroupStatus(type, groupCycleTargetFunc, actionId);
                }
            }
        }

        bool CycleTarget(StringBuilder sb, ToolbarItem item)
        {
            string targetGroup = GetTargetGroupSubtypeId(item.Block);
            if(targetGroup == null)
                return false;

            sb.Append(GetTargetGroupName(targetGroup));

            return true;
        }

        readonly HashSet<string> TempTargetting = new HashSet<string>();
        bool GroupCycleTarget(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyTerminalBlock>())
                return false;

            TempTargetting.Clear();

            foreach(IMyTerminalBlock tb in groupData.Blocks)
            {
                string targetGroup = GetTargetGroupSubtypeId(tb);
                if(targetGroup == null)
                    continue;

                TempTargetting.Add(targetGroup);
            }

            if(TempTargetting.Count == 0)
            {
                sb.Append("Unknown");
            }
            else if(TempTargetting.Count > 1)
            {
                sb.Append("Mixed");
            }
            else
            {
                sb.Append("All\n");

                string targetGroup = TempTargetting.FirstElement();

                sb.Append(GetTargetGroupName(targetGroup));
            }

            return true;
        }

        /// <summary>
        /// Returns subtypeId for current targeting group, "" if it's default, and null if it's not a supported block or has invalid group.
        /// </summary>
        string GetTargetGroupSubtypeId(IMyTerminalBlock block)
        {
            //{
            //    IMyLargeTurretBase turret = block as IMyLargeTurretBase;
            //    if(turret != null)
            //    {
            //        return turret.GetTargetingGroup() ?? "";
            //    }
            //}
            //{
            //    IMyTurretControlBlock turretControl = block as IMyTurretControlBlock;
            //    if(turretControl != null)
            //    {
            //        return turretControl.GetTargetingGroup() ?? "";
            //    }
            //}

            ITerminalProperty<long> prop = block.GetProperty("TargetingGroup_Selector")?.As<long>();
            if(prop != null)
            {
                int groupIdx = (int)prop.GetValue(block); // 0 is default, 1+ is group index+1
                groupIdx -= 1; // turn default to -1

                if(groupIdx == -1)
                    return "";

                List<MyTargetingGroupDefinition> targetGroups = Processor.Main.Caches.OrderedTargetGroups;
                if(groupIdx < 0 || groupIdx >= targetGroups.Count) // unknown state
                    return null;

                return targetGroups[groupIdx].Id.SubtypeName;
            }

            return null;
        }

        string GetTargetGroupName(string subtypeId)
        {
            if(subtypeId == null)
                return "Unknown";

            if(string.IsNullOrEmpty(subtypeId))
                return MyTexts.GetString(MySpaceTexts.BlockPropertyItem_TargetOptions_Default);

            MyTargetingGroupDefinition def;
            if(Processor.Main.Caches.TargetGroups.TryGetValue(subtypeId, out def))
            {
                return def.DisplayNameText;
            }

            return subtypeId;
        }
    }
}
