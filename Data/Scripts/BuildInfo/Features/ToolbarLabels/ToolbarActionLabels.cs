using System;
using System.Collections.Generic;
using System.Text;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage.ObjectBuilders;

using MyShipConnectorStatus = Sandbox.ModAPI.Ingame.MyShipConnectorStatus;

namespace Digi.BuildInfo.Features.ToolbarLabels
{
    public class ToolbarActionLabels : ModComponent
    {
        public int EnteredCockpitTicks = SHOW_ACTION_LABELS_FOR_TICKS;

        public const int SHOW_ACTION_LABELS_FOR_TICKS = Constants.TICKS_PER_SECOND * 3;

        private readonly Dictionary<IMyTerminalAction, ActionWriterOverride> overriddenActions = new Dictionary<IMyTerminalAction, ActionWriterOverride>(16);

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
                foreach(var actionLabel in overriddenActions.Values)
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
                    if(!overriddenActions.ContainsKey(action))
                    {
                        overriddenActions.Add(action, new ActionWriterOverride(action));
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
        public delegate bool StatusDel(ActionWriterOverride actionLabel, IMyTerminalBlock block, StringBuilder sb);

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

        bool Status_PB(ActionWriterOverride actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_Timer(ActionWriterOverride actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_OxygenGenerator(ActionWriterOverride actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_GasTank(ActionWriterOverride actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_Weapons(ActionWriterOverride actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_Warhead(ActionWriterOverride actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_MotorStator(ActionWriterOverride actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_Suspension(ActionWriterOverride actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_Piston(ActionWriterOverride actionLabel, IMyTerminalBlock block, StringBuilder sb)
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

        bool Status_Connector(ActionWriterOverride actionLabel, IMyTerminalBlock block, StringBuilder sb)
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
}
