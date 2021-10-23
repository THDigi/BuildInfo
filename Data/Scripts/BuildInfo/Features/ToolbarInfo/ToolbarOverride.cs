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
        int RehookForSeconds = 10;

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
            IMyTerminalBlock block = slimBlock.FatBlock as IMyTerminalBlock;
            if(block == null)
                return;

            if(CheckedTypes.Contains(block.GetType()))
                return;

            QueuedTypes.Enqueue(new QueuedActionGet(Main.Tick + 60, block.GetType()));
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void UpdateAfterSim(int tick)
        {
            if(RehookForSeconds > 0 && tick % Constants.TICKS_PER_SECOND == 0)
            {
                // HACK: must register late as possible
                MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;
                MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;
                RehookForSeconds--;
            }

            while(QueuedTypes.Count > 0 && QueuedTypes.Peek().ReadAtTick <= tick)
            {
                QueuedActionGet data = QueuedTypes.Dequeue();

                // no remove from CheckedType, any new real-time-added actions should be caught by the CustomActionGetter... unless it's only used in a group.

                // HACK: CustomActionGetter event doesn't get triggered for groups, making them undetectable until you see that action for a single block.
                // HACK: GetActions()'s collect function gets called before the toolbar check, allowing to get all actions.
                // HACK: can't call it in BlockAdded because it can make some mods' terminal controls vanish...
                MyAPIGateway.TerminalActionsHelper.GetActions(data.BlockType, null, CollectActionFunc);
            }

            if(RehookForSeconds <= 0 && QueuedTypes.Count <= 0)
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

            // TODO: make custom icons toggleable
            // needs a way to refresh toolbar...
            //if(MyAPIGateway.Input.IsNewKeyPressed(VRage.Input.MyKeys.L) && MyAPIGateway.Input.IsAnyCtrlKeyPressed())
            //{
            //    foreach(ActionWrapper wrapper in ActionWrappers.Values)
            //    {
            //        if(wrapper.CustomIcon == null)
            //            continue;
            //
            //        if(wrapper.Action.Icon == wrapper.CustomIcon)
            //            wrapper.Action.Icon = wrapper.OriginalIcon;
            //        else
            //            wrapper.Action.Icon = wrapper.CustomIcon;
            //    }
            //
            //    MyAPIGateway.Utilities.ShowNotification("Toggled action icons", 2000, "Debug");
            //}
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
            IMyTerminalAction action = (IMyTerminalAction)a;

            if(!ActionWrappers.ContainsKey(action))
            {
                ActionWrapper wrapper = new ActionWrapper(action);
                ActionWrappers.Add(action, wrapper);

                // TODO: add a way to revert icons... and maybe an option to remove them entirely?

                if(string.IsNullOrEmpty(action.Icon))
                {
                    // HACK: giving an icon for some iconless actions
                    switch(action.Id)
                    {
                        case "Attach": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Attach.dds"); break;
                        case "Detach": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Detach.dds"); break;
                        default: Log.Info($"Warning: Action id '{action.Id}' has no icon, this mod could give it one... tell author :P"); break;
                    }
                }
                else
                {
                    // affect mods too
                    if(action.Id.StartsWith("Increase"))
                    {
                        action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Add.dds");
                        return false;
                    }

                    if(action.Id.StartsWith("Decrease"))
                    {
                        action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Subtract.dds");
                        return false;
                    }

                    if(action.Id.StartsWith("Reset"))
                    {
                        action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Reset.dds");
                        return false;
                    }

                    // TODO: make this feature optional
                    // TODO: get rid of PNGs!
                    // HACK: replaces some icons with more descriptive/unique ones
                    switch(action.Id)
                    {
                        case "OnOff_On": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\TurnOn.dds"); break;
                        case "OnOff_Off": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\TurnOff.dds"); break;
                        case "OnOff": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\ToggleEnabled.dds"); break;

                        case "ShowOnHUD": action.Icon = @"Textures\GUI\Icons\Actions\SmallShipToggle.dds"; break;
                        case "ShowOnHUD_On": action.Icon = @"Textures\GUI\Icons\Actions\SmallShipSwitchOn.dds"; break;
                        case "ShowOnHUD_Off": action.Icon = @"Textures\GUI\Icons\Actions\SmallShipSwitchOff.dds"; break;

                        // matches connector and landing gear
                        case "SwitchLock": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\ToggleAttach.dds"); break;
                        case "Unlock": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Detach.dds"); break;
                        case "Lock": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Attach.dds"); break;
                        case "Autolock": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\ToggleAttach.dds"); break; // TODO: more unique icon?

                        case "PowerTransferOverride": action.Icon = @"Textures\GUI\Icons\HUD 2017\EnergyIcon.png"; break;

                        case "Trading": action.Icon = @"Textures\GUI\Icons\HUD 2017\MultiBlockBuilding.png"; break;
                        case "CollectAll": action.Icon = @"Textures\GUI\Icons\HUD 2017\MoveFurther.png"; break;
                        case "ThrowOut": action.Icon = @"Textures\GUI\Icons\HUD 2017\MoveCloser.png"; break;

                        case "Detonate": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Detonate.dds"); break;
                        case "Safety": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Detach.dds"); break;
                        case "StartCountdown": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\StartWarhead.dds"); break;
                        case "StopCountdown": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Stop.dds"); break;

                        case "PlaySound": action.Icon = @"Textures\GUI\Icons\HUD 2017\GridBroadcastingOn.png"; break;
                        case "StopSound": action.Icon = @"Textures\GUI\Icons\HUD 2017\GridBroadcastingOff.png"; break;

                        case "View":
                        case "Control": // applies to turrets and RC
                            action.Icon = @"Textures\GUI\Icons\Actions\StationSwitchOn.dds"; break;

                        case "ShootOnce":
                        case "Shoot":
                        case "Shoot_On":
                        case "Shoot_Off":
                            action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Shoot.dds"); break;

                        case "EnableIdleMovement":
                        case "EnableIdleMovement_On":
                        case "EnableIdleMovement_Off":
                            action.Icon = @"Textures\GUI\Icons\HUD 2017\RotationPlane.png"; break;

                        case "Run":
                        case "RunWithDefaultArgument":
                        case "Start":
                            action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Start.dds"); break;

                        case "Stop": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Stop.dds"); break;
                        case "TriggerNow": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\TriggerNow.dds"); break;
                        case "Silent": action.Icon = @"Textures\GUI\Icons\HUD 2017\ToggleBroadcasting.png"; break;

                        // applies to doors and parachute
                        case "Open": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Reverse.dds"); break;
                        case "Open_On": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Open.dds"); break;
                        case "Open_Off": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Close.dds"); break;
                        case "AnyoneCanUse": action.Icon = @"Textures\GUI\Icons\Actions\CharacterToggle.dds"; break;
                        case "AutoDeploy": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\AutoDeploy.dds"); break;

                        case "UseConveyor": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\ConveyorToggle.dds"); break;

                        case "helpOthers": action.Icon = @"Textures\GUI\Icons\HUD 2017\PlayerList.png"; break;

                        case "RotorLock":
                        case "HingeLock":
                            action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Lock.png"); break;

                        case "AddRotorTopPart":
                        case "AddSmallRotorTopPart":
                        case "AddHingeTopPart":
                        case "AddSmallHingeTopPart":
                        case "Add Top Part":
                            action.Icon = @"Textures\GUI\Icons\HUD 2017\plus.png";
                            break;

                        case "Force weld": action.Icon = @"Textures\GUI\Icons\WeaponWelder.dds"; break;

                        // matches rotors and pistons
                        case "ShareInertiaTensor": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Inertia.dds"); break;
                        case "Reverse": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Reverse.dds"); break;

                        case "Extend": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Add.dds"); break;
                        case "Retract": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Subtract.dds"); break;

                        case "Steering": action.Icon = @"Textures\GUI\Icons\HUD 2017\RadialMenu.png"; break;
                        case "Propulsion": action.Icon = @"Textures\GUI\Icons\HUD 2017\Jetpack.png"; break;
                        case "AirShock": action.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds"; break;
                        case "InvertSteering": action.Icon = @"Textures\GUI\Icons\Actions\Reset.dds"; break;
                        case "InvertPropulsion": action.Icon = @"Textures\GUI\Icons\Actions\Reset.dds"; break;
                        case "Braking":
                            action.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
                            action.Name.Clear().Append("Can Brake On/Off"); // sometimes an action name is so misleading that it must be renamed
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

                        case "Jump": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Jump.dds"); break;

                        // matches jumpdrive and battery
                        case "Recharge": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\RechargeOn.dds"); break;
                        case "Recharge_On": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\RechargeOn.dds"); break;
                        case "Recharge_Off": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\RechargeOff.dds"); break;

                        case "Discharge": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Detach.dds"); break;
                        case "Auto": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\TriggerNow.dds"); break;

                        case "DrainAll": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\Close.dds"); break;

                        case "Override": action.Icon = Utils.GetModFullPath(@"Textures\ActionIcons\TriggerNow.dds"); break;

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

                        case "MainCockpit": action.Icon = @"Textures\GUI\Icons\HUD 2017\PlayerHelmetOn.png"; break;
                        case "HorizonIndicator": action.Icon = @"Textures\GUI\Icons\HUD 2017\ToggleHud.png"; break;

                        case "DampenersOverride": action.Icon = @"Textures\GUI\Icons\HUD 2017\CloseSymmetrySetup.png"; break;
                        case "HandBrake": action.Icon = @"Textures\GUI\Icons\HUD 2017\HandbrakeCenter.png"; break;

                        case "ControlWheels": action.Icon = @"Textures\GUI\Icons\HUD 2017\RadialMenu.png"; break;
                        case "ControlGyros": action.Icon = @"Textures\GUI\Icons\HUD 2017\RotationPlane.png"; break;
                        case "ControlThrusters": action.Icon = @"Textures\GUI\Icons\HUD 2017\Jetpack.png"; break;

                        case "Park": action.Icon = @"Textures\GUI\Icons\HUD 2017\HandbrakeCenter.png"; break;

                        case "EnableParking": action.Icon = @"Textures\GUI\Icons\HUD 2017\HandbrakeCenter.png"; break;

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

                        // TODO: unchanged icons
                        case "Forward":
                        case "Backward":
                        case "Left":
                        case "Right":
                        case "Up":
                        case "Down":
                            break;

                        case "PreserveAspectRatio":
                            break;

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
                            break;

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
                            break;

                        default:
                            if(BuildInfoMod.IsDevMod)
                                Log.Info($"Unmodified icon for actionId='{action.Id}'; icon={action.Icon}");
                            break;
                    }
                }

                if(!string.IsNullOrEmpty(action.Icon))
                    wrapper.CustomIcon = action.Icon;
            }

            return false; // null list, never add to it.
        }
    }
}
