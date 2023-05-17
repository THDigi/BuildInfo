using System.Collections.Generic;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage;
using VRage.ObjectBuilders;
using static Digi.BuildInfo.Features.ToolbarInfo.ToolbarStatusProcessor;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class TargetSearchShared : StatusOverrideBase
    {
        struct IdInfo
        {
            public readonly string Id;
            public readonly StatusDel Func;
            public readonly GroupStatusDel GroupFunc;

            public IdInfo(string id, StatusDel func = null, GroupStatusDel groupFunc = null)
            {
                Id = id;
                Func = func;
                GroupFunc = groupFunc;
            }
        }

        public TargetSearchShared(ToolbarStatusProcessor processor) : base(processor)
        {
            List<MyObjectBuilderType> blockTypes = new List<MyObjectBuilderType>(8)
            {
                typeof(MyObjectBuilder_InteriorTurret),
                typeof(MyObjectBuilder_LargeGatlingTurret),
                typeof(MyObjectBuilder_LargeMissileTurret),
                typeof(MyObjectBuilder_TurretControlBlock),
                typeof(MyObjectBuilder_Searchlight),
                typeof(MyObjectBuilder_OffensiveCombatBlock),
                typeof(MyObjectBuilder_DefensiveCombatBlock),
            };

            StatusDel func = CycleTarget;
            GroupStatusDel funcGroup = GroupCycleTarget;

            List<IdInfo> ids = new List<IdInfo>(8);

            foreach(MyTargetingGroupDefinition def in Processor.Main.Caches.OrderedTargetGroups)
            {
                // turrets, searchlight, CTC
                ids.Add(new IdInfo($"TargetingGroup_{def.Id.SubtypeName}", func, funcGroup));

                // used by combat blocks, MySearchEnemyComponent.CreateTerminalControls_CombatBlockTargetGroup()
                ids.Add(new IdInfo($"SetTargetingGroup_{def.Id.SubtypeName}", func, funcGroup));
            }

            ids.Add(new IdInfo("TargetingGroup_CycleSubsystems", func, funcGroup));

            foreach(IdInfo info in ids)
            {
                foreach(MyObjectBuilderType type in blockTypes)
                {
                    if(info.Func != null)
                        Processor.AddStatus(type, info.Func, info.Id);

                    if(info.GroupFunc != null)
                        Processor.AddGroupStatus(type, info.GroupFunc, info.Id);
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

        bool GroupCycleTarget(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyTerminalBlock>())
                return false;

            TempUniqueString.Clear();

            foreach(IMyTerminalBlock tb in groupData.Blocks)
            {
                string targetGroup = GetTargetGroupSubtypeId(tb);
                if(targetGroup != null)
                    TempUniqueString.Add(targetGroup);
            }

            if(TempUniqueString.Count == 0)
            {
                sb.Append("Unknown");
            }
            else if(TempUniqueString.Count > 1)
            {
                sb.Append("Mixed");
            }
            else // only one in set, meaning all of them are using the same value
            {
                sb.Append("All:\n");

                string targetGroup = TempUniqueString.FirstElement();

                sb.Append(GetTargetGroupName(targetGroup));
            }

            return true;
        }

        /// <summary>
        /// Returns subtypeId for current targeting group, "" if it's default, and null if it's not a supported block or has invalid group.
        /// </summary>
        string GetTargetGroupSubtypeId(IMyTerminalBlock block)
        {
            // turrets, searchlight, CTC
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

            // combat blocks
            foreach(var comp in block.Components)
            {
                IMySearchEnemyComponent searchComp = comp as IMySearchEnemyComponent;
                if(searchComp != null)
                {
                    return searchComp.SubsystemsToDestroy.String;
                }
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
