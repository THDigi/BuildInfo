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
using Sandbox.Game.Localization;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Collections;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using ChargeMode = Sandbox.ModAPI.Ingame.ChargeMode;
using DoorStatus = Sandbox.ModAPI.Ingame.DoorStatus;
using MyJumpDriveStatus = Sandbox.ModAPI.Ingame.MyJumpDriveStatus;
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
            return CustomStatus.GetValueOrDefault(blockType, null);
        }

        readonly Dictionary<MyObjectBuilderType, StatusDel> CustomStatus = new Dictionary<MyObjectBuilderType, StatusDel>(MyObjectBuilderType.Comparer);

        bool AnimFlip = false;

        public ToolbarActionStatus(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;
        }

        protected override void RegisterComponent()
        {
            InitCustomStatus();

            MyVisualScriptLogicProvider.PlayerEnteredCockpit += EnteredCockpit;

            Main.GameConfig.OptionsMenuClosed += UpdatePBExceptionPrefixes;
            UpdatePBExceptionPrefixes();
        }

        protected override void UnregisterComponent()
        {
            MyVisualScriptLogicProvider.PlayerEnteredCockpit -= EnteredCockpit;

            Main.GameConfig.OptionsMenuClosed -= UpdatePBExceptionPrefixes;
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(tick % ShortCacheLiveTicks == 0)
            {
                ShortLivedCache.Clear();
            }

            if(tick % ShorterCacheLiveTicks == 0)
            {
                ShorterLivedCache.Clear();
            }

            if(tick % (Constants.TICKS_PER_SECOND / 2) == 0)
            {
                AnimFlip = !AnimFlip;
            }
        }

        void EnteredCockpit(string entityName, long playerId, string gridName)
        {
            try
            {
                Utils.AssertMainThread();

                var player = MyAPIGateway.Session?.Player;
                if(player != null && player.IdentityId == playerId)
                {
                    var controlled = MyAPIGateway.Session.ControlledObject as IMyShipController;
                    if(controlled != null && PreviousShipController != controlled.EntityId)
                    {
                        PreviousShipController = controlled.EntityId;

                        // clear caches when entering a different cockpit as they no longer are relevant, since they're per slot index

                        foreach(var sb in ShortLivedCache.Values)
                            SBCachePool.Return(sb.Clear());

                        foreach(var sb in ShorterLivedCache.Values)
                            SBCachePool.Return(sb.Clear());

                        ShortLivedCache.Clear();
                        ShorterLivedCache.Clear();
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        #region Generic fallback
        /// <summary>
        /// Generic status that is used only if the per-type statuses return false.
        /// </summary>
        public bool Status_GenericFallback(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupName != null)
                return Status_GenericGroupFallback(actionLabel, block, toolbarItem, sb);

            switch(actionLabel.Action.Id)
            {
                case "UseConveyor":
                {
                    bool useConveyor = block.GetValueBool("UseConveyor"); // TODO: replace when GetProperty() no longer necessary
                    sb.Append(useConveyor ? "Share" : "Isolate");
                    return true;
                }

                // TODO: more non-group shared actions?
            }

            return false;
        }

        bool Status_GenericGroupFallback(ActionWriterOverride actionLabel, IMyTerminalBlock firstBlockInGroup, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "OnOff":
                case "OnOff_On":
                case "OnOff_Off":
                {
                    using(var token = new CacheGroupToken<IMyFunctionalBlock>(CacheType.Short, firstBlockInGroup, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

                        int on = 0;

                        foreach(IMyFunctionalBlock b in token.Blocks)
                        {
                            if(b.Enabled)
                                on++;
                        }

                        int off = (token.Blocks.Count - on);
                        bool tooMany = (token.Blocks.Count) > 99;

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

                        return true;
                    }
                }

                case "UseConveyor":
                {
                    using(var token = new CacheGroupToken<IMyTerminalBlock>(CacheType.Short, firstBlockInGroup, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

                        int useConveyor = 0;

                        foreach(IMyTerminalBlock b in Blocks)
                        {
                            var prop = b.GetProperty("UseConveyor"); // TODO: replace when GetProperty() no longer necessary
                            if(prop != null)
                            {
                                if(prop.AsBool().GetValue(b))
                                    useConveyor++;
                            }
                        }

                        int total = Blocks.Count;
                        int noConveyor = (total - useConveyor);

                        if(useConveyor == total)
                        {
                            sb.Append("All\nShared");
                        }
                        else if(noConveyor == total)
                        {
                            sb.Append("All\nIsolate");
                        }
                        else
                        {
                            sb.Append(useConveyor).Append(" shared\n");
                            sb.Append(noConveyor).Append(" isolate");
                        }

                        return true;
                    }
                }

                // TODO: more shared actions...
            }

            return false;
        }
        #endregion Generic fallback

        void InitCustomStatus()
        {
            Add(typeof(MyObjectBuilder_MyProgrammableBlock), Status_PB);

            Add(typeof(MyObjectBuilder_TimerBlock), Status_Timer);

            Add(typeof(MyObjectBuilder_BatteryBlock), Status_Battery);

            Add(typeof(MyObjectBuilder_JumpDrive), Status_JumpDrive);

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
            CustomStatus.Add(type, action);
        }

        // NOTE: for groups only the first block gets the writer called, and no way to detect if it's a group.
        // TODO: one method per action ID instead for less switch() on runtime?

        #region Programmable Blocks
        bool Status_PB(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupName != null)
                return Status_PBGroup(actionLabel, block, toolbarItem, sb);

            switch(actionLabel.Action.Id)
            {
                case "Run":
                case "RunWithDefaultArgument":
                {
                    if(!MyAPIGateway.Session.SessionSettings.EnableIngameScripts)
                    {
                        sb.Append("ERROR:\nNotAllowed");
                        return true;
                    }

                    var pb = (IMyProgrammableBlock)block;

                    if(AnimFlip && !pb.Enabled)
                    {
                        sb.Append("OFF!");
                    }
                    else
                    {
                        string detailInfo = pb.DetailedInfo; // allocates a string so best to not call this unnecessarily

                        if(!string.IsNullOrEmpty(detailInfo))
                        {
                            if(detailInfo.StartsWith(PBDIE_NoMain))
                            {
                                sb.Append("ERROR:\nNo Main()");
                            }
                            else if(detailInfo.StartsWith(PBDIE_NoValidCtor))
                            {
                                sb.Append("ERROR:\nInvalid");
                            }
                            else if(detailInfo.StartsWith(PBDIE_NoAssembly)
                                 || detailInfo.StartsWith(PBDIE_OwnershipChanged))
                            {
                                sb.Append("ERROR:\nCompile");
                            }
                            else if(detailInfo.StartsWith(PBDIE_TooComplex)
                                 || detailInfo.StartsWith(PBDIE_NestedTooComplex))
                            {
                                sb.Append("ERROR:\nTooComplex");
                            }
                            else if(detailInfo.StartsWith(PBDIE_Caught))
                            {
                                sb.Append("ERROR:\nException");
                            }
                            else
                            {
                                // append this max amount of lines from PB detailedinfo/echo
                                int allowedLines = 2;
                                int width = 0;

                                for(int i = 0; i < detailInfo.Length; ++i)
                                {
                                    var chr = detailInfo[i];
                                    if(chr == '\n')
                                    {
                                        width = 0;
                                        if(--allowedLines == 0)
                                            break;
                                    }

                                    int chrSize = BuildInfoMod.Instance.FontsHandler.GetCharSize(chr);
                                    width += chrSize;

                                    // don't add characters beyond line width limit because it erases all lines below it
                                    if(width <= ToolbarActionLabels.MaxLineSize)
                                    {
                                        sb.Append(chr);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // running or idle without any echo, nothing client can detect here
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        bool Status_PBGroup(ActionWriterOverride actionLabel, IMyTerminalBlock firstBlockInGroup, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Run":
                case "RunWithDefaultArgument":
                {
                    if(!MyAPIGateway.Session.SessionSettings.EnableIngameScripts)
                    {
                        sb.Append("ERROR:\nNotAllowed");
                        return true;
                    }

                    using(var token = new CacheGroupToken<IMyProgrammableBlock>(CacheType.Short, firstBlockInGroup, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

                        bool allOn = true;
                        int errors = 0;
                        int echo = 0;

                        foreach(IMyProgrammableBlock pb in Blocks)
                        {
                            if(allOn && !pb.Enabled)
                                allOn = false;

                            string detailInfo = pb.DetailedInfo; // allocates a string so best to not call this unnecessarily

                            if(!string.IsNullOrEmpty(detailInfo))
                            {
                                if(detailInfo.StartsWith(PBDIE_NoMain)
                                || detailInfo.StartsWith(PBDIE_NoValidCtor)
                                || detailInfo.StartsWith(PBDIE_NoAssembly)
                                || detailInfo.StartsWith(PBDIE_OwnershipChanged)
                                || detailInfo.StartsWith(PBDIE_TooComplex)
                                || detailInfo.StartsWith(PBDIE_NestedTooComplex)
                                || detailInfo.StartsWith(PBDIE_Caught))
                                {
                                    errors++;
                                }
                                else
                                {
                                    echo++;
                                }
                            }
                        }

                        int total = Blocks.Count;

                        if(!allOn)
                            sb.Append("OFF!\n");

                        if(errors == 0)
                        {
                            sb.Append(echo).Append(" msg");
                        }
                        else
                        {
                            sb.Append(errors).Append(" error");
                        }

                        return true;
                    }
                }
            }

            return false;
        }
        #endregion Programmable Blocks

        bool Status_Timer(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Start":
                case "Stop":
                case "TriggerNow":
                {
                    var timer = (IMyTimerBlock)block;
                    bool working = timer.IsWorking;
                    StringBuilder cachedText;

                    if(working && timer.IsCountingDown)
                    {
                        // manually caching so that we can animate faster than the cache
                        if(!WasJustTriggered(actionLabel, toolbarItem) && ShortLivedCache.TryGetValue(toolbarItem.Index, out cachedText))
                        {
                            if(AnimFlip)
                                sb.Append("  ").AppendStringBuilder(cachedText);
                            else
                                sb.Append("ˇ ").AppendStringBuilder(cachedText).Append(" ˇ");
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
                                    if(!ShortLivedCache.TryGetValue(toolbarItem.Index, out cachedText))
                                        ShortLivedCache[toolbarItem.Index] = cachedText = SBCachePool.Get();

                                    cachedText.Clear().AppendSubstring(detailedInfo, separatorIndex, endLineIndex - separatorIndex);

                                    // keep blinking separate from cache so it can blink faster.
                                    if(AnimFlip)
                                        sb.Append("  ").AppendStringBuilder(cachedText);
                                    else
                                        sb.Append("ˇ ").AppendStringBuilder(cachedText).Append(" ˇ");
                                    return true;
                                }
                            }
                        }

                        sb.Append("Running");

                        if(!ShortLivedCache.TryGetValue(toolbarItem.Index, out cachedText))
                            ShortLivedCache[toolbarItem.Index] = cachedText = SBCachePool.Get();

                        cachedText.Clear().Append("Running");
                    }
                    else
                    {
                        if(ShortLivedCache.TryGetValue(toolbarItem.Index, out cachedText))
                        {
                            SBCachePool.Return(cachedText.Clear());
                            ShortLivedCache.Remove(toolbarItem.Index);
                        }

                        sb.Append(working ? "Stopped" : "Off");
                    }

                    return true;
                }

                case "Silent":
                {
                    var timer = (IMyTimerBlock)block;
                    sb.Append(timer.Silent ? "Silent" : "Loud");
                    return true;
                }
            }

            return false;
        }

        #region Batteries
        bool Status_Battery(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupName != null)
                return Status_BatteryGroup(actionLabel, block, toolbarItem, sb);

            switch(actionLabel.Action.Id)
            {
                case "ChargeMode":
                case "Recharge":
                case "Discharge":
                case "Auto":
                {
                    var battery = (IMyBatteryBlock)block;

                    if(AnimFlip && !battery.Enabled)
                    {
                        sb.Append("OFF!");
                    }
                    else
                    {
                        switch(battery.ChargeMode)
                        {
                            case ChargeMode.Auto: sb.Append("Auto"); break;
                            case ChargeMode.Recharge: sb.Append("Charge"); break;
                            case ChargeMode.Discharge: sb.Append("Drain"); break;
                        }
                    }

                    sb.Append("\n");

                    int filledPercent = (int)((battery.CurrentStoredPower / battery.MaxStoredPower) * 100);
                    sb.Append(filledPercent).Append("% ");

                    const float RatioOfMaxForDoubleArrows = 0.9f;

                    float powerFlow = (battery.CurrentInput - battery.CurrentOutput);
                    bool highFlow = false;
                    if(powerFlow > 0)
                        highFlow = (powerFlow > battery.MaxInput * RatioOfMaxForDoubleArrows);
                    else if(powerFlow < 0)
                        highFlow = (Math.Abs(powerFlow) > battery.MaxOutput * RatioOfMaxForDoubleArrows);

                    if(AnimFlip && powerFlow > 0)
                        sb.Append(highFlow ? "++" : "+");
                    else if(AnimFlip && powerFlow < 0)
                        sb.Append(highFlow ? "--" : "-");

                    return true;
                }
            }

            return false;
        }

        bool Status_BatteryGroup(ActionWriterOverride actionLabel, IMyTerminalBlock firstBlockInGroup, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "ChargeMode":
                case "Recharge":
                case "Discharge":
                case "Auto":
                {
                    using(var token = new CacheGroupToken<IMyBatteryBlock>(CacheType.Short, firstBlockInGroup, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

                        bool allOn = true;
                        float averageFilled = 0f;
                        float averageFlow = 0f;
                        float totalFlow = 0f;
                        float maxInput = 0f;
                        float maxOutput = 0f;
                        int auto = 0;
                        int recharge = 0;
                        int discharge = 0;

                        foreach(IMyBatteryBlock battery in Blocks)
                        {
                            if(!battery.Enabled)
                                allOn = false;

                            averageFilled += (battery.CurrentStoredPower / battery.MaxStoredPower);
                            totalFlow += (battery.CurrentInput - battery.CurrentOutput);

                            maxInput += battery.MaxInput;
                            maxOutput += battery.MaxOutput;

                            switch(battery.ChargeMode)
                            {
                                case ChargeMode.Auto: auto++; break;
                                case ChargeMode.Recharge: recharge++; break;
                                case ChargeMode.Discharge: discharge++; break;
                            }
                        }

                        int total = Blocks.Count;

                        if(averageFilled > 0)
                        {
                            averageFilled /= total;
                            averageFilled *= 100;
                        }

                        if(totalFlow != 0) // can be negative too
                            averageFlow = totalFlow / total;

                        if(AnimFlip && !allOn)
                        {
                            sb.Append("OFF!\n");
                        }
                        else
                        {
                            if(auto == total)
                                sb.Append("Auto\n");
                            else if(recharge == total)
                                sb.Append("Charge\n");
                            else if(discharge == total)
                                sb.Append("Drain\n");
                            else
                                sb.Append("Mixed\n");
                        }

                        sb.Append((int)averageFilled).Append("% ");

                        const float RatioOfMaxForDoubleArrows = 0.9f;

                        bool highFlow = false;
                        if(averageFlow > 0)
                            highFlow = (averageFlow > maxInput * RatioOfMaxForDoubleArrows);
                        else if(averageFlow < 0)
                            highFlow = (Math.Abs(averageFlow) > maxOutput * RatioOfMaxForDoubleArrows);

                        if(AnimFlip && averageFlow > 0)
                            sb.Append(highFlow ? "++" : "+");
                        else if(AnimFlip && averageFlow < 0)
                            sb.Append(highFlow ? "--" : "-");

                        return true;
                    }
                }
            }

            return false;
        }
        #endregion Batteries

        #region Jumpdrives
        bool Status_JumpDrive(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupName != null)
                return Status_JumpDriveGroup(actionLabel, block, toolbarItem, sb);

            switch(actionLabel.Action.Id)
            {
                case "Jump":
                case "Recharge":
                case "Recharge_On":
                case "Recharge_Off":
                {
                    bool jumpAction = (actionLabel.Action.Id == "Jump");

                    if(jumpAction)
                    {
                        float countdown = Main.JumpDriveMonitor.GetJumpCountdown(block.CubeGrid.EntityId);
                        if(countdown > 0)
                        {
                            sb.Append("Jumping\n");
                            sb.TimeFormat(countdown);
                            return true;
                        }
                    }

                    var jd = (IMyJumpDrive)block;
                    if(!jd.Enabled)
                    {
                        sb.Append("Off"); // no blink here because Status gets changed to weird stuff when off.
                    }
                    else
                    {
                        switch(jd.Status)
                        {
                            case MyJumpDriveStatus.Charging:
                            {
                                var recharge = jd.GetValueBool("Recharge");
                                sb.Append(recharge ? "Charge" : "Stop");
                                break;
                            }
                            case MyJumpDriveStatus.Ready: sb.Append("Ready"); break;
                            case MyJumpDriveStatus.Jumping: sb.Append("Jump..."); break;
                            default: return false;
                        }
                    }

                    sb.Append("\n");

                    int filledPercent = (int)((jd.CurrentStoredPower / jd.MaxStoredPower) * 100);
                    sb.Append(filledPercent).Append("%");

                    var sink = jd.Components.Get<MyResourceSinkComponent>();
                    if(sink != null)
                    {
                        const float RatioOfMaxForDoubleArrows = 0.9f;

                        float input = sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
                        float maxInput = sink.MaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId);
                        bool highFlow = (input > (maxInput * RatioOfMaxForDoubleArrows));

                        if(AnimFlip && input > 0)
                            sb.Append(highFlow ? "++" : "+");
                    }

                    return true;
                }

                case "IncreaseJumpDistance":
                case "DecreaseJumpDistance":
                {
                    var jd = (IMyJumpDrive)block;

                    if(AnimFlip)
                    {
                        var prop = jd.GetProperty("JumpDistance") as IMyTerminalControlSlider;
                        if(prop != null && !prop.Enabled.Invoke(block))
                        {
                            sb.Append("GPS!");
                            return true;
                        }
                    }

                    if(actionLabel.OriginalWriter != null)
                    {
                        int startIndex = sb.Length;

                        // vanilla writer but with some alterations as it's easier than re-doing the entire math for jump distance.
                        actionLabel.OriginalWriter.Invoke(block, sb);

                        for(int i = startIndex; i < sb.Length; i++)
                        {
                            char c = sb[i];
                            if(c == '%' && sb.Length > (i + 2))
                            {
                                sb[i + 2] = '\n'; // replace starting paranthesis with newline
                                sb.Length -= 1; // remove ending paranthesis
                                return true;
                            }
                        }
                    }
                    break;
                }
            }

            return false;
        }

        bool Status_JumpDriveGroup(ActionWriterOverride actionLabel, IMyTerminalBlock firstBlockInGroup, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Recharge":
                case "Recharge_On":
                case "Recharge_Off":
                {
                    using(var token = new CacheGroupToken<IMyJumpDrive>(CacheType.Short, firstBlockInGroup, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

                        bool allOn = true;
                        float averageFilled = 0f;
                        float input = 0f;
                        float maxInput = 0f;
                        int ready = 0;
                        int charge = 0;

                        foreach(IMyJumpDrive jd in Blocks)
                        {
                            if(!jd.Enabled)
                                allOn = false;

                            averageFilled += (jd.CurrentStoredPower / jd.MaxStoredPower);

                            var sink = jd.Components.Get<MyResourceSinkComponent>();
                            if(sink != null)
                            {
                                input += sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
                                maxInput += sink.MaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId);
                            }

                            switch(jd.Status)
                            {
                                case MyJumpDriveStatus.Ready: ready++; break;
                                case MyJumpDriveStatus.Charging: charge++; break;
                            }
                        }

                        int total = Blocks.Count;

                        if(averageFilled > 0)
                        {
                            averageFilled /= total;
                            averageFilled *= 100;
                        }

                        float averageInput = 0;
                        if(input > 0)
                            averageInput = input / total;

                        if(AnimFlip && !allOn)
                        {
                            sb.Append("OFF!\n");
                        }
                        else
                        {
                            if(ready == total)
                                sb.Append("Ready\n");
                            else if(charge == total)
                                sb.Append("Charge\n");
                            else
                                sb.Append("Mixed\n");
                        }

                        sb.Append((int)averageFilled).Append("%");

                        return true;
                    }
                }
            }

            return false;
        }
        #endregion Jumpdrives

        bool Status_OxygenGenerator(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Refill":
                {
                    using(var token = new CacheToken(CacheType.Short, block, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

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

                        return true;
                    }
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
                    using(var token = new CacheToken(CacheType.Short, block, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

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

                        if(itemsToFill > 0)
                        {
                            sb.Append("Refill:").Append('\n').Append(itemsToFill);
                        }
                        else
                        {
                            sb.Append(tank.FilledRatio > 0 ? "No Bottles" : "No Gas");
                        }

                        return true;
                    }
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

        #region Rotors
        bool Status_MotorStator(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupName != null)
                return Status_MotorStatorGroup(actionLabel, block, toolbarItem, sb);

            switch(actionLabel.Action.Id)
            {
                case "Reverse":
                {
                    var stator = (IMyMotorStator)block;
                    float angleRad = stator.Angle;

                    if(AnimFlip && !stator.Enabled)
                        sb.Append("OFF!\n");
                    else if(!AnimFlip && stator.TargetVelocityRPM == 0)
                        sb.Append("NoVel!\n");

                    float minRad = stator.LowerLimitRad;
                    float maxRad = stator.UpperLimitRad;

                    // is rotor limited in both directions
                    if(minRad >= -MathHelper.TwoPi && maxRad <= MathHelper.TwoPi)
                    {
                        float progress = (angleRad - minRad) / (maxRad - minRad);
                        sb.ProportionToPercent(progress);
                    }
                    else
                    {
                        sb.AngleFormat(angleRad, 0);
                    }

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

        bool Status_MotorStatorGroup(ActionWriterOverride actionLabel, IMyTerminalBlock firstBlockInGroup, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Reverse":
                {
                    using(var token = new CacheGroupToken<IMyMotorStator>(CacheType.Shorter, firstBlockInGroup, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

                        bool allLimited = true;
                        bool allOn = true;
                        bool allCanMove = true;
                        float travelAverage = 0;
                        float angleMin = float.MaxValue;
                        float angleMax = float.MinValue;

                        foreach(IMyMotorStator stator in Blocks)
                        {
                            if(!stator.Enabled)
                                allOn = false;

                            if(stator.TargetVelocityRPM == 0)
                                allCanMove = false;

                            float angle = stator.Angle;

                            angleMin = Math.Min(angleMin, angle);
                            angleMax = Math.Max(angleMax, angle);

                            float minRad = stator.LowerLimitRad;
                            float maxRad = stator.UpperLimitRad;

                            // is rotor limited in both directions
                            if(minRad >= -MathHelper.TwoPi && maxRad <= MathHelper.TwoPi)
                                travelAverage += (angle - minRad) / (maxRad - minRad);
                            else
                                allLimited = false;
                        }

                        int total = Blocks.Count;

                        if(travelAverage > 0)
                            travelAverage /= total;

                        if(AnimFlip && !allOn)
                            sb.Append("OFF!\n");

                        if(!AnimFlip && allCanMove)
                            sb.Append("NoVel!\n");

                        if(allLimited)
                        {
                            sb.ProportionToPercent(travelAverage);
                        }
                        else
                        {
                            if(Math.Abs(angleMin - angleMax) <= 0.1f)
                            {
                                sb.AngleFormat(angleMin, 0);
                            }
                            else
                            {
                                sb.AngleFormat(angleMin, 0).Append("~").AngleFormat(angleMax);
                            }
                        }

                        return true;
                    }
                }

                case "Attach":
                case "Detach":
                case "Add Top Part":
                case "Add Small Top Part":
                {
                    using(var token = new CacheGroupToken<IMyMotorStator>(CacheType.Short, firstBlockInGroup, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

                        int attached = 0;

                        foreach(IMyMotorStator stator in Blocks)
                        {
                            if(stator.IsAttached)
                                attached++;
                        }

                        int total = Blocks.Count;
                        int detached = (total - attached);

                        if(attached == total)
                        {
                            sb.Append("All\nAttached");
                        }
                        else if(detached == total)
                        {
                            sb.Append("All\nDetached");
                        }
                        else
                        {
                            sb.Append("Detached:\n").Append(detached);
                        }

                        return true;
                    }
                }
            }

            return false;
        }
        #endregion Rotors

        #region Suspensions
        bool Status_Suspension(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupName != null)
                return Status_SuspensionGroup(actionLabel, block, toolbarItem, sb);

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

        bool Status_SuspensionGroup(ActionWriterOverride actionLabel, IMyTerminalBlock firstBlockInGroup, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Add Top Part":
                {
                    using(var token = new CacheGroupToken<IMyMotorSuspension>(CacheType.Short, firstBlockInGroup, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

                        int attached = 0;

                        foreach(IMyMotorSuspension stator in Blocks)
                        {
                            if(stator.IsAttached)
                                attached++;
                        }

                        int total = Blocks.Count;
                        int detached = (total - attached);

                        if(attached == total)
                        {
                            sb.Append("All\nAttached");
                        }
                        else if(detached == total)
                        {
                            sb.Append("All\nDetached");
                        }
                        else
                        {
                            sb.Append("Detached:\n").Append(detached);
                        }

                        return true;
                    }
                }
            }

            return false;
        }
        #endregion Suspensions

        #region Pistons
        bool Status_Piston(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupName != null)
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

        bool Status_PistonGroup(ActionWriterOverride actionLabel, IMyTerminalBlock firstBlockInGroup, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Extend":
                case "Retract":
                case "Reverse":
                {
                    using(var token = new CacheGroupToken<IMyPistonBase>(CacheType.Shorter, firstBlockInGroup, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

                        float travelAverage = 0;
                        bool allOn = true;

                        foreach(IMyPistonBase piston in Blocks)
                        {
                            if(!piston.Enabled)
                                allOn = false;

                            float min = piston.MinLimit;
                            float max = piston.MaxLimit;
                            float travelRatio = (piston.CurrentPosition - min) / (max - min);
                            travelAverage += travelRatio;
                        }

                        if(travelAverage > 0)
                            travelAverage /= Blocks.Count;

                        if(AnimFlip && !allOn)
                            sb.Append("OFF!\n");

                        sb.Append((int)(travelAverage * 100)).Append("%");

                        return true;
                    }
                }

                case "Attach":
                case "Detach":
                case "Add Top Part":
                {
                    using(var token = new CacheGroupToken<IMyPistonBase>(CacheType.Short, firstBlockInGroup, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

                        int attached = 0;

                        foreach(IMyPistonBase piston in Blocks)
                        {
                            if(piston.IsAttached)
                                attached++;
                        }

                        int total = Blocks.Count;

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

                        return true;
                    }
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
            if(toolbarItem.GroupName != null)
                return Status_DoorsGroup(actionLabel, block, toolbarItem, sb);

            // TODO: does AdvancedDoor need special treatment for OpenRatio?

            switch(actionLabel.Action.Id)
            {
                case "Open":
                case "Open_On":
                case "Open_Off":
                {
                    var door = (IMyDoor)block;

                    if(AnimFlip && !door.Enabled)
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

        bool Status_DoorsGroup(ActionWriterOverride actionLabel, IMyTerminalBlock firstBlockInGroup, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Open":
                case "Open_On":
                case "Open_Off":
                {
                    using(var token = new CacheGroupToken<IMyDoor>(CacheType.Shorter, firstBlockInGroup, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

                        // TODO include off state?

                        int open = 0;
                        int closed = 0;
                        int opening = 0;
                        int closing = 0;

                        float averageOpenRatio = 0;

                        foreach(IMyDoor door in Blocks)
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

                        int doors = Blocks.Count;
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

            return false;
        }
        #endregion Doors

        #region Parachutes
        bool Status_Parachute(ActionWriterOverride actionLabel, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            if(toolbarItem.GroupName != null)
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

                    if(AnimFlip && !parachute.Enabled)
                        sb.Append("OFF!\n");

                    if(!AnimFlip && !hasAmmo && parachute.Status != DoorStatus.Open)
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
                        if(AnimFlip && !parachute.Enabled)
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

        bool Status_ParachuteGroup(ActionWriterOverride actionLabel, IMyTerminalBlock firstBlockInGroup, ToolbarItemData toolbarItem, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Open":
                case "Open_On":
                case "Open_Off":
                {
                    using(var token = new CacheGroupToken<IMyParachute>(CacheType.Shorter, firstBlockInGroup, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

                        int open = 0;
                        int ready = 0;
                        bool allAmmo = true;
                        bool allOn = true;

                        foreach(IMyParachute parachute in Blocks)
                        {
                            if(parachute.Status != DoorStatus.Open)
                            {
                                var inv = parachute.GetInventory();
                                if(inv != null)
                                {
                                    // HACK block cast needed because modAPI IMyParachute implements ingame interfaces instead of modAPI ones.
                                    var def = (MyParachuteDefinition)((IMyTerminalBlock)parachute).SlimBlock.BlockDefinition;
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

                        if(AnimFlip && !allOn)
                            sb.Append("OFF!\n");

                        if(!AnimFlip && !allAmmo)
                            sb.Append("Empty!\n");

                        if(open > 0 && ready > 0)
                            sb.Append("Mixed");
                        else if(open > 0)
                            sb.Append("Deployed");
                        else
                            sb.Append("Ready");

                        return true;
                    }
                }

                case "AutoDeploy": // vanilla status is borked
                {
                    using(var token = new CacheGroupToken<IMyParachute>(CacheType.Shorter, firstBlockInGroup, toolbarItem, sb))
                    {
                        if(token.ReturnEarly)
                            return token.ReturnVal;

                        bool allOn = true;
                        int auto = 0;
                        int manual = 0;

                        bool hasSmallerDeploy = false;
                        float highestDeploy = 0;

                        foreach(IMyParachute parachute in Blocks)
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

                        if(auto > 0)
                        {
                            if(AnimFlip && !allOn)
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

                        return true;
                    }
                }
            }

            return false;
        }
        #endregion Parachutes

        #region Caching
        long PreviousShipController;

        readonly MyConcurrentPool<StringBuilder> SBCachePool = new MyConcurrentPool<StringBuilder>(activator: () => new StringBuilder(256));

        readonly Dictionary<int, StringBuilder> ShortLivedCache = new Dictionary<int, StringBuilder>();
        readonly Dictionary<int, StringBuilder> ShorterLivedCache = new Dictionary<int, StringBuilder>();

        readonly List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();

        bool WasJustTriggered(ActionWriterOverride actionLabel, ToolbarItemData toolbarItem)
        {
            return (Main.Tick == Main.ToolbarActionLabels.TriggeredAtTick && toolbarItem.Index == Main.ToolbarActionLabels.TriggeredIndex);
        }

        public enum CacheType
        {
            /// <summary>
            /// Don't use any cache
            /// </summary>
            None = 0,

            /// <summary>
            /// Use the short cache (1s)
            /// </summary>
            Short = 1,

            /// <summary>
            /// Shorter cache (0.25s)
            /// </summary>
            Shorter = 2,
        }

        public struct CacheGroupToken<T> : IDisposable where T : class
        {
            readonly int CacheIndex;
            readonly Dictionary<int, StringBuilder> Cache;

            readonly StringBuilder SB;
            readonly int TextIndex;

            public readonly List<IMyTerminalBlock> Blocks;

            public readonly bool ReturnEarly;
            public readonly bool ReturnVal;

            public CacheGroupToken(CacheType cacheType, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
            {
                if(toolbarItem.GroupName == null)
                    throw new Exception($"CacheGroupToken has been given null group! block={block.CustomName} ({block.EntityId.ToString()}) on grid={block.CubeGrid.CustomName} ({block.CubeGrid.EntityId.ToString()}), slot index={toolbarItem.Index.ToString()}");

                BuildInfoMod Main = BuildInfoMod.Instance;

                switch(cacheType)
                {
                    case CacheType.None: Cache = null; break;
                    case CacheType.Short: Cache = Main.ToolbarActionStatus.ShortLivedCache; break;
                    case CacheType.Shorter: Cache = Main.ToolbarActionStatus.ShorterLivedCache; break;
                    default: throw new Exception($"Unimplemented cache type: {cacheType} (num={(int)cacheType})");
                }

                CacheIndex = toolbarItem.Index;

                SB = sb;
                TextIndex = 0;

                Blocks = null;

                ReturnEarly = false;
                ReturnVal = false;

                // this is just a short-lived cache to aleviate callback spam
                if(Cache != null)
                {
                    // when toolbar slot is pressed the TriggeredIndex and TriggeredAtTick are set, this will force a cache refresh.
                    bool skipCache = (Main.Tick == Main.ToolbarActionLabels.TriggeredAtTick && toolbarItem.Index == Main.ToolbarActionLabels.TriggeredIndex);

                    StringBuilder cachedText;
                    if(!skipCache && Cache.TryGetValue(toolbarItem.Index, out cachedText))
                    {
                        sb.AppendStringBuilder(cachedText);
                        Cache = null; // no cache update in Dispose()
                        ReturnEarly = true; // no reprocessing
                        ReturnVal = true; // true = status was added
                        return;
                    }

                    TextIndex = Math.Max(0, sb.Length); // start inedx of cached text
                }

                Blocks = Main.ToolbarActionStatus.Blocks;
                Blocks.Clear();

                var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(block.CubeGrid);
                var group = gts?.GetBlockGroupWithName(toolbarItem.GroupName);
                group?.GetBlocksOfType<T>(Blocks);

                // if group exists and requested blocks are found then group is valid, otherwise ignore this status.
                ReturnEarly = (Blocks == null || Blocks.Count <= 0);
                ReturnVal = ReturnEarly;
            }

            public void Dispose()
            {
                Blocks?.Clear();

                if(Cache != null) // update cache if it should
                {
                    StringBuilder cachedText;
                    if(!Cache.TryGetValue(CacheIndex, out cachedText))
                        Cache[CacheIndex] = cachedText = BuildInfoMod.Instance.ToolbarActionStatus.SBCachePool.Get();

                    cachedText.Clear().AppendSubstring(SB, TextIndex, SB.Length - TextIndex);
                }
            }
        }

        public struct CacheToken : IDisposable
        {
            readonly int CacheIndex;
            readonly Dictionary<int, StringBuilder> Cache;

            readonly StringBuilder SB;
            readonly int TextIndex;

            public readonly bool ReturnEarly;
            public readonly bool ReturnVal;

            public CacheToken(CacheType cacheType, IMyTerminalBlock block, ToolbarItemData toolbarItem, StringBuilder sb)
            {
                BuildInfoMod Main = BuildInfoMod.Instance;

                switch(cacheType)
                {
                    case CacheType.None: Cache = null; break;
                    case CacheType.Short: Cache = Main.ToolbarActionStatus.ShortLivedCache; break;
                    case CacheType.Shorter: Cache = Main.ToolbarActionStatus.ShorterLivedCache; break;
                    default: throw new Exception($"Unimplemented cache type: {cacheType} (num={(int)cacheType})");
                }

                CacheIndex = toolbarItem.Index;

                SB = sb;
                TextIndex = 0;

                ReturnEarly = false;
                ReturnVal = false;

                // this is just a short-lived cache to aleviate callback spam
                if(Cache != null)
                {
                    // when toolbar slot is pressed the TriggeredIndex and TriggeredAtTick are set, this will force a cache refresh.
                    bool skipCache = (Main.Tick == Main.ToolbarActionLabels.TriggeredAtTick && toolbarItem.Index == Main.ToolbarActionLabels.TriggeredIndex);

                    StringBuilder cachedText;
                    if(!skipCache && Cache.TryGetValue(toolbarItem.Index, out cachedText))
                    {
                        sb.AppendStringBuilder(cachedText);
                        Cache = null; // no cache update in Dispose()
                        ReturnEarly = true; // no reprocessing
                        ReturnVal = true; // true = status was added
                        return;
                    }

                    TextIndex = Math.Max(0, sb.Length); // start inedx of cached text
                }

                ReturnVal = true;
            }

            public void Dispose()
            {
                if(Cache != null) // update cache if it should
                {
                    StringBuilder cachedText;
                    if(!Cache.TryGetValue(CacheIndex, out cachedText))
                        Cache[CacheIndex] = cachedText = BuildInfoMod.Instance.ToolbarActionStatus.SBCachePool.Get();

                    cachedText.Clear().AppendSubstring(SB, TextIndex, SB.Length - TextIndex);
                }
            }
        }
        #endregion Caching

        string PBDIE_NoMain;
        string PBDIE_NoValidCtor;
        string PBDIE_NoAssembly;
        string PBDIE_OwnershipChanged;
        string PBDIE_NestedTooComplex;
        string PBDIE_TooComplex;
        string PBDIE_Caught;

        void UpdatePBExceptionPrefixes()
        {
            PBDIE_NoMain = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_NoMain);
            PBDIE_NoValidCtor = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_NoValidConstructor);
            PBDIE_NoAssembly = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_NoAssembly);
            PBDIE_OwnershipChanged = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_Ownershipchanged);
            PBDIE_NestedTooComplex = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_NestedTooComplex);
            PBDIE_TooComplex = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_TooComplex);
            PBDIE_Caught = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_ExceptionCaught);
        }

        string GetTranslatedLimited(MyStringId langKey, int maxLength = 10)
        {
            string text = MyTexts.GetString(langKey);
            return text.Substring(0, Math.Min(text.Length, maxLength));
        }
    }
}
