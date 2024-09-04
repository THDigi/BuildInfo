using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    internal class GroupData
    {
        public readonly List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();

        /// <summary>
        /// Is set automatically before you get this object
        /// </summary>
        public string GroupName;

        /// <summary>
        /// Is set automatically before you get this object.
        /// </summary>
        public IMyBlockGroup Group;

        /// <summary>
        /// Is set automatically before you get this object
        /// </summary>
        public IMyCubeGrid Grid;

        /// <summary>
        /// Fills <see cref="Blocks"/> with the blocks of specified type for this toolbar item group.
        /// Returns false if no blocks were found.
        /// </summary>
        public bool GetGroupBlocks<T>() where T : class
        {
            if(GroupName == null)
                throw new Exception("Invalid state, GroupName is null!");

            Blocks.Clear();

            if(Group == null)
            {
                IMyGridTerminalSystem gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(Grid);
                Group = gts?.GetBlockGroupWithName(GroupName);
            }

            Group?.GetBlocksOfType<T>(Blocks);
            return (Blocks.Count > 0);
        }
    }

    public class ToolbarStatusProcessor : ModComponent
    {
        public const int MaxChars = 8; // must not be changed, the font size dictates this; if too many characters are than they visually fit, the lines will vanish (except last one).
        public const int MaxLines = 3;

        public const string CustomStatusTag = "c";
        public const int CustomTagPrefixSpaces = 4;
        public const int CustomTagPrefixSpacesNoActionIcons = 6;
        public const char AlternateSpace = '\ue13f'; // a space that's not space character to maintain line height

        public const char IconPowerOn = '\ue100';
        public const char IconPowerOff = '\ue101';
        public const char IconBroken = '\ue102';
        public const char IconGood = '\ue103';
        public const char IconBad = '\ue104';
        public const char IconAlert = '\ue105';
        //public const char IconUnk6 = '\ue106';
        //public const char IconUnk7 = '\ue107';

        public bool Enabled { get; private set; } = true;
        public string DisableReason = "(unknown)";

        public bool AnimFlip { get; private set; }

        int CurrentSlot = 0; // for calling one status callback per tick
        bool FullRefresh = false;
        long PrevShipControllerId;

        GroupData GroupDataToken = new GroupData();

        internal delegate bool StatusDel(StringBuilder sb, ToolbarItem item);
        internal delegate bool GroupStatusDel(StringBuilder sb, ToolbarItem item, GroupData groupData);

        Dictionary<MyObjectBuilderType, Dictionary<string, StatusDel>> StatusOverrides = new Dictionary<MyObjectBuilderType, Dictionary<string, StatusDel>>(MyObjectBuilderType.Comparer);
        Dictionary<MyObjectBuilderType, Dictionary<string, GroupStatusDel>> GroupStatusOverrides = new Dictionary<MyObjectBuilderType, Dictionary<string, GroupStatusDel>>(MyObjectBuilderType.Comparer);

        Dictionary<string, StatusDel> GenericFallback = new Dictionary<string, StatusDel>();
        Dictionary<string, GroupStatusDel> GenericGroupFallback = new Dictionary<string, GroupStatusDel>();

        HashSet<string> NotifiedTooLong = new HashSet<string>();

        StringBuilder StatusSB = new StringBuilder(MaxChars * 6);

        public ToolbarStatusProcessor(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            new StatusOverride.GenericFallback(this);
            new StatusOverride.ProgrammableBlocks(this);
            new StatusOverride.Timers(this);
            new StatusOverride.Batteries(this);
            new StatusOverride.JumpDrives(this);
            new StatusOverride.GasTanks(this);
            new StatusOverride.GasGenerators(this);
            new StatusOverride.Weapons(this);
            new StatusOverride.Warheads(this);
            new StatusOverride.Rotors(this);
            new StatusOverride.Pistons(this);
            new StatusOverride.Connectors(this);
            new StatusOverride.Parachutes(this);
            new StatusOverride.Doors(this);
            new StatusOverride.Thrusters(this);
            new StatusOverride.TargetSearchShared(this);
            new StatusOverride.CombatBase(this);
            new StatusOverride.LaserAntennas(this);

            Main.ToolbarMonitor.ToolbarPageChanged += ToolbarPageChanged;
            Main.EquipmentMonitor.ControlledChanged += EquipmentMonitor_ControlledChanged;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.ToolbarMonitor.ToolbarPageChanged -= ToolbarPageChanged;
            Main.EquipmentMonitor.ControlledChanged -= EquipmentMonitor_ControlledChanged;
        }

        #region Registering methods
        internal void AddFallback(StatusDel func, string actionId1, string actionId2 = null, string actionId3 = null, string actionId4 = null, string actionId5 = null, string actionId6 = null)
        {
            AddTo(GenericFallback, func, actionId1, actionId2, actionId3, actionId4, actionId5, actionId6);
        }

        internal void AddGroupFallback(GroupStatusDel func, string actionId1, string actionId2 = null, string actionId3 = null, string actionId4 = null, string actionId5 = null, string actionId6 = null)
        {
            AddTo(GenericGroupFallback, func, actionId1, actionId2, actionId3, actionId4, actionId5, actionId6);
        }

        internal void AddStatus(MyObjectBuilderType type, StatusDel func, string actionId1, string actionId2 = null, string actionId3 = null, string actionId4 = null, string actionId5 = null, string actionId6 = null)
        {
            Dictionary<string, StatusDel> actions;
            if(!StatusOverrides.TryGetValue(type, out actions))
                StatusOverrides[type] = actions = new Dictionary<string, StatusDel>();

            AddTo(actions, func, actionId1, actionId2, actionId3, actionId4, actionId5, actionId6);
        }

        internal void AddStatus(MyObjectBuilderType type, StatusDel func, params string[] actionIds)
        {
            Dictionary<string, StatusDel> actions;
            if(!StatusOverrides.TryGetValue(type, out actions))
                StatusOverrides[type] = actions = new Dictionary<string, StatusDel>();

            foreach(string actionId in actionIds)
            {
                actions.Add(actionId, func);
            }
        }

        internal void AddGroupStatus(MyObjectBuilderType type, GroupStatusDel func, string actionId1, string actionId2 = null, string actionId3 = null, string actionId4 = null, string actionId5 = null, string actionId6 = null)
        {
            Dictionary<string, GroupStatusDel> actions;
            if(!GroupStatusOverrides.TryGetValue(type, out actions))
                GroupStatusOverrides[type] = actions = new Dictionary<string, GroupStatusDel>();

            AddTo(actions, func, actionId1, actionId2, actionId3, actionId4, actionId5, actionId6);
        }

        internal void AddGroupStatus(MyObjectBuilderType type, GroupStatusDel func, params string[] actionIds)
        {
            Dictionary<string, GroupStatusDel> actions;
            if(!GroupStatusOverrides.TryGetValue(type, out actions))
                GroupStatusOverrides[type] = actions = new Dictionary<string, GroupStatusDel>();

            foreach(string actionId in actionIds)
            {
                actions.Add(actionId, func);
            }
        }

        void AddTo<T>(Dictionary<string, T> actions, T func, string actionId1, string actionId2, string actionId3, string actionId4, string actionId5, string actionId6)
        {
            if(actionId1 != null) actions.Add(actionId1, func);
            if(actionId2 != null) actions.Add(actionId2, func);
            if(actionId3 != null) actions.Add(actionId3, func);
            if(actionId4 != null) actions.Add(actionId4, func);
            if(actionId5 != null) actions.Add(actionId5, func);
            if(actionId6 != null) actions.Add(actionId6, func);
        }
        #endregion Registering methods

        /// <summary>
        /// Appends intermitent OFF if block is turned off.
        /// Returns true if it was appended.
        /// </summary>
        public bool AppendSingleStats(StringBuilder sb, IMyTerminalBlock block)
        {
            if(!AnimFlip)
                return false;

            // doesn't need IsFunctional, slot is grayed out in that case

            IMyFunctionalBlock toggleable = block as IMyFunctionalBlock;
            if(toggleable != null && !toggleable.Enabled)
            {
                sb.Append(IconPowerOff).Append("OFF\n");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if it appended something but only for half a second as these are blinking.
        /// </summary>
        public bool AppendGroupStats(StringBuilder sb, int broken, int off)
        {
            if(!AnimFlip)
                return false;

            if(broken > 0)
            {
                sb.NumberCapped(broken, MaxChars - 4).Append(IconBroken).Append("DMG\n");
                return true;
            }
            else if(off > 0)
            {
                sb.NumberCapped(off, MaxChars - 4).Append(IconPowerOff).Append("OFF\n");
                return true;
            }

            return false;
        }

        void ToolbarPageChanged()
        {
            FullRefresh = true; // make the next update refresh all slots at once
        }

        void EquipmentMonitor_ControlledChanged(VRage.Game.ModAPI.Interfaces.IMyControllableEntity controlled)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, controlled is IMyShipController);
        }

        public override void UpdateAfterSim(int tick)
        {
            // quick prototyping of text scale
            //if(MyAPIGateway.Input.IsAnyCtrlKeyPressed())
            //{
            //    int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
            //    if(scroll != 0)
            //    {
            //        if(MyAPIGateway.Input.IsAnyShiftKeyPressed())
            //            Main.Config.ToolbarStatusTextScaleOverride.Value = (float)Dev.GetValueScroll("textscale", Main.Config.ToolbarStatusTextScaleOverride.Value, 0.001, notifyTime: 16);
            //        else
            //            Main.Config.ToolbarStatusTextScaleOverride.Value = (float)Dev.GetValueScroll("textscale", Main.Config.ToolbarStatusTextScaleOverride.Value, 0.01, notifyTime: 16);
            //    }
            //    else
            //        MyAPIGateway.Utilities.ShowNotification($"textscale = {Main.Config.ToolbarStatusTextScaleOverride.Value:0.######}", 16);
            //}

            if(tick % (Constants.TicksPerSecond / 2) == 0)
            {
                AnimFlip = !AnimFlip;
            }

            bool refreshTriggered = (!FullRefresh && tick == Main.ToolbarMonitor.TriggeredAtTick);
            if(!refreshTriggered && tick % 2 != 0)
                return;

            IMyShipController shipController = MyAPIGateway.Session.ControlledObject as IMyShipController;
            if(shipController == null)
                return;

            if(PrevShipControllerId != shipController.EntityId)
            {
                FullRefresh = true;
                PrevShipControllerId = shipController.EntityId;
            }

            bool gamepadHUD = ToolbarMonitor.EnableGamepadSupport && MyAPIGateway.Input.IsJoystickLastUsed;
            if(gamepadHUD && Main.ToolbarMonitor.GamepadToolbarPage > 20)
                return;

            int toolbarPage = (gamepadHUD ? Main.ToolbarMonitor.GamepadToolbarPage : Main.ToolbarMonitor.ToolbarPage);
            int slotsPerPage = (gamepadHUD ? ToolbarMonitor.SlotsPerPageGamepad : ToolbarMonitor.SlotsPerPage);
            int pageOffset = toolbarPage * slotsPerPage;

            for(int s = 0; s < slotsPerPage; s++)
            {
                int index;
                if(refreshTriggered)
                {
                    index = Main.ToolbarMonitor.TriggeredIndex;
                    refreshTriggered = false;
                }
                else
                {
                    index = pageOffset + ((CurrentSlot + s) % slotsPerPage);
                }

                if(index > Main.ToolbarMonitor.HighestIndexUsed)
                    continue; // not break because it can loop-around to lower values

                if(index >= ToolbarMonitor.TotalSlots)
                    continue;

                ToolbarItem item = Main.ToolbarMonitor.Slots[index];
                if(item.ActionWrapper == null)
                    continue; // not valid, skip

                StringBuilder sb = item.StatusSB.Clear();

                bool overrideStatus = false;

                if(Enabled && Main.Config.ToolbarActionStatus.Value && Main.Config.ToolbarStatusFontOverride.Value)
                {
                    try
                    {
                        StatusSB.Clear();

                        if(item.GroupId == null)
                        {
                            StatusDel func = StatusOverrides.GetValueOrDefault(item.Block.BlockDefinition.TypeId, null)?.GetValueOrDefault(item.ActionId, null);
                            if(func != null)
                                overrideStatus = func.Invoke(StatusSB, item);

                            if(!overrideStatus)
                            {
                                func = GenericFallback?.GetValueOrDefault(item.ActionId, null);
                                if(func != null)
                                    overrideStatus = func.Invoke(StatusSB, item);
                            }
                        }
                        else
                        {
                            GroupDataToken.Grid = item.Block.CubeGrid;
                            GroupDataToken.Group = item.Group;
                            GroupDataToken.GroupName = item.GroupId;

                            try
                            {
                                GroupStatusDel func = GroupStatusOverrides.GetValueOrDefault(item.Block.BlockDefinition.TypeId, null)?.GetValueOrDefault(item.ActionId, null);
                                if(func != null)
                                    overrideStatus = func.Invoke(StatusSB, item, GroupDataToken);

                                if(!overrideStatus)
                                {
                                    func = GenericGroupFallback?.GetValueOrDefault(item.ActionId, null);
                                    if(func != null)
                                        overrideStatus = func.Invoke(StatusSB, item, GroupDataToken);
                                }
                            }
                            finally
                            {
                                GroupDataToken.Blocks.Clear();
                                GroupDataToken.Grid = null;
                                GroupDataToken.Group = null;
                                GroupDataToken.GroupName = null;
                            }
                        }

                        // tag custom statuses with something
                        if(overrideStatus)
                        {
                            // need to know how many lines have been appended to keep the tag at a certain height
                            int lines = 0;
                            for(int i = 0; i < StatusSB.Length; ++i)
                            {
                                char chr = StatusSB[i];
                                if(chr == '\n')
                                    lines++;
                            }

                            int emptyLines = MaxLines - lines;
                            if(emptyLines > 0)
                            {
                                int startIdx = sb.Length;

                                if(Main.Config.ToolbarActionIcons.ValueEnum == Config.ActionIconsMode.Hidden)
                                    sb.Append(AlternateSpace, CustomTagPrefixSpacesNoActionIcons);
                                else
                                    sb.Append(AlternateSpace, CustomTagPrefixSpaces);

                                sb.Append(CustomStatusTag);

                                int leftovers = MaxChars - (sb.Length - startIdx);
                                if(leftovers > 0)
                                    sb.Append(AlternateSpace, leftovers);

                                sb.Append('\n');

                                for(int i = 1; i < (MaxLines - lines); i++)
                                {
                                    sb.Append(AlternateSpace).Append('\n');
                                }
                            }
                            //else
                            //{
                            //    sb.Append(AlternateSpace, MaxChars).Append('\n');
                            //}

                            if(emptyLines < 0 && BuildInfoMod.IsDevMod)
                            {
                                Log.Error($"[DEV] {(item.GroupId == null ? "Single" : "Group")} status for '{item.ActionId}' has too many lines={lines.ToString()} / {MaxLines.ToString()}; \n{StatusSB.ToString().Replace("\n", "\\ ")}", Log.PRINT_MESSAGE);
                            }

                            {
                                int line = 0;
                                int lineLen = 0;
                                int lineStartIdx = 0;

                                // right-align stuff
                                for(int i = 0; i < StatusSB.Length; i++)
                                {
                                    char c = StatusSB[i];
                                    if(c == '\n')
                                    {
                                        int spacing = MaxChars - (i - lineStartIdx);
                                        if(spacing > 0)
                                            sb.Append(AlternateSpace, spacing);

                                        int maxAppend = Math.Min(i, lineStartIdx + MaxChars);
                                        for(int j = lineStartIdx; j < maxAppend; j++)
                                        {
                                            sb.Append(StatusSB[j]);
                                        }

                                        sb.Append(c);

                                        lineStartIdx = i + 1;
                                        lineLen = 0;
                                        line++;
                                        continue;
                                    }
                                    else
                                    {
                                        lineLen++;
                                    }

                                    if(lineLen == MaxChars + 1 && NotifiedTooLong.Add(item.ActionId))
                                    {
                                        Log.Error($"{(item.GroupId == null ? "Single" : "Group")} status for '{item.ActionId}' has >{MaxChars} chars on line {line}; full status:\n{StatusSB}", Log.PRINT_MESSAGE);
                                    }
                                }

                                {
                                    int maxLen = StatusSB.Length;
                                    int spacing = MaxChars - (maxLen - lineStartIdx);
                                    if(spacing > 0)
                                        sb.Append(' ', spacing);

                                    for(int j = lineStartIdx; j < maxLen; j++)
                                    {
                                        sb.Append(StatusSB[j]);
                                    }
                                }
                            }
                        }
                    }
                    catch(Exception e)
                    {
                        Log.Error($"Error in status override :: block={item.Block.BlockDefinition.ToString()}; action={item.ActionId}; index={item.Index.ToString()}; group={item.GroupId}\n{e.ToString()}");
                        sb.Clear().Append(IconBad).Append("Mod\nError\nSee Log");
                        overrideStatus = true;
                    }
                }

                if(!overrideStatus)
                {
                    sb.Clear(); // erase any partial appends
                    item.ActionWrapper.AppendOriginalStatus(item.Block, sb);
                }

                if(FullRefresh)
                    continue;

                // skip this one next for next tick
                CurrentSlot = (CurrentSlot + s + 1) % ToolbarMonitor.SlotsPerPage;

                break; // found a valid slot, end loop
            }

            FullRefresh = false;
        }
    }
}
