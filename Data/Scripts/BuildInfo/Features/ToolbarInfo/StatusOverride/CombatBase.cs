using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage;
using VRage.Library.Utils;
using VRage.ObjectBuilders;
using MyGridTargetingRelationFiltering = VRage.Game.ModAPI.Ingame.MyGridTargetingRelationFiltering;
using OffensiveCombatTargetPriority = VRage.Game.ModAPI.Ingame.OffensiveCombatTargetPriority;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class CombatBase : StatusOverrideBase
    {
        public CombatBase(ToolbarStatusProcessor processor) : base(processor)
        {
            {
                ToolbarStatusProcessor.StatusDel func = AttackMode;
                ToolbarStatusProcessor.GroupStatusDel groupFunc = GroupAttackMode;

                List<MyObjectBuilderType> blockTypes = new List<MyObjectBuilderType>()
                {
                    typeof(MyObjectBuilder_OffensiveCombatBlock),
                    typeof(MyObjectBuilder_DefensiveCombatBlock),
                };

                foreach(MyObjectBuilderType type in blockTypes)
                {
                    Processor.AddStatus(type, func, "SetAttackMode_EnemiesOnly");
                    Processor.AddStatus(type, func, "SetAttackMode_EnemiesAndNeutrals");

                    Processor.AddGroupStatus(type, groupFunc, "SetAttackMode_EnemiesOnly");
                    Processor.AddGroupStatus(type, groupFunc, "SetAttackMode_EnemiesAndNeutrals");
                }
            }

            {
                MyObjectBuilderType type = typeof(MyObjectBuilder_OffensiveCombatBlock);

                ToolbarStatusProcessor.StatusDel func = TargetPriority;
                ToolbarStatusProcessor.GroupStatusDel groupFunc = GroupTargetPriority;

                Processor.AddStatus(type, func, "SetTargetPriority_Closest");
                Processor.AddStatus(type, func, "SetTargetPriority_Smallest");
                Processor.AddStatus(type, func, "SetTargetPriority_Largest");

                Processor.AddGroupStatus(type, groupFunc, "SetTargetPriority_Closest");
                Processor.AddGroupStatus(type, groupFunc, "SetTargetPriority_Smallest");
                Processor.AddGroupStatus(type, groupFunc, "SetTargetPriority_Largest");
            }
        }

        #region Attack Mode
        bool AttackMode(StringBuilder sb, ToolbarItem item)
        {
            AttackModeEnum mode = GetAttackMode(item.Block);
            switch(mode)
            {
                case AttackModeEnum.Enemies: sb.Append("Enemies"); return true;
                case AttackModeEnum.EnemiesAndNeutrals: sb.Append("Enemy&\nNeutral"); return true;
            }

            return false;
        }

        bool GroupAttackMode(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyTerminalBlock>())
                return false;

            TempUniqueInt.Clear();

            foreach(IMyTerminalBlock tb in groupData.Blocks)
            {
                AttackModeEnum mode = GetAttackMode(tb);
                if(mode != AttackModeEnum.Unknown)
                {
                    TempUniqueInt.Add((int)mode);
                }
            }

            if(TempUniqueInt.Count == 0)
            {
                sb.Append("Unknown");
            }
            else if(TempUniqueInt.Count > 1)
            {
                sb.Append("Mixed");
            }
            else // only one in list, meaning all blocks have same mode
            {
                sb.Append("All:\n");

                AttackModeEnum mode = (AttackModeEnum)TempUniqueInt.FirstElement();
                switch(mode)
                {
                    case AttackModeEnum.Enemies: sb.Append("Enemies"); return true;
                    case AttackModeEnum.EnemiesAndNeutrals: sb.Append("Enemy&\nNeutral"); return true;
                }
            }

            return true;
        }

        enum AttackModeEnum { Unknown, Enemies, EnemiesAndNeutrals }

        static AttackModeEnum GetAttackMode(IMyTerminalBlock block)
        {
            foreach(var comp in block.Components)
            {
                IMySearchEnemyComponent searchComp = comp as IMySearchEnemyComponent;
                if(searchComp != null)
                {
                    MyGridTargetingRelationFiltering targetting = searchComp.TargetingLockOptions;

                    // same values as in MySearchEnemyComponent.Translate() and FillTargetLockList()
                    switch(targetting)
                    {
                        case MyGridTargetingRelationFiltering.Enemy:
                            return AttackModeEnum.Enemies;

                        case MyGridTargetingRelationFiltering.Enemy | MyGridTargetingRelationFiltering.Neutral:
                            return AttackModeEnum.EnemiesAndNeutrals;

                        default:
                            return AttackModeEnum.Unknown;
                    }
                }
            }

            return AttackModeEnum.Unknown;
        }
        #endregion

        #region Target priority
        bool TargetPriority(StringBuilder sb, ToolbarItem item)
        {
            IMyOffensiveCombatBlock oc = item.Block as IMyOffensiveCombatBlock;
            if(oc != null)
            {
                AppendTargetPriorityName(sb, oc.TargetPriority);
                return true;
            }

            return false;
        }

        bool GroupTargetPriority(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyOffensiveCombatBlock>())
                return false;

            TempUniqueInt.Clear();

            foreach(IMyOffensiveCombatBlock oc in groupData.Blocks)
            {
                TempUniqueInt.Add((int)oc.TargetPriority);
            }

            if(TempUniqueInt.Count == 0)
            {
                sb.Append("Unknown");
            }
            else if(TempUniqueInt.Count > 1)
            {
                sb.Append("Mixed");
            }
            else // only one in set, meaning all of them are using the same value
            {
                sb.Append("All:\n");
                AppendTargetPriorityName(sb, (OffensiveCombatTargetPriority)TempUniqueInt.FirstElement());
            }

            return true;
        }

        static void AppendTargetPriorityName(StringBuilder sb, OffensiveCombatTargetPriority value)
        {
            //string enumName = MyEnum<OffensiveCombatTargetPriority>.GetName(value);
            //string langKey = "TargetPriority_" + enumName;
            //string translated = MyTexts.GetString(langKey);

            //if(translated == langKey)
            //    return enumName;
            //else
            //    return translated;

            sb.AppendMaxLength(MyEnum<OffensiveCombatTargetPriority>.GetName(value), MaxChars, addDots: false);
        }
        #endregion
    }
}
