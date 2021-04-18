using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
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
        readonly Queue<QueuedActionGet> QueuedTypes = new Queue<QueuedActionGet>();
        HashSet<Type> CheckedTypes = new HashSet<Type>();

        struct QueuedActionGet
        {
            public readonly int ReadAtTick;
            public readonly Type BlockType;

            public QueuedActionGet(int readAtTick, Type blockType)
            {
                ReadAtTick = readAtTick;
                BlockType = blockType;
            }
        }

        public ToolbarOverride(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;

            CollectActionFunc = new Func<ITerminalAction, bool>(CollectAction);

            Main.BlockMonitor.BlockAdded += BlockAdded;
            MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            Main.BlockMonitor.BlockAdded -= BlockAdded;

            if(!Main.ComponentsRegistered)
                return;

            MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;
        }

        void BlockAdded(IMySlimBlock slimBlock)
        {
            var block = slimBlock.FatBlock as IMyTerminalBlock;
            if(block == null)
                return;

            if(CheckedTypes.Contains(block.GetType()))
                return;

            QueuedTypes.Enqueue(new QueuedActionGet(Main.Tick + 60, block.GetType()));
        }

        public override void UpdateAfterSim(int tick)
        {
            if(tick % Constants.TICKS_PER_SECOND == 0)
            {
                // HACK: must register late as possible
                MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;
                MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;
            }

            while(QueuedTypes.Count > 0 && QueuedTypes.Peek().ReadAtTick <= tick)
            {
                var data = QueuedTypes.Dequeue();

                // no remove from CheckedType, any new real-time-added actions should be caught by the CustomActionGetter... unless it's only used in a group.

                // HACK: CustomActionGetter event doesn't get triggered for groups, making them undetectable until you see that action for a single block.
                // HACK: GetActions()'s collect function gets called before the toolbar check, allowing to get all actions.
                // HACK: can't call it in BlockAdded because it can make some mods' terminal controls vanish...
                MyAPIGateway.TerminalActionsHelper.GetActions(data.BlockType, null, CollectActionFunc);
            }
        }

        void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            // required to catch mod actions that are only added to this event
            for(int i = 0; i < actions.Count; i++)
            {
                CollectAction(actions[i]);
            }
        }

        bool CollectAction(ITerminalAction a)
        {
            var action = (IMyTerminalAction)a;

            if(!ActionWrappers.ContainsKey(action))
            {
                ActionWrappers.Add(action, new ActionWrapper(action));

                // TODO: add a way to revert icons... and maybe an option to remove them entirely?

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
                    // affect mods too
                    if(action.Id.StartsWith("Increase"))
                    {
                        action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Add.png");
                        return false;
                    }

                    if(action.Id.StartsWith("Decrease"))
                    {
                        action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Subtract.png");
                        return false;
                    }

                    if(action.Id.StartsWith("Reset"))
                    {
                        action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Reset.png");
                        return false;
                    }

                    // TODO: make this feature optional
                    // HACK: replace some icons with more descriptive/unique ones
                    switch(action.Id)
                    {
                        case "OnOff_On": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\TurnOn.png"); break;
                        case "OnOff_Off": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\TurnOff.png"); break;
                        case "OnOff":
                            action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\ToggleEnabled.png"); break;

                        case "ShowOnHUD": action.Icon = @"Textures\GUI\Icons\Actions\SmallShipToggle.dds"; break;
                        case "ShowOnHUD_On": action.Icon = @"Textures\GUI\Icons\Actions\SmallShipSwitchOn.dds"; break;
                        case "ShowOnHUD_Off": action.Icon = @"Textures\GUI\Icons\Actions\SmallShipSwitchOff.dds"; break;

                        // matches connector and landing gear
                        case "SwitchLock": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\ToggleAttach.png"); break;
                        case "Unlock": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Detach.png"); break;
                        case "Lock": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Attach.png"); break;
                        case "Autolock": action.Icon = @"Textures\GUI\Icons\HUD 2017\ToggleConnectors.png"; break;

                        case "Trading": action.Icon = @"Textures\GUI\Icons\HUD 2017\MultiBlockBuilding.png"; break;
                        case "CollectAll": action.Icon = @"Textures\GUI\Icons\HUD 2017\MoveFurther.png"; break;
                        case "ThrowOut": action.Icon = @"Textures\GUI\Icons\HUD 2017\MoveCloser.png"; break;

                        case "Detonate": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Detonate.png"); break;
                        case "Safety": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Detach.png"); break;
                        case "StartCountdown": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\StartWarhead.png"); break;
                        case "StopCountdown": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Stop.png"); break;

                        case "PlaySound": action.Icon = @"Textures\GUI\Icons\HUD 2017\GridBroadcastingOn.png"; break;
                        case "StopSound": action.Icon = @"Textures\GUI\Icons\HUD 2017\GridBroadcastingOff.png"; break;

                        case "View":
                        case "Control": // applies to turrets and RC
                            action.Icon = @"Textures\GUI\Icons\Actions\StationSwitchOn.dds"; break;

                        case "ShootOnce":
                        case "Shoot":
                        case "Shoot_On":
                        case "Shoot_Off":
                            action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Shoot.png"); break;

                        case "EnableIdleMovement":
                        case "EnableIdleMovement_On":
                        case "EnableIdleMovement_Off":
                            action.Icon = @"Textures\GUI\Icons\HUD 2017\RotationPlane.png"; break;

                        case "Run":
                        case "RunWithDefaultArgument":
                        case "Start":
                            action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Start.png"); break;

                        case "Stop": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Stop.png"); break;
                        case "TriggerNow": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\TriggerNow.png"); break;
                        case "Silent": action.Icon = @"Textures\GUI\Icons\HUD 2017\ToggleBroadcasting.png"; break;

                        // applies to doors and parachute
                        case "Open": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Reverse.png"); break;
                        case "Open_On": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Open.png"); break;
                        case "Open_Off": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Close.png"); break;
                        case "AnyoneCanUse": action.Icon = @"Textures\GUI\Icons\Actions\CharacterToggle.dds"; break;
                        case "AutoDeploy": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\AutoDeploy.png"); break;

                        case "UseConveyor": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\ConveyorToggle.png"); break;

                        case "helpOthers": action.Icon = @"Textures\GUI\Icons\HUD 2017\PlayerList.png"; break;

                        case "RotorLock":
                        case "HingeLock":
                            action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Lock.png"); break;

                        // matches rotors and pistons
                        case "ShareInertiaTensor": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Inertia.png"); break;
                        case "Reverse": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Reverse.png"); break;

                        case "Extend": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Add.png"); break;
                        case "Retract": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Subtract.png"); break;

                        case "Steering": action.Icon = @"Textures\GUI\Icons\HUD 2017\RadialMenu.png"; break;
                        case "Propulsion": action.Icon = @"Textures\GUI\Icons\HUD 2017\Jetpack.png"; break;
                        case "AirShock": action.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds"; break;
                        case "InvertSteering": action.Icon = @"Textures\GUI\Icons\Actions\Reset.dds"; break;
                        case "InvertPropulsion": action.Icon = @"Textures\GUI\Icons\Actions\Reset.dds"; break;
                        case "Braking":
                            action.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
                            action.Name.Clear().Append("Can Brake On/Off"); // sometimes an action is so misleading that it must be renamed
                            break;

                        // matches gas generator and gas tank
                        case "Refill": action.Icon = @"Textures\GUI\Icons\HUD 2017\PasteGrid.png"; break;
                        case "Auto-Refill": action.Icon = @"Textures\GUI\Icons\HUD 2017\PlacementMode.png"; break;

                        case "Stockpile":
                        case "Stockpile_On":
                        case "Stockpile_Off":
                            action.Icon = @"Textures\GUI\Icons\HUD 2017\MultiBlockBuilding.png"; break;

                        case "Depressurize":
                        case "Depressurize_On":
                        case "Depressurize_Off":
                            action.Icon = @"Textures\GUI\Icons\HUD 2017\SpawnMenu.png"; break;

                        case "Jump": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Jump.png"); break;

                        // matches jumpdrive and battery
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
                        case "isPerm": action.Icon = @"Textures\GUI\Icons\Actions\CharacterSwitchOn.dds"; break; // visible only when connection is active

                        case "BroadcastUsingAntennas": // ore detector
                        case "EnableBroadCast": // antenna and space ball
                            action.Icon = @"Textures\GUI\Icons\HUD 2017\SignalMode.png"; break;
                        case "ShowShipName": action.Icon = @"Textures\GUI\Icons\HUD 2017\AdminMenu.png"; break;

                        case "KeepProjection": action.Icon = @"Textures\GUI\Icons\HUD 2017\PasteGrid.png"; break;
                        case "SpawnProjection": action.Icon = @"Textures\GUI\Icons\HUD 2017\PlacementMode.png"; break;
                    }
                }
            }

            return false; // null list, never add to it.
        }
    }
}
