using System;
using System.Collections.Generic;
using System.Text;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.ObjectBuilders;
using DoorStatus = Sandbox.ModAPI.Ingame.DoorStatus;
using MyShipConnectorStatus = Sandbox.ModAPI.Ingame.MyShipConnectorStatus;

// TODO: group combined status?

namespace Digi.BuildInfo.Features.ToolbarLabels
{
    public class ToolbarActionStatus : ModComponent
    {
        public const int CacheLiveTicks = Constants.TICKS_PER_SECOND * 1;

        public delegate bool StatusDel(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb);

        public StatusDel GetCustomStatus(MyObjectBuilderType blockType)
        {
            return customStatus.GetValueOrDefault(blockType, null);
        }

        private readonly Dictionary<MyObjectBuilderType, StatusDel> customStatus = new Dictionary<MyObjectBuilderType, StatusDel>(MyObjectBuilderType.Comparer);

        private readonly Dictionary<int, string> cachedStatusText = new Dictionary<int, string>();

        private long previousShipController;

        public ToolbarActionStatus(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;
        }

        protected override void RegisterComponent()
        {
            InitCustomStatus();

            MyVisualScriptLogicProvider.PlayerEnteredCockpit += EnteredCockpit;
        }

        protected override void UnregisterComponent()
        {
            MyVisualScriptLogicProvider.PlayerEnteredCockpit -= EnteredCockpit;
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(tick % CacheLiveTicks == 0)
            {
                cachedStatusText.Clear();
            }
        }

        void EnteredCockpit(string entityName, long playerId, string gridName)
        {
            try
            {
                var player = MyAPIGateway.Session?.Player;
                if(player != null && player.IdentityId == playerId)
                {
                    var controlled = MyAPIGateway.Session.ControlledObject as IMyShipController;

                    if(controlled != null && previousShipController != controlled.EntityId)
                    {
                        previousShipController = controlled.EntityId;
                        cachedStatusText.Clear(); // clear cache when entering new cockpit as they no longer are relevant, since they're per slot index
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        /// <summary>
        /// Generic status that is only used after specific status callbacks return false or don't exist.
        /// </summary>
        public bool Status_GenericFallback(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupName != null)
            {
                switch(actionLabel.Action.Id)
                {
                    case "OnOff":
                    case "OnOff_On":
                    case "OnOff_Off":
                    {
                        // this is just a short-lived cache to aleviate callback spam
                        string cachedText;
                        if(cachedStatusText.TryGetValue(toolbarItem.Index, out cachedText))
                        {
                            sb.Append(cachedText);
                            return true;
                        }

                        int startIndex = Math.Max(0, sb.Length);

                        var blocks = GetGroupBlocks(block, toolbarItem.GroupName);
                        if(blocks == null)
                            break;

                        int on = 0;
                        int off = 0;

                        foreach(var b in blocks)
                        {
                            var func = b as IMyFunctionalBlock;
                            if(func == null)
                                continue;

                            if(func.Enabled)
                                on++;
                            else
                                off++;
                        }

                        blocks.Clear();

                        if(off == 0)
                            sb.Append("All On");
                        else if(on == 0)
                            sb.Append("All Off");
                        else
                            sb.Append(on).Append(" On\n").Append(off).Append(" Off");

                        cachedStatusText.Add(toolbarItem.Index, sb.ToString(startIndex, sb.Length - startIndex));
                        return true;
                    }

                    case "Open":
                    case "Open_On":
                    case "Open_Off":
                    {
                        string cachedText;
                        if(cachedStatusText.TryGetValue(toolbarItem.Index, out cachedText))
                        {
                            sb.Append(cachedText);
                            return true;
                        }

                        int startIndex = Math.Max(0, sb.Length);

                        var blocks = GetGroupBlocks(block, toolbarItem.GroupName);
                        if(blocks == null)
                            break;

                        int open = 0;
                        int closed = 0;
                        int progress = 0;

                        foreach(var b in blocks)
                        {
                            var door = b as IMyDoor;
                            if(door == null)
                                continue;

                            switch(door.Status)
                            {
                                case DoorStatus.Open: open++; break;
                                case DoorStatus.Closed: closed++; break;

                                case DoorStatus.Opening:
                                case DoorStatus.Closing:
                                    progress++; break;
                            }
                        }

                        blocks.Clear();

                        // TODO: improve!

                        if(progress > 0)
                            sb.Append("...\n");

                        if(closed == 0)
                            sb.Append("All Open");
                        else if(open == 0)
                            sb.Append("All Closed");
                        else
                            sb.Append(open).Append(" Open\n").Append(closed).Append(" Closed");

                        cachedStatusText.Add(toolbarItem.Index, sb.ToString(startIndex, sb.Length - startIndex));
                        return true;
                    }

                    // TODO: more shared actions...
                }
            }
            else
            {
                // TODO: non-group shared actions... ?
            }

            return false;
        }

        List<IMyTerminalBlock> GroupBlocks = new List<IMyTerminalBlock>();
        private List<IMyTerminalBlock> GetGroupBlocks(IMyTerminalBlock firstBlock, string groupName)
        {
            var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(firstBlock.CubeGrid);

            var group = gts.GetBlockGroupWithName(groupName);

            if(group == null)
                return null;

            GroupBlocks.Clear();
            group.GetBlocks(GroupBlocks);

            return GroupBlocks;
        }

        void InitCustomStatus()
        {
            Add(typeof(MyObjectBuilder_MyProgrammableBlock), Status_PB);

            Add(typeof(MyObjectBuilder_TimerBlock), Status_Timer);

            var gasTank = new StatusDel(Status_GasTank);
            Add(typeof(MyObjectBuilder_GasTank), gasTank);
            Add(typeof(MyObjectBuilder_OxygenTank), gasTank);

            Add(typeof(MyObjectBuilder_OxygenGenerator), Status_OxygenGenerator);

            var weapons = new StatusDel(Status_Weapons);
            Add(typeof(MyObjectBuilder_SmallGatlingGun), weapons);
            Add(typeof(MyObjectBuilder_SmallMissileLauncher), weapons);
            Add(typeof(MyObjectBuilder_SmallMissileLauncherReload), weapons);
            Add(typeof(MyObjectBuilder_InteriorTurret), weapons);
            Add(typeof(MyObjectBuilder_LargeGatlingTurret), weapons);
            Add(typeof(MyObjectBuilder_LargeMissileTurret), weapons);

            Add(typeof(MyObjectBuilder_Warhead), Status_Warhead);

            var motors = new StatusDel(Status_MotorStator);
            Add(typeof(MyObjectBuilder_MotorStator), motors);
            Add(typeof(MyObjectBuilder_MotorAdvancedStator), motors);

            Add(typeof(MyObjectBuilder_MotorSuspension), Status_Suspension);

            var pistons = new StatusDel(Status_Piston);
            Add(typeof(MyObjectBuilder_ExtendedPistonBase), pistons);
            Add(typeof(MyObjectBuilder_PistonBase), pistons);

            Add(typeof(MyObjectBuilder_ShipConnector), Status_Connector);
        }

        void Add(MyObjectBuilderType type, StatusDel action)
        {
            customStatus.Add(type, action);
        }

        // NOTE: for groups only the first block gets the writer called, and no way to detect if it's a group.
        // TODO: one method per action ID instead for less switch() on runtime?

        bool Status_PB(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupName != null)
            {
                // TODO: group stuff?
            }

            switch(actionLabel.Action.Id)
            {
                case "Run":
                case "RunWithDefaultArgument":
                {
                    var pb = (IMyProgrammableBlock)block;

                    if(!string.IsNullOrEmpty(pb.DetailedInfo))
                    {
                        if(pb.DetailedInfo.StartsWith("Main method not found"))
                        {
                            sb.Append("ERROR:\nNo Main");
                        }
                        else if(pb.DetailedInfo.StartsWith("Assembly not found"))
                        {
                            sb.Append("ERROR:\nCompile");
                        }
                        else if(pb.DetailedInfo.Contains("Caught exception"))
                        {
                            sb.Append("ERROR:\nException");
                        }
                        else
                        {
                            // append this max amount of lines from PB detailedinfo/echo
                            const int MAX_LINES = 2;
                            int allowedLines = MAX_LINES;

                            for(int i = 0; i < pb.DetailedInfo.Length; ++i)
                            {
                                var chr = pb.DetailedInfo[i];

                                if(chr == '\n' && --allowedLines == 0)
                                    break;

                                sb.Append(chr);
                            }
                        }
                    }
                    else
                    {
                        // running or idle without any echo, nothing client can detect here
                    }

                    return true;
                }
            }

            return false;
        }

        bool Status_Timer(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Start":
                case "Stop":
                case "TriggerNow":
                {
                    int startIndex = Math.Max(0, sb.Length);
                    var timer = (IMyTimerBlock)block;
                    bool working = timer.IsWorking;

                    if(working && timer.IsCountingDown)
                    {
                        string cachedText;
                        if(cachedStatusText.TryGetValue(toolbarItem.Index, out cachedText))
                        {
                            bool blink = DateTime.UtcNow.Millisecond >= 500;
                            sb.Append(blink ? "ˇ " : "  ");
                            sb.Append(cachedText);
                            if(blink)
                                sb.Append(" ˇ");
                            return true;
                        }

                        // HACK must parse detailedInfo because there's no getter of the current time.
                        string detailedInfo = timer.DetailedInfo;

                        if(!string.IsNullOrEmpty(detailedInfo))
                        {
                            // expected format "<Whatever language>: 00:00:00" in first line

                            int endLineIndex = detailedInfo.IndexOf('\n');

                            if(endLineIndex == -1)
                                endLineIndex = detailedInfo.Length;

                            int separatorIndex = detailedInfo.IndexOf(':', 0, endLineIndex);

                            if(separatorIndex != -1)
                            {
                                separatorIndex += 2; // move past ": "
                                separatorIndex += 3; // move past "00:"

                                if(separatorIndex < detailedInfo.Length)
                                {
                                    var time = detailedInfo.Substring(separatorIndex, endLineIndex - separatorIndex);
                                    cachedStatusText.Add(toolbarItem.Index, time);

                                    // keep blinking separate so it can blink faster
                                    bool blink = DateTime.UtcNow.Millisecond >= 500;
                                    sb.Append(blink ? "ˇ " : "  ");
                                    sb.Append(time);
                                    if(blink)
                                        sb.Append(" ˇ");

                                    return true;
                                }
                            }
                        }

                        sb.Append("Running");
                        cachedStatusText.Add(toolbarItem.Index, "Running");
                    }
                    else
                    {
                        cachedStatusText.Remove(toolbarItem.Index);
                        sb.Append(working ? "Stopped" : "Off");
                    }

                    return true;
                }
            }

            return false;
        }

        bool Status_OxygenGenerator(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Refill":
                {
                    string cachedText;
                    if(cachedStatusText.TryGetValue(toolbarItem.Index, out cachedText))
                    {
                        sb.Append(cachedText);
                        return true;
                    }

                    int startIndex = Math.Max(0, sb.Length);
                    var generator = (IMyGasGenerator)block;
                    bool canFill = false;

                    if(generator.IsWorking)
                    {
                        var generatorDef = (MyOxygenGeneratorDefinition)generator.SlimBlock.BlockDefinition;
                        canFill = !(!MyAPIGateway.Session.SessionSettings.EnableOxygen && generatorDef.ProducedGases.TrueForAll((p) => p.Id == MyResourceDistributorComponent.OxygenId));
                    }

                    int itemsToFill = 0;
                    bool hasIce = false;

                    if(canFill)
                    {
                        var inv = block.GetInventory(0) as MyInventory;

                        if(inv != null)
                        {
                            foreach(var item in inv.GetItems())
                            {
                                var containerOB = item.Content as MyObjectBuilder_GasContainerObject;

                                if(containerOB == null)
                                    hasIce = true;
                                else if(containerOB.GasLevel < 1f)
                                    itemsToFill++;
                            }
                        }
                    }

                    if(hasIce && itemsToFill > 0)
                    {
                        sb.Append("Refill:").Append('\n').Append(itemsToFill);
                    }
                    else
                    {
                        sb.Append(hasIce ? "No Bottles" : "No Ice");
                    }

                    cachedStatusText.Add(toolbarItem.Index, sb.ToString(startIndex, sb.Length - startIndex));
                    return true;
                }
            }

            return false;
        }

        bool Status_GasTank(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Refill":
                {
                    string cachedText;
                    if(cachedStatusText.TryGetValue(toolbarItem.Index, out cachedText))
                    {
                        sb.Append(cachedText);
                        return true;
                    }

                    var tank = (IMyGasTank)block;

                    bool canFill = false;

                    if(tank.IsWorking)
                    {
                        var tankDef = (MyGasTankDefinition)tank.SlimBlock.BlockDefinition;
                        canFill = !(!MyAPIGateway.Session.SessionSettings.EnableOxygen && tankDef.StoredGasId == MyResourceDistributorComponent.OxygenId);
                    }

                    int itemsToFill = 0;

                    if(canFill)
                    {
                        var inv = block.GetInventory(0) as MyInventory;

                        if(inv != null)
                        {
                            foreach(var item in inv.GetItems())
                            {
                                var containerOB = item.Content as MyObjectBuilder_GasContainerObject;

                                if(containerOB != null && containerOB.GasLevel < 1f)
                                    itemsToFill++;
                            }
                        }
                    }

                    int startIndex = Math.Max(0, sb.Length);

                    if(itemsToFill > 0)
                    {
                        sb.Append("Refill:").Append('\n').Append(itemsToFill);
                    }
                    else
                    {
                        sb.Append(tank.FilledRatio > 0 ? "No Bottles" : "No Gas");
                    }

                    cachedStatusText.Add(toolbarItem.Index, sb.ToString(startIndex, sb.Length - startIndex));
                    return true;
                }
            }

            return false;
        }

        bool Status_Weapons(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "ShootOnce":
                case "Shoot":
                case "Shoot_On":
                case "Shoot_Off":
                {
                    var gun = (IMyGunObject<MyGunBase>)block;
                    sb.Append(gun.GunBase.GetTotalAmmunitionAmount().ToString());
                    return true;
                }
            }

            return false;
        }

        bool Status_Warhead(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "IncreaseDetonationTime": // these have timer but different format
                case "DecreaseDetonationTime": // ^
                case "StartCountdown":
                case "StopCountdown":
                {
                    var warhead = (IMyWarhead)block;

                    var span = TimeSpan.FromSeconds(warhead.DetonationTime);
                    int minutes = span.Minutes;

                    if(span.Hours > 0)
                        minutes += span.Hours * 60;

                    bool blink = (warhead.IsCountingDown && DateTime.UtcNow.Millisecond >= 500);
                    sb.Append(blink ? "ˇ " : "  ");

                    sb.Append(minutes.ToString("00")).Append(':').Append(span.Seconds.ToString("00"));

                    if(blink)
                        sb.Append(" ˇ");

                    return true;
                }
                case "Safety":
                {
                    var warhead = (IMyWarhead)block;
                    sb.Append(warhead.IsArmed ? "Armed" : "Safe");
                    return true;
                }
                case "Detonate":
                {
                    var warhead = (IMyWarhead)block;
                    sb.Append(warhead.IsArmed ? "Ready!" : "Safe");
                    return true;
                }
            }

            return false;
        }

        bool Status_MotorStator(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Reverse":
                {
                    var stator = (IMyMotorStator)block;
                    sb.Append(stator.TargetVelocityRPM.ToString("0.##"));
                    return true;
                }

                case "Attach":
                case "Detach":
                case "Add Top Part":
                case "Add Small Top Part":
                {
                    var stator = (IMyMotorStator)block;
                    sb.Append(stator.IsAttached ? "Atached" : "Detached");
                    return true;
                }
            }

            return false;
        }

        bool Status_Suspension(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Add Top Part":
                {
                    var susp = (IMyMotorSuspension)block;
                    sb.Append(susp.IsAttached ? "Atached" : "Detached");
                    return true;
                }
            }

            return false;
        }

        bool Status_Piston(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Extend":
                case "Retract":
                case "Reverse":
                {
                    var piston = (IMyPistonBase)block;
                    sb.Append(piston.Velocity.ToString("0.##"));
                    return true;
                }

                case "Attach":
                case "Detach":
                case "Add Top Part":
                {
                    var piston = (IMyPistonBase)block;
                    sb.Append(piston.IsAttached ? "Atached" : "Detached");
                    return true;
                }
            }

            return false;
        }

        bool Status_Connector(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Lock":
                case "Unlock":
                case "SwitchLock": // SwitchLock already has status but text is too long and clips
                {
                    var connector = (IMyShipConnector)block;

                    switch(connector.Status)
                    {
                        case MyShipConnectorStatus.Connected: sb.Append("Locked"); return true;
                        case MyShipConnectorStatus.Connectable: sb.Append("Ready"); return true;
                        case MyShipConnectorStatus.Unconnected: sb.Append("Unlocked"); return true;
                    }

                    return false; // none of the above statuses matched, just let the game handle it then
                }
            }

            return false;
        }
    }
}
