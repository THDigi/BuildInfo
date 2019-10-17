using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Localization;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.ObjectBuilders;

using MyShipConnectorStatus = Sandbox.ModAPI.Ingame.MyShipConnectorStatus;

namespace Digi.BuildInfo.Features
{
    public class ToolbarActionLabels : ModComponent
    {
        public int EnteredCockpitTicks = SHOW_ACTION_LABELS_FOR_TICKS;

        public const int SHOW_ACTION_LABELS_FOR_TICKS = Constants.TICKS_PER_SECOND * 3;

        private readonly Dictionary<IMyTerminalAction, ActionLabel> overwrittenActions = new Dictionary<IMyTerminalAction, ActionLabel>(16);

        public ToolbarActionLabels(BuildInfoMod main) : base(main)
        {
        }

        protected override void RegisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;

            MyVisualScriptLogicProvider.PlayerEnteredCockpit += EnteredCockpit;
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);

            Main.Config.ToolbarActionLabelMode.ValueAssigned += ToolbarActionLabelModeChanged;

            InitCustomStatus();
        }

        protected override void UnregisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;

            MyVisualScriptLogicProvider.PlayerEnteredCockpit -= EnteredCockpit;

            Main.Config.ToolbarActionLabelMode.ValueAssigned -= ToolbarActionLabelModeChanged;
        }

        void ToolbarActionLabelModeChanged(int oldValue, int newValue, SettingBase<int> setting)
        {
            if(oldValue != newValue)
            {
                foreach(var actionLabel in overwrittenActions.Values)
                {
                    actionLabel.SettingChanged(newValue);
                }
            }
        }

        void EnteredCockpit(string entityName, long playerId, string gridName)
        {
            try
            {
                var player = MyAPIGateway.Session?.Player;

                if(player != null && player.IdentityId == playerId)
                {
                    EnteredCockpitTicks = SHOW_ACTION_LABELS_FOR_TICKS;
                    //SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            try
            {
                if(Main.Config.ToolbarActionLabelMode.Value == 0)
                    return;

                foreach(var action in actions)
                {
                    if(!overwrittenActions.ContainsKey(action))
                    {
                        overwrittenActions.Add(action, new ActionLabel(action));
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(EnteredCockpitTicks > 0)
            {
                EnteredCockpitTicks--;
            }

            if(tick % 60 == 0)
            {
                cachedStatusText.Clear();
            }
        }

        #region Custom statuses
        public delegate bool StatusDel(ActionLabel actionLabel, IMyTerminalBlock block, StringBuilder sb);

        private readonly Dictionary<MyObjectBuilderType, StatusDel> customStatus = new Dictionary<MyObjectBuilderType, StatusDel>();

        private readonly Dictionary<long, string> cachedStatusText = new Dictionary<long, string>();

        public StatusDel GetCustomStatus(MyObjectBuilderType blockType)
        {
            return customStatus.GetValueOrDefault(blockType, null);
        }

        void InitCustomStatus()
        {
            Add(typeof(MyObjectBuilder_MyProgrammableBlock), Status_PB);

            Add(typeof(MyObjectBuilder_TimerBlock), Status_Timer);

            Add(typeof(MyObjectBuilder_GasTank), Status_GasTank);
            Add(typeof(MyObjectBuilder_OxygenTank), Status_GasTank);

            Add(typeof(MyObjectBuilder_OxygenGenerator), Status_OxygenGenerator);

            Add(typeof(MyObjectBuilder_SmallGatlingGun), Status_Weapons);
            Add(typeof(MyObjectBuilder_SmallMissileLauncher), Status_Weapons);
            Add(typeof(MyObjectBuilder_SmallMissileLauncherReload), Status_Weapons);
            Add(typeof(MyObjectBuilder_InteriorTurret), Status_Weapons);
            Add(typeof(MyObjectBuilder_LargeGatlingTurret), Status_Weapons);
            Add(typeof(MyObjectBuilder_LargeMissileTurret), Status_Weapons);

            Add(typeof(MyObjectBuilder_Warhead), Status_Warhead);

            Add(typeof(MyObjectBuilder_MotorStator), Status_MotorStator);
            Add(typeof(MyObjectBuilder_MotorAdvancedStator), Status_MotorStator);

            Add(typeof(MyObjectBuilder_MotorSuspension), Status_Suspension);

            Add(typeof(MyObjectBuilder_ExtendedPistonBase), Status_Piston);
            Add(typeof(MyObjectBuilder_PistonBase), Status_Piston);

            Add(typeof(MyObjectBuilder_ShipConnector), Status_Connector);
        }

        void Add(MyObjectBuilderType type, StatusDel action)
        {
            customStatus.Add(type, action);
        }

        // NOTE: for groups only the first block gets the writer called, and no way to detect if it's a group.

        bool Status_PB(ActionLabel actionLabel, IMyTerminalBlock block, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Run":
                case "RunWithDefaultArgument":
                    {
                        var pb = (IMyProgrammableBlock)block;

                        if(!string.IsNullOrEmpty(pb.DetailedInfo))
                        {
                            if(pb.DetailedInfo.StartsWith("Assembly not found"))
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
                            // TODO: print if it's running?
                        }

                        return true;
                    }
            }

            return false;
        }

        bool Status_Timer(ActionLabel actionLabel, IMyTerminalBlock block, StringBuilder sb)
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
                            if(cachedStatusText.TryGetValue(block.EntityId, out cachedText))
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
                                        cachedStatusText.Add(block.EntityId, time);

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
                            cachedStatusText.Add(block.EntityId, "Running");
                        }
                        else
                        {
                            cachedStatusText.Remove(block.EntityId);
                            sb.Append(working ? "Stopped" : "Off");
                        }

                        return true;
                    }
            }

            return false;
        }

        bool Status_OxygenGenerator(ActionLabel actionLabel, IMyTerminalBlock block, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Refill":
                    {
                        string cachedText;
                        if(cachedStatusText.TryGetValue(block.EntityId, out cachedText))
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

                        cachedStatusText.Add(block.EntityId, sb.ToString(startIndex, sb.Length - startIndex));
                        return true;
                    }
            }

            return false;
        }

        bool Status_GasTank(ActionLabel actionLabel, IMyTerminalBlock block, StringBuilder sb)
        {
            switch(actionLabel.Action.Id)
            {
                case "Refill":
                    {
                        string cachedText;
                        if(cachedStatusText.TryGetValue(block.EntityId, out cachedText))
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

                        cachedStatusText.Add(block.EntityId, sb.ToString(startIndex, sb.Length - startIndex));
                        return true;
                    }
            }

            return false;
        }

        bool Status_Weapons(ActionLabel actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_Warhead(ActionLabel actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_MotorStator(ActionLabel actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_Suspension(ActionLabel actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_Piston(ActionLabel actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_Connector(ActionLabel actionLabel, IMyTerminalBlock block, StringBuilder sb)
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
                            case MyShipConnectorStatus.Connected: sb.Append("Locked"); break;
                            case MyShipConnectorStatus.Connectable: sb.Append("Ready"); break;
                            case MyShipConnectorStatus.Unconnected: sb.Append("Unlocked"); break;
                        }

                        return true;
                    }
            }

            return false;
        }
        #endregion
    }

    public class ActionLabel
    {
        public readonly IMyTerminalAction Action;

        public readonly Action<IMyTerminalBlock, StringBuilder> CustomWriter;
        public readonly Action<IMyTerminalBlock, StringBuilder> OriginalWriter;

        private ToolbarActionLabelsMode mode;
        private string actionNameCache;
        private bool ignoreWriter = false;
        private bool customStatusChecked = false;
        private ToolbarActionLabels.StatusDel customStatusFunc = null;

        public ActionLabel(IMyTerminalAction action)
        {
            Action = action;
            OriginalWriter = action.Writer;
            CustomWriter = NewWriter;

            Action.Writer = CustomWriter;

            // HACK giving an icon for some iconless actions
            if(string.IsNullOrEmpty(action.Icon))
            {
                if(Action.Id == "Attach")
                {
                    action.Icon = @"Textures\GUI\Icons\Lock.png";
                }
                else if(Action.Id == "Detach")
                {
                    action.Icon = @"Textures\GUI\Icons\DisconnectedPlayerIcon.png";
                }
            }

            mode = (ToolbarActionLabelsMode)BuildInfoMod.Instance.Config.ToolbarActionLabelMode.Value;
        }

        public void SettingChanged(int newMode)
        {
            mode = (ToolbarActionLabelsMode)newMode;

            // FIXME: uncomment once CustomWriter getter is no longer throwing exceptions for PB Run action
            //if(mode == ToolbarActionLabelsMode.Off)
            //{
            //    Action.Writer = OriginalWriter;
            //    Log.Info($"Reset writer to original for action {Action.Name}"); // DEBUG
            //}
            //else
            //{
            //    Action.Writer = CustomWriter;
            //    Log.Info($"Reset writer to custom for action {Action.Name}"); // DEBUG
            //}
        }

        void NewWriter(IMyTerminalBlock block, StringBuilder sb)
        {
            try
            {
                bool customizedStatus = false;

                if(mode != ToolbarActionLabelsMode.Off)
                {
                    if(mode == ToolbarActionLabelsMode.AlwaysOn
                    || MyAPIGateway.Gui.IsCursorVisible
                    || BuildInfoMod.Instance.ToolbarActionLabels.EnteredCockpitTicks > 0
                    || (mode == ToolbarActionLabelsMode.HudHints && (BuildInfoMod.Instance.GameConfig.HudState == HudState.HINTS || MyAPIGateway.Input.IsAnyAltKeyPressed()))
                    || (mode == ToolbarActionLabelsMode.AltKey && MyAPIGateway.Input.IsAnyAltKeyPressed()))
                    {
                        AppendLabel(block, sb);
                    }

                    sb.Append(' ', LINE_CHAR_STRETCH).Append('\n'); // left-align everything for consistency

                    // elevate the label above the icon so it's readable
                    // also required here to have some empty lines to remove for multi-line custom statuses
                    sb.Append('\n');
                    sb.Append('\n');
                    sb.Append('\n');

                    // TODO: add toggle for custom status too?
                    if(!customStatusChecked)
                    {
                        customStatusChecked = true;
                        customStatusFunc = BuildInfoMod.Instance.ToolbarActionLabels.GetCustomStatus(block.BlockDefinition.TypeId);
                    }

                    if(customStatusFunc != null)
                    {
                        var tempSB = BuildInfoMod.Instance.Caches.SB;
                        tempSB.Clear();

                        customizedStatus = customStatusFunc.Invoke(this, block, tempSB);

                        // remove trailing newlines
                        while(tempSB.Length > 0 && tempSB[tempSB.Length - 1] == '\n')
                        {
                            tempSB.Length -= 1;
                        }

                        // remove newlines from `sb` for every newline added by status' `tempSB`
                        for(int i = 0; i < tempSB.Length; ++i)
                        {
                            if(tempSB[i] == '\n')
                                sb.Length -= 1;
                        }

                        sb.AppendSB(tempSB);
                        tempSB.Clear();
                    }
                }

                if(!customizedStatus && !ignoreWriter)
                {
                    // HACK invoking original Writer on any action that has no writer throws NRE inside the game code, undetectable in a graceful way.
                    try
                    {
                        OriginalWriter?.Invoke(block, sb);
                    }
                    catch(Exception)
                    {
                        ignoreWriter = true;
                        //Log.Error($"Error calling original writer on {block.GetType().Name} with action {Action.Id}\n{e}", Log.PRINT_MSG);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void AppendLabel(IMyTerminalBlock block, StringBuilder sb)
        {
            // issues:
            // - can't detect groups
            // - gets kinda large

            // TODO per-block name cache?
            // TODO toggleable per block somehow (maybe use Show In Toolbar Config?) or toggle names globally

            if(MyAPIGateway.Gui.IsCursorVisible // always show names when in menu
            || (block is IMyCameraBlock && Action.Id == "View")
            || (block is IMyProgrammableBlock && (Action.Id == "Run" || Action.Id == "RunWithDefaultArgument"))
            || (block is IMyTimerBlock && (Action.Id == "TriggerNow" || Action.Id == "Start" || Action.Id == "Stop"))
            || (block is IMyRemoteControl && Action.Id == "Control")
            || (block is IMyLargeTurretBase && Action.Id == "Control")
            || (block is IMyShipConnector && Action.Id == "SwitchLock")
            || (block is IMyPistonBase && (Action.Id == "Reverse" || Action.Id == "Extend" || Action.Id == "Retract"))
            || (block is IMyMotorStator && Action.Id == "Reverse"))
            {
                AppendWordWrapped(sb, block.CustomName);
                sb.Append('\n');
                sb.Append('—', LINE_CHAR_STRETCH).Append('\n');
            }

            if(actionNameCache == null)
            {
                int startIndex = Math.Max(0, sb.Length);

                AppendWordWrapped(sb, Action.Name);

                actionNameCache = sb.ToString(startIndex, sb.Length - startIndex);
            }
            else
            {
                sb.Append(actionNameCache);
            }

            // over the vanilla inventory bar (collides with bar and selected tool text and "Toolbar" label in G menu)
            sb.Append('\n');
            sb.Append('\n');
            sb.Append('\n');

            if(BuildInfoMod.Instance.ShipToolInventoryBar.Shown)
            {
                sb.Append('\n');
                sb.Append('\n');
                sb.Append('\n');
            }
        }

        // measured from "Argumen" word
        // NOTE: only supports English because MyLanguagesEnum is prohibited and some languages scale the text smaller or larger.
        const int MAX_LINE_SIZE = 126;
        const int LINE_CHAR_STRETCH = 16; // arbitrary size to guarantee to go from one side to the other

        private void AppendWordWrapped(StringBuilder sb, StringBuilder text)
        {
            int textLength = text.Length;
            int lineSize = 0;

            for(int i = 0; i < textLength; ++i)
            {
                var chr = text[i];

                int chrSize = GetCharSize(chr);
                lineSize += chrSize;

                if(chr == ' ')
                {
                    // find next space and determine if adding the word would go over the limit
                    int nextSpaceIndex = (textLength - 1);
                    int nextSizeAdd = 0;

                    for(int x = i + 1; x < textLength; ++x)
                    {
                        var chr2 = text[x];
                        nextSizeAdd += GetCharSize(chr2);

                        if(chr2 == ' ')
                        {
                            nextSpaceIndex = x;
                            break;
                        }
                    }

                    if((lineSize + nextSizeAdd) > MAX_LINE_SIZE)
                    {
                        lineSize = 0;
                        sb.Append('\n');
                        continue;
                    }
                }

                if(lineSize > MAX_LINE_SIZE)
                {
                    lineSize = 0;
                    sb.Append('\n');
                }

                sb.Append(chr);
            }
        }

        private void AppendWordWrapped(StringBuilder sb, string text)
        {
            int textLength = text.Length;
            int lineSize = 0;

            for(int i = 0; i < textLength; ++i)
            {
                var chr = text[i];

                int chrSize = GetCharSize(chr);
                lineSize += chrSize;

                if(chr == ' ')
                {
                    // find next space and determine if adding the word would go over the limit
                    int nextSpaceIndex = (textLength - 1);
                    int nextSizeAdd = 0;

                    for(int x = i + 1; x < textLength; ++x)
                    {
                        var chr2 = text[x];
                        nextSizeAdd += GetCharSize(chr2);

                        if(chr2 == ' ')
                        {
                            nextSpaceIndex = x;
                            break;
                        }
                    }

                    if((lineSize + nextSizeAdd) > MAX_LINE_SIZE)
                    {
                        lineSize = 0;
                        sb.Append('\n');
                        continue;
                    }
                }

                if(lineSize > MAX_LINE_SIZE)
                {
                    lineSize = 0;
                    sb.Append('\n');
                }

                sb.Append(chr);
            }
        }

        private int GetCharSize(char chr)
        {
            var charSizeDict = BuildInfoMod.Instance.Constants.CharSize;
            int chrSize;
            if(!charSizeDict.TryGetValue(chr, out chrSize))
            {
                Log.Error($"Couldn't find character size for character: '{chr.ToString()}'", Log.PRINT_MSG);
                chrSize = charSizeDict[' '];
            }
            return chrSize;
        }
    }
}
