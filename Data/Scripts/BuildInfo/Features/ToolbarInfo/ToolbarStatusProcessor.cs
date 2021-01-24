﻿using System;
using System.Collections.Generic;
using System.Text;
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
        /// Is set automatically before you get this object
        /// </summary>
        public IMyCubeGrid Grid;

        /// <summary>
        /// Fills <see cref="Blocks"/> with the blocks of specified type for this toolbar item group.
        /// Returns false if no blocks were found.
        /// </summary>
        public bool GetGroupBlocks<T>() where T : class
        {
            Blocks.Clear();

            var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(Grid);
            var group = gts?.GetBlockGroupWithName(GroupName);
            group?.GetBlocksOfType<T>(Blocks);

            return (Blocks.Count > 0);
        }
    }

    public class ToolbarStatusProcessor : ModComponent
    {
        // NOTE: this is not reliable in any way to prevent the next lines from vanishing, but it'll do for now
        // NOTE: only supports English because MyLanguagesEnum is prohibited and some languages scale the text smaller or larger.
        public const int MaxLineSize = 120;

        public bool AnimFlip;

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

        public ToolbarStatusProcessor(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;
        }

        protected override void RegisterComponent()
        {
            new StatusOverride.GenericFallback(this);
            new StatusOverride.ProgrammableBlocks(this);
            new StatusOverride.Timers(this);
            new StatusOverride.Batteries(this);
            new StatusOverride.JumpDrives(this);
            new StatusOverride.GasTanks(this);
            new StatusOverride.OxygenGenerators(this);
            new StatusOverride.Weapons(this);
            new StatusOverride.Warheads(this);
            new StatusOverride.Rotors(this);
            new StatusOverride.Pistons(this);
            new StatusOverride.Connectors(this);
            new StatusOverride.Parachutes(this);
            new StatusOverride.Doors(this);

            Main.ToolbarMonitor.ToolbarPageChanged += ToolbarPageChanged;
        }

        protected override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.ToolbarMonitor.ToolbarPageChanged += ToolbarPageChanged;
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

        internal void AddGroupStatus(MyObjectBuilderType type, GroupStatusDel func, string actionId1, string actionId2 = null, string actionId3 = null, string actionId4 = null, string actionId5 = null, string actionId6 = null)
        {
            Dictionary<string, GroupStatusDel> actions;
            if(!GroupStatusOverrides.TryGetValue(type, out actions))
                GroupStatusOverrides[type] = actions = new Dictionary<string, GroupStatusDel>();

            AddTo(actions, func, actionId1, actionId2, actionId3, actionId4, actionId5, actionId6);
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

        void ToolbarPageChanged()
        {
            FullRefresh = true; // make the next update refresh all slots at once
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(tick % (Constants.TICKS_PER_SECOND / 2) == 0)
            {
                AnimFlip = !AnimFlip;
            }

            // update 1 valid slot every 3 ticks
            if(tick % 3 != 0)
                return;

            var shipController = MyAPIGateway.Session.ControlledObject as IMyShipController;
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
            int slotOffset = toolbarPage * slotsPerPage;

            for(int i = 0; i < slotsPerPage; i++)
            {
                int index = slotOffset + ((CurrentSlot + i) % slotsPerPage);
                if(index > Main.ToolbarMonitor.HighestIndexUsed)
                    continue; // not break because it can loop-around to lower values

                if(index >= ToolbarMonitor.TotalSlots)
                    continue;

                var item = Main.ToolbarMonitor.Slots[index];
                if(item.ActionWrapper == null)
                    continue; // not valid, skip

                var sb = item.StatusSB.Clear();

                bool overrideStatus = false;

                if(Main.Config.ToolbarActionStatus.Value)
                {
                    try
                    {
                        if(item.GroupName == null)
                        {
                            StatusDel func = StatusOverrides.GetValueOrDefault(item.Block.BlockDefinition.TypeId, null)?.GetValueOrDefault(item.ActionId, null);
                            if(func != null)
                                overrideStatus = func.Invoke(sb, item);

                            if(!overrideStatus)
                            {
                                func = GenericFallback?.GetValueOrDefault(item.ActionId, null);
                                if(func != null)
                                    overrideStatus = func.Invoke(sb, item);
                            }
                        }
                        else
                        {
                            GroupDataToken.GroupName = item.GroupName;
                            GroupDataToken.Grid = item.Block.CubeGrid;

                            try
                            {
                                GroupStatusDel func = GroupStatusOverrides.GetValueOrDefault(item.Block.BlockDefinition.TypeId, null)?.GetValueOrDefault(item.ActionId, null);
                                if(func != null)
                                    overrideStatus = func.Invoke(sb, item, GroupDataToken);

                                if(!overrideStatus)
                                {
                                    func = GenericGroupFallback?.GetValueOrDefault(item.ActionId, null);
                                    if(func != null)
                                        overrideStatus = func.Invoke(sb, item, GroupDataToken);
                                }
                            }
                            finally
                            {
                                GroupDataToken.Blocks.Clear();
                                GroupDataToken.Grid = null;
                            }
                        }
                    }
                    catch(Exception e)
                    {
                        Log.Error($"Error in status override :: block={item.Block.BlockDefinition.ToString()}; action={item.ActionId}; index={item.Index.ToString()}; group={item.GroupName}\n{e.ToString()}");
                        sb.Clear().Append("ERROR!\nSeeModLog");
                        overrideStatus = true;
                    }
                }

                if(!overrideStatus)
                {
                    item.ActionWrapper.AppendOriginalStatus(item.Block, sb);
                }

                if(FullRefresh)
                    continue;

                // skip this one next for next tick
                CurrentSlot = (CurrentSlot + i + 1) % ToolbarMonitor.SlotsPerPage;

                break; // found a valid slot, end loop
            }

            FullRefresh = false;
        }
    }
}