﻿using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    public class ActionWrapper
    {
        public readonly IMyTerminalAction Action;
        public readonly string DisplayName;
        public readonly string OriginalIcon;
        public readonly string CustomIcon;

        public Action<IMyTerminalBlock, StringBuilder> OriginalWriter { get; private set; }

        private readonly Action<IMyTerminalBlock, StringBuilder> CustomWriter;

        public ActionWrapper(IMyTerminalAction action)
        {
            Action = action;

            OriginalIcon = Action.Icon;
            CustomIcon = GetCustomActionIcon(Action) ?? OriginalIcon;
            UpdateIcon();

            OriginalWriter = Action.Writer;
            CustomWriter = NewWriter;

            Action.Writer = CustomWriter;

            EditActionName(action);
            DisplayName = Action.Name.ToString();
        }

        void NewWriter(IMyTerminalBlock block, StringBuilder sb)
        {
            try
            {
                if(block == null || block.MarkedForClose || sb == null)
                    return;

                // not really necessary...
                //var controlled = Main.ToolbarMonitor.ControlledBlock;
                //if(controlled == null || !MyAPIGateway.GridGroups.HasConnection(controlled.CubeGrid, block.CubeGrid, GridLinkTypeEnum.Logical))
                //    return;

                // HACK: not overriding status when in GUI because it can be for timers/other toolbars and no idea which is which...
                // TODO: maybe find a way to detect them and maybe even label events slots for airvent and such...
                // Also no status override for gamepad HUD because it doesn't sync with the rest of the system so won't work.
                if((MyAPIGateway.Gui.IsCursorVisible && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
                || (!ToolbarMonitor.EnableGamepadSupport && MyAPIGateway.Input.IsJoystickLastUsed))
                {
                    AppendOriginalStatus(block, sb);
                    return;
                }

                ToolbarMonitor toolbarMonitor = BuildInfoMod.Instance.ToolbarMonitor;

                if(ToolbarMonitor.DebugLogging)
                    Log.Info($"Writer :: action={Action.Id}; block={block.CustomName}; wrapperSlotIndex={toolbarMonitor.WrapperSlotIndex.ToString()}");

                int max = toolbarMonitor.SequencedItems.Count;
                int num = toolbarMonitor.WrapperSlotIndex;
                if(num >= max)
                    return;

                // HACK: find next matching slot, it could not match if a mod adds actions via event which won't have this status override class
                // other issues are PBs or timer blocks calling actions and causing the writer to get triggered, desynchronizing the order.
                ToolbarItem toolbarItem = toolbarMonitor.SequencedItems[num];
                while(toolbarItem.ActionId != Action.Id || toolbarItem.BlockEntId != block.EntityId)
                {
                    num++;
                    if(num >= max)
                        return;

                    toolbarItem = toolbarMonitor.SequencedItems[num];
                }

                // writers get called in sequence that they are in the toolbar so this should pair them exactly
                toolbarMonitor.WrapperSlotIndex = num + 1;

                if(toolbarMonitor.ToolbarPage != (toolbarItem.Index / ToolbarMonitor.SlotsPerPage))
                    return;

                // update some properties that are easily accessible in this context.
                if(toolbarItem.ActionWrapper == null)
                {
                    toolbarItem.ActionWrapper = this;
                    toolbarItem.Block = block;
                    toolbarItem.OriginalName = toolbarItem.CustomLabel ?? toolbarItem.GroupId ?? block.CustomName;
                    toolbarItem.ActionName = GetCustomActionName(Action, block) ?? DisplayName;

                    if(ToolbarMonitor.DebugLogging)
                        Log.Info($" ^-- filled data for slot #{toolbarItem.Index.ToString()}; name={toolbarItem.OriginalName}");
                }

                sb.AppendStringBuilder(toolbarItem.StatusSB);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public void AppendOriginalStatus(IMyTerminalBlock block, StringBuilder sb)
        {
            try
            {
                OriginalWriter?.Invoke(block, sb);
            }
            catch(Exception)
            {
                // HACK invoking original Writer on any action that has no writer throws NRE inside the game code, undetectable in a graceful way.
                OriginalWriter = null;
            }
        }

        public void UpdateIcon()
        {
            ActionIconsMode mode = ActionIconsMode.Original;
            ConfigLib.EnumSetting<ActionIconsMode> setting = BuildInfoMod.Instance?.Config?.ToolbarActionIcons;
            if(setting != null)
                mode = setting.ValueEnum;
            else if(BuildInfoMod.IsDevMod)
                Log.Error($"[DEV] Config is null in {GetType().Name}.{nameof(UpdateIcon)} for actionId={Action.Id}; icon={OriginalIcon}");

            switch(mode)
            {
                case ActionIconsMode.Hidden: Action.Icon = string.Empty; break; // null makes icons occupy the space while "" makes them not exist at all.
                case ActionIconsMode.Original: Action.Icon = OriginalIcon; break;
                case ActionIconsMode.Custom: Action.Icon = CustomIcon; break;
            }
        }

        static string GetDirectionTranslated(string direction)
        {
            switch(direction)
            {
                case "Forward": return MyTexts.GetString("Thrust_Forward");
                case "Backward": return MyTexts.GetString("Thrust_Back");
                case "Left": return MyTexts.GetString("Thrust_Left");
                case "Right": return MyTexts.GetString("Thrust_Right");
                case "Up": return MyTexts.GetString("Thrust_Up");
                case "Down": return MyTexts.GetString("Thrust_Down");
                default: return direction;
            }
        }

        // HACK: edit the action's name everywhere, for the cases where the original name is so bad...
        static void EditActionName(IMyTerminalAction action)
        {
            switch(action.Id)
            {
                case "Braking":
                    // HACK: new SB because otherwise it modifies the language key's text
                    action.Name = new StringBuilder("Can Brake On/Off");
                    break;

                case "Forward":
                case "Backward":
                case "Left":
                case "Right":
                case "Up":
                case "Down":
                {
                    string label = MyTexts.GetString("BlockPropertyTitle_ForwardDirection") ?? "Forward Direction";
                    string dirName = GetDirectionTranslated(action.Id);

                    // HACK: new SB because otherwise it modifies the language key's text
                    action.Name = new StringBuilder(label.Length + 2 + dirName.Length).Append(label).Append(": ").Append(dirName);
                    break;
                }
            }
        }

        // only used to rename actions in toolbar info box, does not change the action itself.
        static string GetCustomActionName(IMyTerminalAction action, IMyTerminalBlock block)
        {
            if(block is IMyRemoteControl)
            {
                switch(action.Id)
                {
                    case "Forward":
                    case "Backward":
                    case "Left":
                    case "Right":
                    case "Up":
                    case "Down":
                    {
                        string label = MyTexts.GetString("BlockPropertyTitle_ForwardDirection") ?? "Forward Direction";
                        string dirName = GetDirectionTranslated(action.Id);
                        return $"{label}: {dirName}";
                    }
                }
            }

            if(block is IMyShipGrinder)
            {
                switch(action.Id)
                {
                    case "OnOff": return "Grind On/Off";
                    case "OnOff_On": return "Start grinding";
                    case "OnOff_Off": return "Stop grinding";
                }
            }

            if(block is IMyShipWelder)
            {
                switch(action.Id)
                {
                    case "OnOff": return "Weld On/Off";
                    case "OnOff_On": return "Start welding";
                    case "OnOff_Off": return "Stop welding";
                }
            }

            if(block is IMyShipDrill)
            {
                switch(action.Id)
                {
                    case "OnOff": return "Drill On/Off";
                    case "OnOff_On": return "Start drilling";
                    case "OnOff_Off": return "Stop drilling";
                }
            }

            //if(block is IMyParachute)
            //{
            //    switch(action.Id)
            //    {
            //        case "Open": return "Toggle Deploy";
            //        case "Open_On": return "Deploy";
            //        case "Open_Off": return "Close";
            //    }
            //}

            // applies to all blocks
            //switch(action.Id)
            //{
            //    case "OnOff": return "On/Off";
            //    case "OnOff_On": return "Turn On";
            //    case "OnOff_Off": return "Turn Off";

            //    case "ShowOnHUD": return "Show/Hide on HUD";
            //    case "ShowOnHUD_On": return "Show on HUD";
            //    case "ShowOnHUD_Off": return "Hide from HUD";

            //    case "Shoot": return "Toggle Shoot";
            //    case "Shoot_On": return "Start shooting";
            //    case "Shoot_Off": return "Stop shooting";

            //    case "Open": return "Toggle Open";
            //    case "Open_On": return "Open";
            //    case "Open_Off": return "Close";

            //    case "AutoDeploy": return "Toggle Auto-Deploy";

            //    case "UseConveyor": return "Toggle Use Conveyor";

            //    case "RunWithDefaultArgument": return "Run (no args)";
            //}

            return null;
        }

        static string GetCustomActionIcon(IMyTerminalAction action)
        {
            // HACK: giving an icon for some iconless actions
            if(string.IsNullOrEmpty(action.Icon))
            {
                switch(action.Id)
                {
                    case "Attach": return Utils.GetModFullPath(@"Textures\ActionIcons\Attach.dds");
                    case "Detach": return Utils.GetModFullPath(@"Textures\ActionIcons\Detach.dds");

                    default: Log.Info($"Action id '{action.Id}' has no icon, this mod could give it one... tell author :P"); break;
                }

                return null;
            }

            // don't change other mod's custom icons
            if(!action.Icon.StartsWith(@"Textures\GUI\Icons\Actions\", StringComparison.OrdinalIgnoreCase))
                return null;

            #region replace by id prefix
            if(action.Id.StartsWith("Increase"))
                return Utils.GetModFullPath(@"Textures\ActionIcons\Add.dds");

            if(action.Id.StartsWith("Decrease"))
                return Utils.GetModFullPath(@"Textures\ActionIcons\Subtract.dds");

            if(action.Id.StartsWith("Reset"))
                return Utils.GetModFullPath(@"Textures\ActionIcons\Reset.dds");
            #endregion

            #region replace by action id
            switch(action.Id)
            {
                case "OnOff_On": return Utils.GetModFullPath(@"Textures\ActionIcons\TurnOn.dds");
                case "OnOff_Off": return Utils.GetModFullPath(@"Textures\ActionIcons\TurnOff.dds");
                case "OnOff": return Utils.GetModFullPath(@"Textures\ActionIcons\ToggleEnabled.dds");

                case "ShowOnHUD": return Utils.GetModFullPath(@"Textures\ActionIcons\ShowOnHud_Toggle.dds");
                case "ShowOnHUD_On": return Utils.GetModFullPath(@"Textures\ActionIcons\ShowOnHud_On.dds");
                case "ShowOnHUD_Off": return Utils.GetModFullPath(@"Textures\ActionIcons\ShowOnHud_Off.dds");

                // matches connector and landing gear
                case "SwitchLock": return Utils.GetModFullPath(@"Textures\ActionIcons\ToggleLock.dds");
                case "Unlock": return Utils.GetModFullPath(@"Textures\ActionIcons\Unlock.dds");
                case "Lock": return Utils.GetModFullPath(@"Textures\ActionIcons\Lock.dds");
                case "Autolock": return Utils.GetModFullPath(@"Textures\ActionIcons\AutoLock.dds");

                case "PowerTransferOverride": return Utils.GetModFullPath(@"Textures\ActionIcons\Energy.dds");

                case "Trading": return Utils.GetModFullPath(@"Textures\ActionIcons\Trading.dds");
                case "CollectAll": return Utils.GetModFullPath(@"Textures\ActionIcons\CollectAll.dds");
                case "ThrowOut": return Utils.GetModFullPath(@"Textures\ActionIcons\ThrowOut.dds");

                case "Detonate": return Utils.GetModFullPath(@"Textures\ActionIcons\Detonate.dds");
                case "Safety": return Utils.GetModFullPath(@"Textures\ActionIcons\Detach.dds");
                case "StartCountdown": return Utils.GetModFullPath(@"Textures\ActionIcons\StartWarhead.dds");
                case "StopCountdown": return Utils.GetModFullPath(@"Textures\ActionIcons\StopButton.dds");

                case "PlaySound": return Utils.GetModFullPath(@"Textures\ActionIcons\PlayButton.dds");
                case "StopSound": return Utils.GetModFullPath(@"Textures\ActionIcons\StopButton.dds");

                case "View": return Utils.GetModFullPath(@"Textures\ActionIcons\ViewCamera.dds");
                case "Control": return Utils.GetModFullPath(@"Textures\ActionIcons\Control.dds"); // applies to turrets and RC

                case "ShootOnce": return Utils.GetModFullPath(@"Textures\ActionIcons\ShootOnce.dds");
                case "Shoot": return Utils.GetModFullPath(@"Textures\ActionIcons\ShootOn.dds");
                case "Shoot_On": return Utils.GetModFullPath(@"Textures\ActionIcons\ShootOn.dds");
                case "Shoot_Off": return Utils.GetModFullPath(@"Textures\ActionIcons\ShootOff.dds");

                case "EnableIdleMovement":
                case "EnableIdleMovement_On":
                case "EnableIdleMovement_Off":
                    return Utils.GetModFullPath(@"Textures\ActionIcons\Rotation.dds");

                case "Run": return Utils.GetModFullPath(@"Textures\ActionIcons\Script.dds");

                case "RunWithDefaultArgument":
                case "Start":
                    return Utils.GetModFullPath(@"Textures\ActionIcons\PlayButton.dds");

                case "Stop": return Utils.GetModFullPath(@"Textures\ActionIcons\StopButton.dds");
                case "TriggerNow": return Utils.GetModFullPath(@"Textures\ActionIcons\TriggerNow.dds");
                case "Silent": return Utils.GetModFullPath(@"Textures\ActionIcons\ToggleSilent.dds");

                // applies to doors and parachute
                case "Open": return Utils.GetModFullPath(@"Textures\ActionIcons\Reverse.dds");
                case "Open_On": return Utils.GetModFullPath(@"Textures\ActionIcons\Open.dds");
                case "Open_Off": return Utils.GetModFullPath(@"Textures\ActionIcons\Close.dds");
                case "AnyoneCanUse": return Utils.GetModFullPath(@"Textures\ActionIcons\Multiplayer.dds");
                case "AutoDeploy": return Utils.GetModFullPath(@"Textures\ActionIcons\AutoDeploy.dds");

                case "UseConveyor": return Utils.GetModFullPath(@"Textures\ActionIcons\ConveyorToggle.dds");

                case "helpOthers": return Utils.GetModFullPath(@"Textures\ActionIcons\Multiplayer.dds");

                case "RotorLock":
                case "HingeLock":
                    return Utils.GetModFullPath(@"Textures\ActionIcons\Lock.dds");

                case "AddRotorTopPart":
                case "AddSmallRotorTopPart":
                case "AddHingeTopPart":
                case "AddSmallHingeTopPart":
                case "Add Top Part":
                    return Utils.GetModFullPath(@"Textures\ActionIcons\WheelSpawn.dds");

                case "Force weld": return Utils.GetModFullPath(@"Textures\ActionIcons\Lock.dds");

                // matches rotors and pistons
                case "ShareInertiaTensor": return Utils.GetModFullPath(@"Textures\ActionIcons\Inertia.dds");
                case "Reverse": return Utils.GetModFullPath(@"Textures\ActionIcons\Reverse.dds");

                case "Extend": return Utils.GetModFullPath(@"Textures\ActionIcons\Add.dds");
                case "Retract": return Utils.GetModFullPath(@"Textures\ActionIcons\Subtract.dds");

                case "Steering": return Utils.GetModFullPath(@"Textures\ActionIcons\WheelSteering.dds");
                case "Propulsion": return Utils.GetModFullPath(@"Textures\ActionIcons\WheelPropulsion.dds");
                case "AirShock": return Utils.GetModFullPath(@"Textures\ActionIcons\WheelSuspension.dds");
                case "InvertSteering": return Utils.GetModFullPath(@"Textures\ActionIcons\WheelInvertSteering.dds");
                case "InvertPropulsion": return Utils.GetModFullPath(@"Textures\ActionIcons\WheelInvertPropulsion.dds");
                case "Braking": return Utils.GetModFullPath(@"Textures\ActionIcons\WheelBrake.dds");

                // matches gas generator and gas tank
                case "Refill": return Utils.GetModFullPath(@"Textures\ActionIcons\Refill.dds");
                case "Auto-Refill": return Utils.GetModFullPath(@"Textures\ActionIcons\AutoRefill.dds");

                case "Stockpile":
                case "Stockpile_On":
                case "Stockpile_Off":
                    return Utils.GetModFullPath(@"Textures\ActionIcons\Stockpile.dds");

                case "Depressurize":
                case "Depressurize_On":
                case "Depressurize_Off":
                    return Utils.GetModFullPath(@"Textures\ActionIcons\CharacterHelmet.dds");

                case "Jump": return Utils.GetModFullPath(@"Textures\ActionIcons\Jump.dds");

                // matches jumpdrive and battery
                case "Recharge": return Utils.GetModFullPath(@"Textures\ActionIcons\RechargeOn.dds");
                case "Recharge_On": return Utils.GetModFullPath(@"Textures\ActionIcons\RechargeOn.dds");
                case "Recharge_Off": return Utils.GetModFullPath(@"Textures\ActionIcons\RechargeOff.dds");

                case "Discharge": return Utils.GetModFullPath(@"Textures\ActionIcons\Detach.dds");
                case "Auto": return Utils.GetModFullPath(@"Textures\ActionIcons\TriggerNow.dds");

                case "DrainAll": return Utils.GetModFullPath(@"Textures\ActionIcons\Close.dds");

                // gyro
                case "Override": return Utils.GetModFullPath(@"Textures\ActionIcons\Rotation.dds");

                case "AutoPilot":
                case "AutoPilot_On":
                case "AutoPilot_Off":
                    return Utils.GetModFullPath(@"Textures\ActionIcons\AI.dds");

                case "CollisionAvoidance":
                case "CollisionAvoidance_On":
                case "CollisionAvoidance_Off":
                    return Utils.GetModFullPath(@"Textures\ActionIcons\CollisionAvoidance.dds");

                case "DockingMode":
                case "DockingMode_On":
                case "DockingMode_Off":
                    return Utils.GetModFullPath(@"Textures\ActionIcons\Attach.dds");

                case "MainCockpit":
                case "MainRemoteControl":
                    return Utils.GetModFullPath(@"Textures\ActionIcons\CharacterHelmet.dds");

                case "HorizonIndicator": return Utils.GetModFullPath(@"Textures\ActionIcons\HorizonIndicator.dds");

                case "DampenersOverride": return Utils.GetModFullPath(@"Textures\ActionIcons\WheelSuspension.dds");

                // cockpit/RC
                case "HandBrake": return Utils.GetModFullPath(@"Textures\ActionIcons\TogglePark.dds");
                case "Park": return Utils.GetModFullPath(@"Textures\ActionIcons\Attach.dds");

                // used by connector/lg/wheels
                case "EnableParking": return Utils.GetModFullPath(@"Textures\ActionIcons\ParkAllowed.dds");

                case "ControlWheels": return Utils.GetModFullPath(@"Textures\ActionIcons\WheelPropulsion.dds");
                case "ControlGyros": return Utils.GetModFullPath(@"Textures\ActionIcons\Rotation.dds");
                case "ControlThrusters": return Utils.GetModFullPath(@"Textures\ActionIcons\Thruster.dds");

                // assembler
                case "slaveMode": return Utils.GetModFullPath(@"Textures\ActionIcons\CoopMode.dds");

                // laser antenna
                case "Idle": return Utils.GetModFullPath(@"Textures\ActionIcons\Detach.dds");
                case "PasteGpsCoords": return Utils.GetModFullPath(@"Textures\ActionIcons\Paste.dds");
                case "ConnectGPS": return Utils.GetModFullPath(@"Textures\ActionIcons\Broadcast.dds");
                case "isPerm": return Utils.GetModFullPath(@"Textures\ActionIcons\PermanentConnection.dds"); // visible only when connection is active

                case "BroadcastUsingAntennas": // ore detector
                case "EnableBroadCast": // antenna and space ball
                    return Utils.GetModFullPath(@"Textures\ActionIcons\Broadcast.dds");

                case "ShowShipName": return Utils.GetModFullPath(@"Textures\ActionIcons\Label.dds");

                case "KeepProjection": return Utils.GetModFullPath(@"Textures\ActionIcons\Save.dds");
                case "SpawnProjection": return Utils.GetModFullPath(@"Textures\ActionIcons\Paste.dds");

                case "PreserveAspectRatio": return Utils.GetModFullPath(@"Textures\ActionIcons\AspectRatio.dds");

                // TODO: unchanged icons
                case "Detect Players":
                case "Detect Players_On":
                case "Detect Players_Off":
                case "Detect Floating Objects":
                case "Detect Floating Objects_On":
                case "Detect Floating Objects_Off":
                case "Detect Small Ships":
                case "Detect Small Ships_On":
                case "Detect Small Ships_Off":
                case "Detect Large Ships":
                case "Detect Large Ships_On":
                case "Detect Large Ships_Off":
                case "Detect Stations":
                case "Detect Stations_On":
                case "Detect Stations_Off":
                case "Detect Subgrids":
                case "Detect Subgrids_On":
                case "Detect Subgrids_Off":
                case "Detect Asteroids":
                case "Detect Asteroids_On":
                case "Detect Asteroids_Off":
                case "Detect Owner":
                case "Detect Owner_On":
                case "Detect Owner_Off":
                case "Detect Friendly":
                case "Detect Friendly_On":
                case "Detect Friendly_Off":
                case "Detect Neutral":
                case "Detect Neutral_On":
                case "Detect Neutral_Off":
                case "Detect Enemy":
                case "Detect Enemy_On":
                case "Detect Enemy_Off":

                case "TargetMeteors":
                case "TargetMeteors_On":
                case "TargetMeteors_Off":
                case "TargetMissiles":
                case "TargetMissiles_On":
                case "TargetMissiles_Off":
                case "TargetSmallShips":
                case "TargetSmallShips_On":
                case "TargetSmallShips_Off":
                case "TargetLargeShips":
                case "TargetLargeShips_On":
                case "TargetLargeShips_Off":
                case "TargetCharacters":
                case "TargetCharacters_On":
                case "TargetCharacters_Off":
                case "TargetStations":
                case "TargetStations_On":
                case "TargetStations_Off":
                case "TargetNeutrals":
                case "TargetNeutrals_On":
                case "TargetNeutrals_Off":
                case "TargetFriends":
                case "TargetFriends_On":
                case "TargetFriends_Off":
                case "TargetEnemies":
                case "TargetEnemies_On":
                case "TargetEnemies_Off":

                case "TargetingGroup_Weapons":
                case "TargetingGroup_Propulsion":
                case "TargetingGroup_PowerSystems":
                case "TargetingGroup_CycleSubsystems":

                case "CopyTarget":
                case "ForgetTarget":

                case "Forward":
                case "Backward":
                case "Left":
                case "Right":
                case "Up":
                case "Down":
                    return null;
            }
            #endregion

            // ignore icons for all target group actions
            if(action.Id.StartsWith("TargetingGroup_"))
            {
                string targetId = action.Id.Substring("TargetingGroup_".Length);

                foreach(MyTargetingGroupDefinition targetGroup in MyDefinitionManager.Static.GetTargetingGroupDefinitions())
                {
                    if(targetId == targetGroup.Id.SubtypeName)
                        return null;
                }
            }

            #region replace by icon path
            if(action.Icon.Equals(@"Textures\GUI\Icons\Actions\Increase.dds", StringComparison.OrdinalIgnoreCase))
                return Utils.GetModFullPath(@"Textures\ActionIcons\Add.dds");

            if(action.Icon.Equals(@"Textures\GUI\Icons\Actions\Decrease.dds", StringComparison.OrdinalIgnoreCase))
                return Utils.GetModFullPath(@"Textures\ActionIcons\Subtract.dds");

            if(action.Icon.Equals(@"Textures\GUI\Icons\Actions\Reset.dds", StringComparison.OrdinalIgnoreCase))
                return Utils.GetModFullPath(@"Textures\ActionIcons\Reset.dds");

            if(action.Icon.Equals(@"Textures\GUI\Icons\Actions\Reverse.dds", StringComparison.OrdinalIgnoreCase))
                return Utils.GetModFullPath(@"Textures\ActionIcons\Reverse.dds");

            if(action.Icon.Equals(@"Textures\GUI\Icons\Actions\Reverse.dds", StringComparison.OrdinalIgnoreCase))
                return Utils.GetModFullPath(@"Textures\ActionIcons\Reverse.dds");

            if(action.Icon.Equals(@"Textures\GUI\Icons\Actions\Toggle.dds", StringComparison.OrdinalIgnoreCase))
                return Utils.GetModFullPath(@"Textures\ActionIcons\GenericToggle.dds");

            if(action.Icon.Equals(@"Textures\GUI\Icons\Actions\SwitchOn.dds", StringComparison.OrdinalIgnoreCase))
                return Utils.GetModFullPath(@"Textures\ActionIcons\GenericOn.dds");

            if(action.Icon.Equals(@"Textures\GUI\Icons\Actions\SwitchOff.dds", StringComparison.OrdinalIgnoreCase))
                return Utils.GetModFullPath(@"Textures\ActionIcons\GenericOff.dds");
            #endregion

            if(BuildInfoMod.IsDevMod)
                Log.Info($"[DEV] Unmodified icon for actionId='{action.Id}'; icon={action.Icon}");

            return null;
        }
    }
}
