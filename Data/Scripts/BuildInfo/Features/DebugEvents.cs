using System.Text;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Draygo.API;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features
{
    public class DebugEvents : ModComponent
    {
        public DebugEvents(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.Config.Debug.ValueAssigned += Debug_ValueAssigned;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.Config.Debug.ValueAssigned -= Debug_ValueAssigned;
        }

        void Debug_ValueAssigned(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            Main.EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;

            if(newValue)
                Main.EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;

            if(DebugEquipmentMsg != null)
                DebugEquipmentMsg.Visible = newValue;
        }

        HudAPIv2.HUDMessage DebugEquipmentMsg;

        void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            if(Main.TextAPI.WasDetected && Main.Config.Debug.Value)
            {
                if(DebugEquipmentMsg == null)
                    DebugEquipmentMsg = TextAPI.CreateHUDText(new StringBuilder(128), new Vector2D(-0.2f, 0.98f), scale: 0.75, hideWithHud: false);

                StringBuilder sb = DebugEquipmentMsg.Message.Clear();

                sb.Append(BuildInfoMod.ModName).Append(" Debug - Equipment.Update()\n");
                sb.Append(character != null ? "Character" : (shipController != null ? "Ship" : "<color=red>Other")).NewCleanLine();
                sb.Append("tool=").Color(Color.Yellow).Append(Main.EquipmentMonitor.ToolDefId == default(MyDefinitionId) ? "NONE" : Main.EquipmentMonitor.ToolDefId.ToString()).NewCleanLine();
                sb.Append("block=").Color(Color.Yellow).Append(Main.EquipmentMonitor.BlockDef?.Id.ToString() ?? "NONE").NewCleanLine();

                DebugEquipmentMsg.Visible = true;
            }
            else if(DebugEquipmentMsg != null)
            {
                DebugEquipmentMsg.Visible = false;
            }
        }

#if false
        void DumpTerminalProperties()
        {
            var dict = new Dictionary<MyObjectBuilderType, MyCubeBlockDefinition>();
            foreach(var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                var blockDef = def as MyCubeBlockDefinition;
                if(blockDef == null)
                    continue;

                dict[def.Id.TypeId] = blockDef;
            }

            foreach(var def in dict.Values)
            {
                TempBlockSpawn.Spawn(def, callback: BlockSpawned);
            }
        }

        void BlockSpawned(IMySlimBlock slim)
        {
            var tb = slim.FatBlock as IMyTerminalBlock;
            if(tb == null)
                return;

            var properties = new List<ITerminalProperty>();
            tb.GetProperties(properties);

            Log.Info("");
            Log.Info($"Properties of {tb.GetType().Name}");

            foreach(var p in properties)
            {
                switch(p.Id)
                {
                    case "OnOff":
                    case "ShowInTerminal":
                    case "ShowInInventory":
                    case "ShowInToolbarConfig":
                    case "ShowOnHUD":
                    case "Name":
                        continue;
                }

                Log.Info($"    id={p.Id,-24} type={p.TypeName}");
            }
        }
#endif

#if false
        void DumpActions()
        {
            // NOTE: requires all blocks to be spawned in the world in order to get accurate actions

            PrintActions<IMyLargeTurretBase>();
            PrintActions<IMyShipDrill>();
            PrintActions<IMyShipGrinder>();
            PrintActions<IMyShipToolBase>();
            PrintActions<IMySmallGatlingGun>();
            PrintActions<IMySmallMissileLauncher>();
            PrintActions<IMySmallMissileLauncherReload>();
            PrintActions<IMyUserControllableGun>();
            PrintActions<IMyAdvancedDoor>();
            PrintActions<IMyAirtightHangarDoor>();
            PrintActions<IMyAirtightSlideDoor>();
            PrintActions<IMyCameraBlock>();
            PrintActions<IMyCargoContainer>();
            PrintActions<IMyCockpit>();
            PrintActions<IMyConveyorSorter>();
            PrintActions<IMyDoor>();
            PrintActions<IMyGyro>();
            PrintActions<IMyJumpDrive>();
            PrintActions<IMyReflectorLight>();
            PrintActions<IMyRemoteControl>();
            PrintActions<IMyShipController>();
            PrintActions<IMyThrust>();
            PrintActions<IMyAssembler>();
            PrintActions<IMyBeacon>();
            PrintActions<IMyLaserAntenna>();
            PrintActions<IMyMotorAdvancedStator>();
            PrintActions<IMyMotorBase>();
            PrintActions<IMyMotorStator>();
            PrintActions<IMyMotorSuspension>();
            PrintActions<IMyOreDetector>();
            PrintActions<IMyProductionBlock>();
            PrintActions<IMyRadioAntenna>();
            PrintActions<IMyRefinery>();
            PrintActions<IMyWarhead>();
            PrintActions<IMyFunctionalBlock>();
            PrintActions<IMyShipConnector>();
            PrintActions<IMyTerminalBlock>();
            PrintActions<IMyCollector>();
            PrintActions<IMyCryoChamber>();
            PrintActions<IMyDecoy>();
            PrintActions<IMyExtendedPistonBase>();
            PrintActions<IMyGasGenerator>();
            PrintActions<IMyGasTank>();
            PrintActions<IMyMechanicalConnectionBlock>();
            PrintActions<IMyLightingBlock>();
            PrintActions<IMyPistonBase>();
            PrintActions<IMyProgrammableBlock>();
            PrintActions<IMySensorBlock>();
            PrintActions<IMyStoreBlock>();
            PrintActions<IMyTextPanel>();
            PrintActions<IMyProjector>();
            PrintActions<IMyLargeConveyorTurretBase>();
            PrintActions<IMyLargeGatlingTurret>();
            PrintActions<IMyLargeInteriorTurret>();
            PrintActions<IMyLargeMissileTurret>();
            PrintActions<IMyAirVent>();
            PrintActions<IMyButtonPanel>();
            PrintActions<IMyControlPanel>();
            PrintActions<IMyGravityGenerator>();
            PrintActions<IMyGravityGeneratorBase>();
            PrintActions<IMyGravityGeneratorSphere>();
            PrintActions<IMyInteriorLight>();
            PrintActions<IMyLandingGear>();
            PrintActions<IMyMedicalRoom>();
            PrintActions<IMyOxygenFarm>();
            PrintActions<IMyShipMergeBlock>();
            PrintActions<IMyShipWelder>();
            PrintActions<IMySoundBlock>();
            PrintActions<IMySpaceBall>();
            PrintActions<IMyTimerBlock>();
            PrintActions<IMyUpgradeModule>();
            PrintActions<IMyVirtualMass>();
            PrintActions<IMySafeZoneBlock>();

            // not proper!
            PrintActions<IMyBatteryBlock>();
            PrintActions<IMyReactor>();
            PrintActions<IMySolarPanel>();
            PrintActions<IMyParachute>();
            PrintActions<IMyExhaustBlock>();

            // not terminal
            //PrintActions<IMyConveyor>();
            //PrintActions<IMyConveyorTube>();
            //PrintActions<IMyWheel>();
            //PrintActions<IMyPistonTop>();
            //PrintActions<IMyMotorRotor>();
            //PrintActions<IMyMotorAdvancedRotor>();
            //PrintActions<IMyPassage>();
            //PrintActions<IMyAttachableTopBlock>();

            // not exist
            //PrintActions<IMyContractBlock>();
            //PrintActions<IMyLCDPanelsBlock>();
            //PrintActions<IMyRealWheel>();
            //PrintActions<IMyScenarioBuildingBlock>();
            //PrintActions<IMyVendingMachine>();
            //PrintActions<IMyEnvironmentalPowerProducer>();
            //PrintActions<IMyGasFueledPowerProducer>();
            //PrintActions<IMyHydrogenEngine>();
            //PrintActions<IMyJukebox>();
            //PrintActions<IMySurvivalKit>();
            //PrintActions<IMyWindTurbine>();
            //PrintActions<IMyLadder>();
            //PrintActions<IMyKitchen>();
            //PrintActions<IMyPlanter>();
            //PrintActions<IMyDoorBase>();
            //PrintActions<IMyEmissiveBlock>();
            //PrintActions<IMyFueledPowerProducer>();
        }

        void PrintActions<T>()
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<T>(out actions);

            Log.Info($"Actions of {typeof(T).Name}");

            foreach(var action in actions)
            {
                Log.Info($"    id='{action.Id}', name='{action.Name.ToString()}', icon='{action.Icon}'");
            }
        }
#endif
    }
}
