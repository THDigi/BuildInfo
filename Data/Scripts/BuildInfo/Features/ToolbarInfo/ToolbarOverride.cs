using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    /// <summary>
    /// Grabs all possible actions and wraps them in <see cref="ActionWrapper"/> which takes control over their Writer func.
    /// </summary>
    public class ToolbarOverride : ModComponent
    {
        public readonly Dictionary<IMyTerminalAction, ActionWrapper> ActionWrappers = new Dictionary<IMyTerminalAction, ActionWrapper>(16);
        readonly Func<ITerminalAction, bool> CollectActionFunc;

        public ToolbarOverride(BuildInfoMod main) : base(main)
        {
            CollectActionFunc = new Func<ITerminalAction, bool>(CollectAction);

            Main.BlockMonitor.BlockAdded += BlockAdded;
        }

        protected override void RegisterComponent()
        {
        }

        protected override void UnregisterComponent()
        {
            Main.BlockMonitor.BlockAdded -= BlockAdded;
        }

        void BlockAdded(IMySlimBlock slimBlock)
        {
            var block = slimBlock.FatBlock as IMyTerminalBlock;
            if(block == null)
                return;

            // HACK: CustomActionGetter event doesn't get triggered for groups, making them undetectable until you see that action for a single block.
            // HACK: GetActions()'s collect function gets called before the toolbar check, allowing to get all actions.
            block.GetActions(null, CollectActionFunc);
        }

        bool CollectAction(ITerminalAction a)
        {
            var action = (IMyTerminalAction)a;

            if(!ActionWrappers.ContainsKey(action))
            {
                ActionWrappers.Add(action, new ActionWrapper(action));

                if(string.IsNullOrEmpty(action.Icon))
                {
                    // HACK: giving an icon for some iconless actions
                    switch(action.Id)
                    {
                        case "Attach": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Attach.png"); break;
                        case "Detach": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Detach.png"); break;
                        default: Log.Info($"Warning: Action id '{action.Id}' has no icon, this mod could give it one... tell author :P"); break;
                    }
                }
                else
                {
                    // TODO: make this feature optional
                    // HACK: replace some icons with more descriptive/unique ones
                    switch(action.Id)
                    {
                        case "ShowOnHUD": action.Icon = @"Textures\GUI\Icons\Actions\SmallShipToggle.dds"; break;
                        case "ShowOnHUD_On": action.Icon = @"Textures\GUI\Icons\Actions\SmallShipSwitchOn.dds"; break;
                        case "ShowOnHUD_Off": action.Icon = @"Textures\GUI\Icons\Actions\SmallShipSwitchOff.dds"; break;

                        // NOTE: matches connector and landing gear
                        case "SwitchLock": Utils.GetModFullPath(@"Textures\ActionIcons\Attach.png"); break;
                        case "Unlock": Utils.GetModFullPath(@"Textures\ActionIcons\Detach.png"); break;
                        case "Lock": Utils.GetModFullPath(@"Textures\ActionIcons\Attach.png"); break;
                        case "Autolock": action.Icon = @"Textures\GUI\Icons\HUD 2017\ToggleConnectors.png"; break;

                        case "Trading": action.Icon = @"Textures\GUI\Icons\HUD 2017\MultiBlockBuilding.png"; break;
                        case "CollectAll": action.Icon = @"Textures\GUI\Icons\HUD 2017\MoveFurther.png"; break;
                        case "ThrowOut": action.Icon = @"Textures\GUI\Icons\HUD 2017\MoveCloser.png"; break;

                        case "Detonate": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Detonate.png"); break;
                        case "Safety": action.Icon = @"Textures\GUI\Icons\HUD 2017\ToggleConnectors.png"; break;
                        case "StartCountdown": action.Icon = @"Textures\GUI\Icons\HUD 2017\Notification_badge.png"; break;
                        case "StopCountdown": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Stop.png"); break;

                        case "PlaySound": action.Icon = @"Textures\GUI\Icons\HUD 2017\GridBroadcastingOn.png"; break;
                        case "StopSound": action.Icon = @"Textures\GUI\Icons\HUD 2017\GridBroadcastingOff.png"; break;

                        case "View": action.Icon = @"Textures\GUI\Icons\Actions\StationSwitchOn.dds"; break;

                        case "Control": action.Icon = @"Textures\GUI\Icons\Actions\StationSwitchOn.dds"; break;

                        case "Shoot_Once": action.Icon = @"Textures\GUI\Icons\HUD 2017\GravityIndicatorCircles.png"; break;
                        case "Shoot": action.Icon = @"Textures\GUI\Icons\HUD 2017\GravityIndicatorCircles.png"; break;
                        case "Shoot_On": action.Icon = @"Textures\GUI\Icons\HUD 2017\GravityIndicatorCircles.png"; break;
                        case "Shoot_Off": action.Icon = @"Textures\GUI\Icons\HUD 2017\GravityIndicatorCircleLarge.png"; break;

                        case "EnableIdleMovement": action.Icon = @"Textures\GUI\Icons\HUD 2017\RotationPlane.png"; break;
                        case "EnableIdleMovement_On": action.Icon = @"Textures\GUI\Icons\HUD 2017\RotationPlane.png"; break;
                        case "EnableIdleMovement_Off": action.Icon = @"Textures\GUI\Icons\HUD 2017\RotationPlane.png"; break;

                        case "Start": action.Icon = @"Textures\GUI\Icons\Actions\Start.dds"; break;
                        case "Stop": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Stop.png"); break;
                        case "TriggerNow": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\TriggerNow.png"); break;

                        // NOTE: applies to doors and parachute
                        case "Open": action.Icon = @"Textures\GUI\Icons\Actions\Reverse.dds"; break;
                        //case "Open": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\OpenToggle.png"); break;
                        case "Open_On": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Open.png"); break;
                        case "Open_Off": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Close.png"); break;

                        case "AnyoneCanUse": action.Icon = @"Textures\GUI\Icons\Actions\CharacterToggle.dds"; break;

                        case "UseConveyor": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\ConveyorToggle.png"); break;

                        case "AutoDeploy": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\AutoDeploy.png"); break;

                        case "ShareInertiaTensor": action.Icon = @"Textures\GUI\Icons\HUD 2017\SymmetrySetupRadial.png"; break;

                        case "Steering": action.Icon = @"Textures\GUI\Icons\HUD 2017\RadialMenu.png"; break;
                        case "Propulsion": action.Icon = @"Textures\GUI\Icons\HUD 2017\Jetpack.png"; break;
                        //case "Braking": action.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds"; break;
                        case "AirShock": action.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds"; break;
                        case "InvertSteering": action.Icon = @"Textures\GUI\Icons\Actions\Reset.dds"; break;
                        case "InvertPropulsion": action.Icon = @"Textures\GUI\Icons\Actions\Reset.dds"; break;

                        case "Refill": action.Icon = @"Textures\GUI\Icons\HUD 2017\PasteGrid.png"; break;
                        case "Auto-Refill": action.Icon = @"Textures\GUI\Icons\HUD 2017\PlacementMode.png"; break;

                        case "helpOthers": action.Icon = @"Textures\GUI\Icons\HUD 2017\PlayerList.png"; break;

                        case "Jump": action.Icon = @"Textures\GUI\Icons\HUD 2017\MoveFurther.png"; break;

                        // NOTE: matches jumpdrive and battery
                        case "Recharge": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\RechargeOn.png"); break;
                        case "Recharge_On": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\RechargeOn.png"); break;
                        case "Recharge_Off": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\RechargeOff.png"); break;

                        case "Discharge": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Detach.png"); break;
                        case "Auto": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\TriggerNow.png"); break;

                        case "DrainAll": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Close.png"); break;

                        case "Override": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\TriggerNow.png"); break;

                        case "MainRemoteControl": action.Icon = @"Textures\GUI\Icons\HUD 2017\PlayerHelmetOn.png"; break;

                        case "AutoPilot": action.Icon = @"Textures\GUI\Icons\Actions\SmallShipSwitchOn.dds"; break;
                        case "AutoPilot_On": action.Icon = @"Textures\GUI\Icons\Actions\SmallShipSwitchOn.dds"; break;
                        case "AutoPilot_Off": action.Icon = @"Textures\GUI\Icons\Actions\SmallShipSwitchOff.dds"; break;

                        case "CollisionAvoidance": action.Icon = @"Textures\GUI\Icons\Actions\MovingObjectSwitchOn.dds"; break;
                        case "CollisionAvoidance_On": action.Icon = @"Textures\GUI\Icons\Actions\MovingObjectSwitchOn.dds"; break;
                        case "CollisionAvoidance_Off": action.Icon = @"Textures\GUI\Icons\Actions\MovingObjectSwitchOff.dds"; break;

                        case "DockingMode": action.Icon = @"Textures\GUI\Icons\Actions\StationSwitchOn.dds"; break;
                        case "DockingMode_On": action.Icon = @"Textures\GUI\Icons\Actions\StationSwitchOn.dds"; break;
                        case "DockingMode_Off": action.Icon = @"Textures\GUI\Icons\Actions\StationSwitchOff.dds"; break;

                        //case "Forward": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\AutoDeploy.png"); break;
                        //case "Backward": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\AutoDeploy.png"); break;
                        //case "Left": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\AutoDeploy.png"); break;
                        //case "Right": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\AutoDeploy.png"); break;
                        //case "Up": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\AutoDeploy.png"); break;
                        //case "Down": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\AutoDeploy.png"); break;

                        case "MainCockpit": action.Icon = @"Textures\GUI\Icons\HUD 2017\PlayerHelmetOn.png"; break;
                        case "HorizonIndicator": action.Icon = @"Textures\GUI\Icons\HUD 2017\ToggleHud.png"; break;

                        case "DampenersOverride": action.Icon = @"Textures\GUI\Icons\HUD 2017\CloseSymmetrySetup.png"; break;
                        case "HandBrake": action.Icon = @"Textures\GUI\Icons\HUD 2017\HandbrakeCenter.png"; break;

                        case "ControlWheels": action.Icon = @"Textures\GUI\Icons\HUD 2017\RadialMenu.png"; break;
                        case "ControlGyros": action.Icon = @"Textures\GUI\Icons\HUD 2017\RotationPlane.png"; break;
                        case "ControlThrusters": action.Icon = @"Textures\GUI\Icons\HUD 2017\Jetpack.png"; break;

                        case "slaveMode": action.Icon = @"Textures\GUI\Icons\HUD 2017\MultiBlockBuilding.png"; break;

                        case "Idle": action.Icon = @"Textures\GUI\Icons\HUD 2017\GridBroadcastingOff.png"; break;
                        case "PasteGpsCoords": action.Icon = @"Textures\GUI\Icons\HUD 2017\Chat.png"; break;
                        case "ConnectGPS": action.Icon = @"Textures\GUI\Icons\HUD 2017\GridBroadcastingOn.png"; break;
                        // not visible?
                        //case "isPerm": action.Icon = @"Textures\GUI\Icons\Actions\CharacterSwitchOn.dds"; break;

                        case "EnableBroadCast": action.Icon = @"Textures\GUI\Icons\HUD 2017\SignalMode.png"; break;
                        case "ShowShipName": action.Icon = @"Textures\GUI\Icons\HUD 2017\AdminMenu.png"; break;
                    }
                }
            }

            return false; // null list, never add to it.
        }
    }
}
