using System.Collections.Generic;
using System.Linq;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using static Digi.BuildInfo.Features.ToolbarInfo.ToolbarStatusProcessor;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class TargetSearchShared : StatusOverrideBase
    {
        public TargetSearchShared(ToolbarStatusProcessor processor) : base(processor)
        {
            StatusDel cycleFunc = CycleTarget;
            GroupStatusDel groupCycleFunc = GroupCycleTarget;

            foreach(MyTargetingGroupDefinition def in Processor.Main.Caches.OrderedTargetGroups)
            {
                // turrets, searchlight, CTC
                RegisterFor($"TargetingGroup_{def.Id.SubtypeName}", cycleFunc, groupCycleFunc);

                // used by combat blocks, MySearchEnemyComponent.CreateTerminalControls_CombatBlockTargetGroup()
                RegisterFor($"SetTargetingGroup_{def.Id.SubtypeName}", cycleFunc, groupCycleFunc);
            }

            RegisterFor("TargetingGroup_CycleSubsystems", cycleFunc, groupCycleFunc);

            //RegisterFor("FocusLockedTarget", FocusLocked, GroupFocusLocked);
        }

        void RegisterFor(string actionId, StatusDel func, GroupStatusDel funcGroup)
        {
            RegisterFor(typeof(MyObjectBuilder_InteriorTurret), actionId, func, funcGroup);
            RegisterFor(typeof(MyObjectBuilder_LargeGatlingTurret), actionId, func, funcGroup);
            RegisterFor(typeof(MyObjectBuilder_LargeMissileTurret), actionId, func, funcGroup);
            RegisterFor(typeof(MyObjectBuilder_TurretControlBlock), actionId, func, funcGroup);
            RegisterFor(typeof(MyObjectBuilder_Searchlight), actionId, func, funcGroup);
            RegisterFor(typeof(MyObjectBuilder_OffensiveCombatBlock), actionId, func, funcGroup);
            RegisterFor(typeof(MyObjectBuilder_DefensiveCombatBlock), actionId, func, funcGroup);
        }

        void RegisterFor(MyObjectBuilderType obType, string actionId, StatusDel func, GroupStatusDel funcGroup)
        {
            if(func != null)
                Processor.AddStatus(obType, func, actionId);

            if(funcGroup != null)
                Processor.AddGroupStatus(obType, funcGroup, actionId);
        }

        //bool FocusLocked(StringBuilder sb, ToolbarItem item)
        //{
        //    if(BuildInfoMod.Instance.CoreSystemsAPIHandler.Weapons.ContainsKey(item.Block.BlockDefinition))
        //        return false;

        //    Processor.AppendSingleStats(sb, item.Block);

        //    return AppendTargetInfo(sb);
        //}

        //bool GroupFocusLocked(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        //{
        //    if(BuildInfoMod.Instance.CoreSystemsAPIHandler.Weapons.ContainsKey(groupToolbarItem.Block.BlockDefinition))
        //        return false;

        //    if(!groupData.GetGroupBlocks<IMyFunctionalBlock>())
        //        return false;

        //    int off = 0;
        //    int broken = 0;

        //    foreach(IMyFunctionalBlock fb in groupData.Blocks)
        //    {
        //        if(!fb.IsFunctional)
        //            broken++;

        //        if(!fb.Enabled)
        //            off++;
        //    }

        //    Processor.AppendGroupStats(sb, broken, off);

        //    return AppendTargetInfo(sb);
        //}

        // TODO: needs to show turret's target

        //bool AppendTargetInfo(StringBuilder sb)
        //{
        //    MyTargetLockingComponent targetLockComp = MyAPIGateway.Session?.Player?.Character?.Components?.Get<MyTargetLockingComponent>();
        //    if(targetLockComp == null)
        //        return false;

        //    if(targetLockComp.Target == null)
        //    {
        //        sb.Append("NoTarget");
        //    }
        //    else if(!targetLockComp.IsTargetLocked)
        //    {
        //        sb.Append("Wait...");
        //    }
        //    else
        //    {
        //        long gridOwner = targetLockComp.Target.BigOwners.FirstOrDefault();
        //        IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(gridOwner);

        //        if(faction != null)
        //            sb.AppendMaxLength(faction.Tag, 4, addDots: false).Append('\n');
        //        else
        //            sb.Append("NoAffil");

        //        sb.AppendMaxLength(targetLockComp.Target.DisplayName, MaxChars, addDots: false);

        //        sb.TrimEndWhitespace();
        //    }

        //    return true;
        //}

        bool CycleTarget(StringBuilder sb, ToolbarItem item)
        {
            if(BuildInfoMod.Instance.CoreSystemsAPIHandler.Weapons.ContainsKey(item.Block.BlockDefinition))
                return false;

            string targetGroup = GetTargetGroupSubtypeId(item.Block);
            if(targetGroup == null)
                return false;

            AppendTargetGroupName(sb, targetGroup);
            return true;
        }

        bool GroupCycleTarget(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(BuildInfoMod.Instance.CoreSystemsAPIHandler.Weapons.ContainsKey(groupToolbarItem.Block.BlockDefinition))
                return false;

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
                AppendTargetGroupName(sb, targetGroup);
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

        void AppendTargetGroupName(StringBuilder sb, string subtypeId)
        {
            if(string.IsNullOrEmpty(subtypeId))
            {
                //sb.AppendMaxLength(MyTexts.GetString(MySpaceTexts.BlockPropertyItem_TargetOptions_Default), MaxChars, addDots: false);
                sb.Append("Default");
                return;
            }

            switch(subtypeId)
            {
                case "Weapons": sb.Append("Weapons"); return;
                case "Propulsion": sb.Append("Thrust"); return;
                case "PowerSystems": sb.Append("Power"); return;
            }

            MyTargetingGroupDefinition def;
            if(Processor.Main.Caches.TargetGroups.TryGetValue(subtypeId, out def))
            {
                sb.AppendMaxLength(def.DisplayNameText, MaxChars, addDots: false);
                return;
            }

            sb.AppendMaxLength(subtypeId, MaxChars, addDots: false);
        }
    }
}
