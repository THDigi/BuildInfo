using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Systems;
using Draygo.API;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    //[ProtoContract]
    //public class TestPacket
    //{
    //    [ProtoMember(1)]
    //    public long IdentityId;
    //
    //    [ProtoMember(2)]
    //    public string BlockId;
    //
    //    [ProtoMember(3)]
    //    public int Slot;
    //
    //    public TestPacket() { }
    //}


    //var packet = new TestPacket();
    //packet.IdentityId = MyAPIGateway.Session.Player.IdentityId;
    //packet.BlockId = PickedBlockDef.Id.ToString();
    //packet.Slot = (slot - 1);
    //MyAPIGateway.Multiplayer.SendMessageToServer(1337, MyAPIGateway.Utilities.SerializeToBinary(packet));


    public class DebugEvents : ModComponent
    {
        public DebugEvents(BuildInfoMod main) : base(main)
        {
        }

        protected override void RegisterComponent()
        {
            if(Main.IsPlayer)
            {
                //EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
                //EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
                EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;

                //MyVisualScriptLogicProvider.ToolbarItemChanged += ToolbarItemChanged;
            }

            //if(MyAPIGateway.Multiplayer.IsServer)
            //{
            //    MyAPIGateway.Multiplayer.RegisterMessageHandler(1337, ReceivedPacket);
            //}

            //DumpActions();
        }

        protected override void UnregisterComponent()
        {
            if(Main.IsPlayer)
            {
                //EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
                //EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
                EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;

                //MyVisualScriptLogicProvider.ToolbarItemChanged -= ToolbarItemChanged;
            }

            //if(MyAPIGateway.Multiplayer.IsServer)
            //{
            //    MyAPIGateway.Multiplayer.UnregisterMessageHandler(1337, ReceivedPacket);
            //}
        }

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

        //void ReceivedPacket(byte[] rawData)
        //{
        //    try
        //    {
        //        var packet = MyAPIGateway.Utilities.SerializeFromBinary<TestPacket>(rawData);
        //
        //        if(packet == null)
        //            return;
        //
        //        MyLog.Default.WriteLineAndConsole($"### DEBUG: Received packet for identity={packet.IdentityId}; blockId={packet.BlockId}; slot={packet.Slot}");
        //
        //        var blockDefId = MyDefinitionId.Parse(packet.BlockId);
        //        MyVisualScriptLogicProvider.SetToolbarSlotToItem(packet.Slot - 1, blockDefId, packet.IdentityId);
        //    }
        //    catch(Exception e)
        //    {
        //        Log.Error(e);
        //    }
        //}

        //void ToolbarItemChanged(long entityId, string typeId, string subtypeId, int page, int slot)
        //{
        //    MyAPIGateway.Utilities.ShowNotification($"entId={entityId.ToString()}; id={typeId}/{subtypeId}; page={page.ToString()}; slot={slot.ToString()}", 5000);
        //}

        //private void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        //{
        //    if(Config.Debug)
        //        MyAPIGateway.Utilities.ShowNotification($"Equipment.ToolChanged :: {toolDefId}", 1000, MyFontEnum.Green);
        //}

        //private void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        //{
        //    if(Config.Debug)
        //        MyAPIGateway.Utilities.ShowNotification($"Equipment.BlockChanged :: {def?.Id.ToString() ?? "Unequipped"}, {(def == null ? "" : (block != null ? "Aimed" : "Held"))}", 1000);
        //}

        private HudAPIv2.HUDMessage debugEquipmentMsg;

        private void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            if(TextAPI.WasDetected)
            {
                if(Config.Debug.Value)
                {
                    if(debugEquipmentMsg == null)
                        debugEquipmentMsg = new HudAPIv2.HUDMessage(new StringBuilder(), new Vector2D(-0.2f, 0.98f), Scale: 0.75, HideHud: false);

                    debugEquipmentMsg.Visible = true;
                    debugEquipmentMsg.Message.Clear().Append($"BuildInfo Debug - Equipment.Update()\n" +
                        $"{(character != null ? "Character" : (shipController != null ? "Ship" : "<color=red>Other<color=white>"))}\n" +
                        $"tool=<color=yellow>{(EquipmentMonitor.ToolDefId == default(MyDefinitionId) ? "NONE" : EquipmentMonitor.ToolDefId.ToString())}\n" +
                        $"<color=white>block=<color=yellow>{EquipmentMonitor.BlockDef?.Id.ToString() ?? "NONE"}");
                }
                else if(debugEquipmentMsg != null && debugEquipmentMsg.Visible)
                {
                    debugEquipmentMsg.Visible = false;
                }
            }
        }

        //private HudAPIv2.HUDMessage debugHudMsg;

        //protected override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        //{
        //    MyAPIGateway.Utilities.ShowMessage("DEBUG", $"HUD={MyAPIGateway.Session.Config.HudState}; MinimalHUD={MyAPIGateway.Session.Config.MinimalHud}");

        //    if(!TextAPI.WasDetected)
        //        return;

        //    if(debugHudMsg == null)
        //        debugHudMsg = new HudAPIv2.HUDMessage(new StringBuilder(), new Vector2D(-0.2f, 0.9f), Scale: 0.75, HideHud: false);

        //    debugHudMsg.Message.Clear().Append($"" +
        //        $"HUD State = {MyAPIGateway.Session.Config.HudState}\n" +
        //        $"MinimalHUD = {MyAPIGateway.Session.Config.MinimalHud}");

        //    if(anyKeyOrMouse && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.L))
        //    {
        //        MyVisualScriptLogicProvider.ShowHud(false);
        //        debugHudMsg.Message.Append("\n<color=red>HIDDEN!!!!!");
        //    }
        //}

        //HudAPIv2.SpaceMessage msg;
        //HudAPIv2.SpaceMessage shadow;

        //protected override void UpdateDraw()
        //{
        //    if(TextAPI.WasDetected)
        //    {
        //        var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
        //        var up = camMatrix.Up;
        //        var left = camMatrix.Left;
        //        var pos = camMatrix.Translation + camMatrix.Forward * 0.2;

        //        double textSize = 0.24;
        //        double shadowOffset = 0.007;

        //        if(msg == null)
        //        {
        //            var offset = new Vector2D(0, -0.05);
        //            msg = new HudAPIv2.SpaceMessage(new StringBuilder("Text"), pos, up, left, textSize, offset, Blend: BlendTypeEnum.SDR);

        //            offset += new Vector2D(shadowOffset, -shadowOffset);
        //            shadow = new HudAPIv2.SpaceMessage(new StringBuilder("<color=black>Text"), pos, up, left, textSize, offset, Blend: BlendTypeEnum.Standard);
        //        }

        //        msg.Up = up;
        //        msg.Left = left;
        //        msg.WorldPosition = pos;
        //        msg.Flush();

        //        //pos += up * -shadowOffset + left * -shadowOffset;

        //        shadow.Up = up;
        //        shadow.Left = left;
        //        shadow.WorldPosition = pos;
        //        shadow.Flush();
        //    }
        //}
    }
}
