using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using DoorStatus = Sandbox.ModAPI.Ingame.DoorStatus;
using MyShipConnectorStatus = Sandbox.ModAPI.Ingame.MyShipConnectorStatus;

namespace Digi.BuildInfo.Features.ToolbarLabels
{
    public class ToolbarActionStatus : ModComponent
    {
        public const int ShortCacheLiveTicks = Constants.TICKS_PER_SECOND;
        public const int ShorterCacheLiveTicks = Constants.TICKS_PER_SECOND / 4;

        public delegate bool StatusDel(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb);

        public StatusDel GetCustomStatus(MyObjectBuilderType blockType)
        {
            return customStatus.GetValueOrDefault(blockType, null);
        }

        private readonly Dictionary<MyObjectBuilderType, StatusDel> customStatus = new Dictionary<MyObjectBuilderType, StatusDel>(MyObjectBuilderType.Comparer);

        private readonly Dictionary<int, string> shortLivedCache = new Dictionary<int, string>();
        private readonly Dictionary<int, string> shorterLivedCache = new Dictionary<int, string>();

        private readonly List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();

        private long previousShipController;

        private bool animFlip = false;
        private int animCycle = 0;
        private const int animCycleMax = 4; // exclusive

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
            if(tick % ShortCacheLiveTicks == 0)
            {
                shortLivedCache.Clear();
            }

            if(tick % ShorterCacheLiveTicks == 0)
            {
                shorterLivedCache.Clear();
            }

            if(tick % (Constants.TICKS_PER_SECOND / 2) == 0)
            {
                animFlip = !animFlip;
            }

            if(tick % (Constants.TICKS_PER_SECOND / 4) == 0)
            {
                if(++animCycle >= animCycleMax)
                    animCycle = 0;
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

                        // clear caches when entering new cockpit as they no longer are relevant, since they're per slot index
                        shortLivedCache.Clear();
                        shorterLivedCache.Clear();
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        /// <summary>
        /// Generic status that is used only if the per-type statuses return false.
        /// </summary>
        public bool Status_GenericFallback(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupNameWrapped != null)
                return Status_GenericGroupFallback(actionLabel, block, toolbarItem, sb);

            // TODO: non-group shared actions... ?

            return false;
        }

        bool Status_GenericGroupFallback(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "OnOff":
                case "OnOff_On":
                case "OnOff_Off":
                {
                    // this is just a short-lived cache to aleviate callback spam
                    string cachedText;
                    if(shortLivedCache.TryGetValue(toolbarItem.Index, out cachedText))
                    {
                        sb.Append(cachedText);
                        return true;
                    }
                    int startIndex = Math.Max(0, sb.Length);

                    blocks.Clear();
                    var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(block.CubeGrid);
                    var group = gts?.GetBlockGroupWithName(toolbarItem.GroupName);
                    group?.GetBlocksOfType<IMyFunctionalBlock>(blocks);
                    if(blocks.Count == 0)
                        return false;

                    int on = 0;
                    int off = 0;

                    foreach(IMyFunctionalBlock b in blocks)
                    {
                        if(b.Enabled)
                            on++;
                        else
                            off++;
                    }

                    blocks.Clear();

                    bool tooMany = (on + off) > 99;

                    if(off == 0)
                    {
                        if(tooMany)
                            sb.Append("All On");
                        else
                            sb.Append("All\n").Append(on).Append(" On");
                    }
                    else if(on == 0)
                    {
                        if(tooMany)
                            sb.Append("All Off");
                        else
                            sb.Append("All\n").Append(off).Append(" Off");
                    }
                    else
                    {
                        if(tooMany)
                            sb.Append("Mixed");
                        else
                            sb.Append(on).Append(" On\n").Append(off).Append(" Off");
                    }

                    shortLivedCache.Add(toolbarItem.Index, sb.ToString(startIndex, sb.Length - startIndex));
                    return true;
                }

                // TODO: more shared actions...
            }

            return false;
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

            var doors = new StatusDel(Status_Doors);
            Add(typeof(MyObjectBuilder_DoorBase), doors);
            Add(typeof(MyObjectBuilder_Door), doors);
            Add(typeof(MyObjectBuilder_AdvancedDoor), doors);
            Add(typeof(MyObjectBuilder_AirtightDoorGeneric), doors);
            Add(typeof(MyObjectBuilder_AirtightHangarDoor), doors);
            Add(typeof(MyObjectBuilder_AirtightSlideDoor), doors);

            Add(typeof(MyObjectBuilder_Parachute), Status_Parachute);
        }

        void Add(MyObjectBuilderType type, StatusDel action)
        {
            customStatus.Add(type, action);
        }

        // NOTE: for groups only the first block gets the writer called, and no way to detect if it's a group.
        // TODO: one method per action ID instead for less switch() on runtime?

        bool Status_PB(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupNameWrapped != null)
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
                        if(shortLivedCache.TryGetValue(toolbarItem.Index, out cachedText))
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
                                    shortLivedCache.Add(toolbarItem.Index, time);

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
                        shortLivedCache.Add(toolbarItem.Index, "Running");
                    }
                    else
                    {
                        shortLivedCache.Remove(toolbarItem.Index);
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
                    if(shortLivedCache.TryGetValue(toolbarItem.Index, out cachedText))
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

                    shortLivedCache.Add(toolbarItem.Index, sb.ToString(startIndex, sb.Length - startIndex));
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
                    if(shortLivedCache.TryGetValue(toolbarItem.Index, out cachedText))
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

                    shortLivedCache.Add(toolbarItem.Index, sb.ToString(startIndex, sb.Length - startIndex));
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

        #region Pistons
        bool Status_Piston(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupNameWrapped != null)
                return Status_PistonGroup(actionLabel, block, toolbarItem, sb);

            switch(actionLabel.Action.Id)
            {
                case "Extend":
                case "Retract":
                case "Reverse":
                {
                    var piston = (IMyPistonBase)block;

                    float min = piston.MinLimit;
                    float max = piston.MaxLimit;
                    float travelRatio = (piston.CurrentPosition - min) / (max - min);

                    sb.Append((int)(travelRatio * 100)).Append("%");
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

        bool Status_PistonGroup(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Extend":
                case "Retract":
                case "Reverse":
                {
                    string cachedText;
                    if(shorterLivedCache.TryGetValue(toolbarItem.Index, out cachedText))
                    {
                        sb.Append(cachedText);
                        return true;
                    }
                    int startIndex = Math.Max(0, sb.Length);

                    blocks.Clear();
                    var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(block.CubeGrid);
                    var group = gts?.GetBlockGroupWithName(toolbarItem.GroupName);
                    group?.GetBlocksOfType<IMyPistonBase>(blocks);
                    if(blocks.Count == 0)
                        return false;

                    float travelAverage = 0;
                    bool allOn = true;

                    foreach(IMyPistonBase piston in blocks)
                    {
                        if(!piston.Enabled)
                            allOn = false;

                        float min = piston.MinLimit;
                        float max = piston.MaxLimit;
                        float travelRatio = (piston.CurrentPosition - min) / (max - min);
                        travelAverage += travelRatio;
                    }

                    if(travelAverage > 0)
                        travelAverage /= blocks.Count;

                    if(!allOn && animFlip)
                        sb.Append("OFF!\n");

                    sb.Append((int)(travelAverage * 100)).Append("%");

                    shorterLivedCache.Add(toolbarItem.Index, sb.ToString(startIndex, sb.Length - startIndex));
                    return true;
                }

                case "Attach":
                case "Detach":
                case "Add Top Part":
                {
                    string cachedText;
                    if(shortLivedCache.TryGetValue(toolbarItem.Index, out cachedText))
                    {
                        sb.Append(cachedText);
                        return true;
                    }
                    int startIndex = Math.Max(0, sb.Length);

                    blocks.Clear();
                    var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(block.CubeGrid);
                    var group = gts?.GetBlockGroupWithName(toolbarItem.GroupName);
                    group?.GetBlocksOfType<IMyFunctionalBlock>(blocks);
                    if(blocks.Count == 0)
                        return false;

                    int attached = 0;

                    foreach(IMyPistonBase piston in blocks)
                    {
                        if(piston.IsAttached)
                            attached++;
                    }

                    int total = blocks.Count;

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

                    shortLivedCache.Add(toolbarItem.Index, sb.ToString(startIndex, sb.Length - startIndex));
                    return true;
                }
            }

            return false;
        }
        #endregion Pistons

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

        #region Doors
        bool Status_Doors(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupNameWrapped != null)
                return Status_DoorsGroup(actionLabel, block, toolbarItem, sb);

            // TODO: does AdvancedDoor need special treatment for OpenRatio?

            switch(actionLabel.Action.Id)
            {
                case "Open":
                case "Open_On":
                case "Open_Off":
                {
                    var door = (IMyDoor)block;
                    if(!door.Enabled && animFlip)
                        sb.Append("OFF!\n");

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
            }

            return false;
        }

        bool Status_DoorsGroup(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Open":
                case "Open_On":
                case "Open_Off":
                {
                    string cachedText;
                    if(shorterLivedCache.TryGetValue(toolbarItem.Index, out cachedText))
                    {
                        sb.Append(cachedText);
                        return true;
                    }

                    blocks.Clear();
                    var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(block.CubeGrid);
                    var group = gts?.GetBlockGroupWithName(toolbarItem.GroupName);
                    group?.GetBlocksOfType<IMyDoor>(blocks);
                    if(blocks.Count == 0)
                        return false;

                    // TODO include off state?

                    int open = 0;
                    int closed = 0;
                    int opening = 0;
                    int closing = 0;

                    float averageOpenRatio = 0;
                    int doors = 0;

                    foreach(IMyDoor door in blocks)
                    {
                        doors++;

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

                    blocks.Clear();

                    if(doors == 0)
                        return false;

                    int startIndex = Math.Max(0, sb.Length);
                    try
                    {
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
                    finally
                    {
                        shorterLivedCache.Add(toolbarItem.Index, sb.ToString(startIndex, sb.Length - startIndex));
                    }
                }
            }

            return false;
        }
        #endregion Doors

        #region Parachutes
        bool Status_Parachute(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupNameWrapped != null)
                return Status_ParachuteGroup(actionLabel, block, toolbarItem, sb);

            switch(actionLabel.Action.Id)
            {
                case "Open":
                case "Open_On":
                case "Open_Off":
                {
                    var parachute = (IMyParachute)block;
                    bool hasAmmo = true;

                    var inv = parachute.GetInventory();
                    if(inv != null)
                    {
                        var def = (MyParachuteDefinition)block.SlimBlock.BlockDefinition;
                        var foundItems = (float)inv.GetItemAmount(def.MaterialDefinitionId);
                        hasAmmo = (foundItems >= def.MaterialDeployCost);
                    }

                    if(!parachute.Enabled && animFlip)
                        sb.Append("OFF!\n");

                    if(!hasAmmo && !animFlip && parachute.Status != DoorStatus.Open)
                        sb.Append("Empty!\n");

                    switch(parachute.Status)
                    {
                        case DoorStatus.Opening: sb.Append("Deploying..."); break;
                        case DoorStatus.Closing: sb.Append("Closing..."); break;
                        case DoorStatus.Open: sb.Append("Deployed"); break;
                        case DoorStatus.Closed: sb.Append("Ready"); break;
                        default: return false;
                    }
                    return true;
                }

                case "AutoDeploy":
                {
                    var parachute = (IMyParachute)block;
                    bool autoDeploy = parachute.GetValue<bool>("AutoDeploy"); // HACK: no interface members for this

                    if(autoDeploy)
                    {
                        if(!parachute.Enabled && animFlip)
                            sb.Append("OFF!\n");
                        else
                            sb.Append("Auto\n");

                        float deployHeight = parachute.GetValue<float>("AutoDeployHeight");
                        sb.DistanceFormat(deployHeight, 1);
                    }
                    else
                    {
                        sb.Append("Manual");
                    }
                    return true;
                }
            }

            return false;
        }

        bool Status_ParachuteGroup(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Open":
                case "Open_On":
                case "Open_Off":
                {
                    string cachedText;
                    if(shorterLivedCache.TryGetValue(toolbarItem.Index, out cachedText))
                    {
                        sb.Append(cachedText);
                        return true;
                    }
                    int startIndex = Math.Max(0, sb.Length);

                    blocks.Clear();
                    var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(block.CubeGrid);
                    var group = gts?.GetBlockGroupWithName(toolbarItem.GroupName);
                    group?.GetBlocksOfType<IMyParachute>(blocks);
                    if(blocks.Count == 0)
                        return false;

                    int open = 0;
                    int ready = 0;
                    bool allAmmo = true;
                    bool allOn = true;

                    foreach(IMyParachute parachute in blocks)
                    {
                        if(parachute.Status != DoorStatus.Open)
                        {
                            var inv = parachute.GetInventory();
                            if(inv != null)
                            {
                                var def = (MyParachuteDefinition)block.SlimBlock.BlockDefinition;
                                var foundItems = (float)inv.GetItemAmount(def.MaterialDefinitionId);
                                if(foundItems < def.MaterialDeployCost)
                                    allAmmo = false;
                            }
                        }

                        if(!parachute.Enabled)
                            allOn = false;

                        switch(parachute.Status)
                        {
                            case DoorStatus.Open: open++; break;
                            case DoorStatus.Closed: ready++; break;
                        }
                    }

                    blocks.Clear();

                    if(!allAmmo && animFlip)
                        sb.Append("Empty!\n");

                    if(!allOn && !animFlip)
                        sb.Append("OFF!\n");

                    if(open > 0 && ready > 0)
                        sb.Append("Mixed");
                    else if(open > 0)
                        sb.Append("Deployed");
                    else
                        sb.Append("Ready");

                    shorterLivedCache.Add(toolbarItem.Index, sb.ToString(startIndex, sb.Length - startIndex));
                    return true;
                }

                case "AutoDeploy": // vanilla status is borked
                {
                    string cachedText;
                    if(shorterLivedCache.TryGetValue(toolbarItem.Index, out cachedText))
                    {
                        sb.Append(cachedText);
                        return true;
                    }
                    int startIndex = Math.Max(0, sb.Length);

                    blocks.Clear();
                    var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(block.CubeGrid);
                    var group = gts?.GetBlockGroupWithName(toolbarItem.GroupName);
                    group?.GetBlocksOfType<IMyParachute>(blocks);
                    if(blocks.Count == 0)
                        return false;

                    bool allOn = true;
                    int auto = 0;
                    int manual = 0;

                    bool hasSmallerDeploy = false;
                    float highestDeploy = 0;

                    foreach(IMyParachute parachute in blocks)
                    {
                        if(!parachute.Enabled)
                            allOn = false;

                        bool autoDeploy = parachute.GetValue<bool>("AutoDeploy");
                        if(autoDeploy)
                        {
                            auto++;

                            float deployHeight = parachute.GetValue<float>("AutoDeployHeight");
                            if(deployHeight > highestDeploy)
                            {
                                if(highestDeploy != 0)
                                    hasSmallerDeploy = true;

                                highestDeploy = deployHeight;
                            }
                        }
                        else
                        {
                            manual++;
                        }
                    }

                    blocks.Clear();

                    if(auto > 0)
                    {
                        if(!allOn && animFlip)
                            sb.Append("OFF!\n");
                        else if(manual > 0)
                            sb.Append("Mixed\n");
                        else
                            sb.Append("Auto\n");

                        if(hasSmallerDeploy)
                            sb.Append('<');

                        sb.DistanceFormat(highestDeploy, 1);
                    }
                    else if(manual > 0)
                    {
                        sb.Append("Manual");
                    }

                    shorterLivedCache.Add(toolbarItem.Index, sb.ToString(startIndex, sb.Length - startIndex));
                    return true;
                }
            }

            return false;
        }
        #endregion Parachutes
    }
}
