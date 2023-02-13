using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Engine.Physics;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using BlendType = VRageRender.MyBillboard.BlendTypeEnum;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
using System.Linq;
using VRage.ModAPI;
using VRage.Input;
using VRage.Game.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using VRage.Game.Entity;

namespace Digi.BuildInfo.Features
{
    public class DebugEvents : ModComponent
    {
        // cross reference/testing
        //public override void UpdateDraw()
        //{
        //    MatrixD m = MyAPIGateway.Session.Player.Character.WorldMatrix;
        //
        //    Vector3D back = Vector3D.Cross(m.Right, m.Up);
        //    Vector3D up = Vector3D.Cross(m.Forward, m.Left);
        //    Vector3D right = Vector3D.Cross(m.Forward, m.Up);
        //
        //    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), Color.Blue, m.Translation, back, 10, 0.1f);
        //    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), Color.Lime, m.Translation, up, 10, 0.1f);
        //    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), Color.Red, m.Translation, right, 10, 0.1f);
        //}

        public DebugEvents(BuildInfoMod main) : base(main)
        {
            //SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);

            //SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);

            //MyAPIGateway.Gui.GuiControlCreated += GuiControlCreated;
            //MyAPIGateway.Gui.GuiControlRemoved += GuiControlRemoved;
        }

        public override void RegisterComponent()
        {
            if(Main.IsPlayer)
            {
                Main.Config.Debug.ValueAssigned += Debug_ValueAssigned;

                //EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
                //EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;

                //MyVisualScriptLogicProvider.ToolbarItemChanged += ToolbarItemChanged;
            }

            //MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, BeforeDamage);

            //SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);

            //DumpActions();
            //DumpTerminalProperties();
        }

        public override void UnregisterComponent()
        {
            //MyAPIGateway.Gui.GuiControlCreated -= GuiControlCreated;
            //MyAPIGateway.Gui.GuiControlRemoved -= GuiControlRemoved;

            if(!Main.ComponentsRegistered)
                return;

            if(Main.IsPlayer)
            {
                Main.Config.Debug.ValueAssigned -= Debug_ValueAssigned;

                //EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
                //EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;

                //MyVisualScriptLogicProvider.ToolbarItemChanged -= ToolbarItemChanged;
            }
        }

        //Vector3 PrevDir;

        //public override void UpdateAfterSim(int tick)
        //{
        //    var slim = MyAPIGateway.Session.Player?.Character?.EquippedTool?.Components?.Get<MyCasterComponent>()?.HitBlock as IMySlimBlock;
        //    if(slim != null && slim.FatBlock is IMyReflectorLight)
        //    {
        //        MyCubeBlock block = (MyCubeBlock)slim.FatBlock;

        //        if(block.Subparts != null && block.Subparts.Count > 0)
        //        {
        //            MyEntitySubpart subpart = block.Subparts.First().Value;

        //            Vector3 dir = Vector3D.Normalize(subpart.PositionComp.LocalMatrix.Up);

        //            if(!Vector3.IsZero(PrevDir))
        //            {
        //                float angleDiff = (float)Math.Acos(Vector3.Dot(PrevDir, dir));

        //                float RPM = MathHelper.ToDegrees(angleDiff) * 20f;
        //                float degPerSec = MathHelper.ToDegrees(angleDiff) * 60f;

        //                MyAPIGateway.Utilities.ShowNotification($"RPM={RPM:0.##}", 16);
        //                MyAPIGateway.Utilities.ShowNotification($"deg per sec={degPerSec:0.##}", 16);
        //            }

        //            PrevDir = dir;
        //        }
        //    }
        //}

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

            //if(character != null && MyAPIGateway.Input.IsAnyShiftKeyPressed())
            //{
            //    var blendType = BlendType.PostPP;
            //    var thick = 0.01f;
            //    var cm = character.WorldMatrix;
            //    var observerPos = cm.Translation;
            //    var closePos = observerPos + cm.Forward * 5;
            //    var targetPos = Vector3D.Zero;

            //    var a = closePos - observerPos;
            //    var b = targetPos - observerPos;

            //    //Vector3D toPoint = somePoint - lineOrigin; // line from origin to this point
            //    //Vector3D projection = Vector3D.Dot(toPoint, lineDirection) / lineDirection.LengthSquared() * lineDirection;
            //    //Vector3D rejection = toPoint - projection;

            //    //Vector3D projectTargetOnLCD = dirToLCD * Vector3D.Dot(dirToTarget, dirToLCD);
            //    Vector3D projection = b * Vector3D.Dot(a, b) / b.LengthSquared();
            //    //MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), Color.Lime, observerPos, projection, 1f, 0.1f, blendType);

            //    Vector3D reject;

            //    if(MyAPIGateway.Input.IsAnyCtrlKeyPressed())
            //    {
            //        a.Normalize();
            //        b.Normalize();
            //    }

            //    reject = Vector3D.Reject(a, b);
            //    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), new Color(255, 155, 0) * 0.75f, observerPos, reject, 1f, 0.05f, blendType);

            //    reject = a - (b * (a.Dot(b) / b.LengthSquared()));
            //    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), new Color(0, 255, 0) * 0.75f, observerPos, reject, 1f, 0.1f, blendType);


            //    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), Color.White, observerPos, a, 1f, thick, blendType);
            //    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), Color.Blue, observerPos, b, 1f, thick, blendType);
            //}

            //if(character != null)
            //{
            //    var charView = MyAPIGateway.Session.Camera.WorldMatrix;
            //    var from = charView.Translation;
            //    var to = from + charView.Forward * 10;

            //    IHitInfo hit;
            //    if(MyAPIGateway.Physics.CastRay(from, to, out hit, CollisionLayers.CollisionLayerWithoutCharacter))
            //    {
            //        //MyTransparentGeometry.AddPointBillboard(MyStringId.GetOrCompute("WhiteDot"), Color.Red, hit.Position, 0.01f, 0, blendType: BlendType.PostPP);
            //        //MyTransparentGeometry.AddPointBillboard(MyStringId.GetOrCompute("WhiteDot"), Color.Lime, hit.Position, 0.01f, 0, blendType: BlendType.AdditiveTop);

            //        var grid = hit.HitEntity as IMyCubeGrid;
            //        if(grid != null)
            //        {
            //            var internalGrid = (MyCubeGrid)grid;

            //            Vector3I gridPos;
            //            grid.FixTargetCube(out gridPos, Vector3D.Transform(hit.Position, grid.WorldMatrixInvScaled) * internalGrid.GridSizeR);

            //            var block = grid.GetCubeBlock(gridPos);
            //            if(block != null && block.FatBlock != null)
            //            {
            //                var dir = hit.Position - block.FatBlock.GetPosition();
            //                double distX = Vector3D.Dot(dir, block.FatBlock.WorldMatrix.Right);
            //                double distY = Vector3D.Dot(dir, block.FatBlock.WorldMatrix.Up);
            //                double distZ = Vector3D.Dot(dir, block.FatBlock.WorldMatrix.Forward);

            //                MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), Color.Red, hit.Position, block.FatBlock.WorldMatrix.Left * (float)distX, 1f, 0.0005f, BlendType.Standard);
            //                MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), Color.Lime, hit.Position, block.FatBlock.WorldMatrix.Down * (float)distY, 1f, 0.0005f, BlendType.Standard);
            //                MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), Color.Blue, hit.Position, block.FatBlock.WorldMatrix.Forward, 1f, 0.0005f, BlendType.Standard);

            //                MyAPIGateway.Utilities.ShowNotification($"distX={distX:0.#####}; distY={distY:0.#####}; distZ={distZ:0.#####}", 16, MyFontEnum.Debug);
            //            }
            //        }
            //    }
            //}
        }


        // doublechecked cone maffs, which is correct
        //public override void UpdateDraw()
        //{
        //    float height = (float)Dev.GetValueScroll("height", 5, 1, MyKeys.D1);
        //    double angleDeg = Dev.GetValueScroll("angle", 3, 0.1, MyKeys.D2);
        //    double angleRad = MathHelper.ToRadians(angleDeg);

        //    MatrixD m = MatrixD.CreateRotationX(angleRad);
        //    m.Translation = Vector3D.Zero;

        //    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), Color.Blue, m.Translation, Vector3.Forward, height, 0.01f);
        //    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), Color.Red, m.Translation, m.Forward, height * 2, 0.01f);

        //    float radius = height * (float)Math.Tan(angleRad);
        //    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), Color.Lime, m.Translation + Vector3.Forward * height, Vector3.Up, radius, 0.01f);
        //}


        //int LastDamageAtTick;
        //float AccumulatedDamage = 0;
        //int AccumulatedStartAt = 0;

        //void BeforeDamage(object target, ref MyDamageInformation info)
        //{
        //    if(info.Type == MyDamageType.Grind)
        //    {
        //        if(LastDamageAtTick > 0)
        //        {
        //            double timeDiff = (Main.Tick - LastDamageAtTick) / 60d;

        //            MyAPIGateway.Utilities.ShowNotification($"BeforeDamage timeDiff={timeDiff.ToString("0.#####")}s", 1000, "Debug");
        //        }

        //        LastDamageAtTick = Main.Tick;
        //    }
        //}


        //int elapsed;
        //public override void UpdateAfterSim(int tick)
        //{
        //    if(MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.R))
        //    {
        //        float seconds = elapsed * (1 / 60f);
        //        MyAPIGateway.Utilities.ShowNotification($"elapsed: {seconds.ToString("0.00")} s", 16, "Debug");

        //        elapsed++;
        //    }
        //    else if(elapsed > 0)
        //    {
        //        float seconds = elapsed * (1 / 60f);
        //        MyAPIGateway.Utilities.ShowNotification($"final time: {seconds.ToString("0.00")} s", 3000, "Debug");

        //        elapsed = 0;
        //    }
        //}




        //List<MyCubeGrid.DebugUpdateRecord> debugData = new List<MyCubeGrid.DebugUpdateRecord>();
        //public override void UpdateAfterSim(int tick)
        //{
        //    var block = MyAPIGateway.Session.ControlledObject as IMyTerminalBlock;
        //    if(block == null)
        //        return;

        //    var grid = (MyCubeGrid)block.CubeGrid;

        //    debugData.Clear();
        //    grid.GetDebugUpdateInfo(debugData);

        //    DebugLog.ClearHUD(this);

        //    foreach(var debug in debugData)
        //    {
        //        DebugLog.PrintHUD(this, debug.ToString());
        //    }
        //}

        //void ToolbarItemChanged(long entityId, string typeId, string subtypeId, int page, int slot)
        //{
        //    Utils.AssertMainThread();
        //    Log.Info($"ToolbarItemChanged :: entId={entityId}; id={typeId}/{subtypeId}; page={page}; slot={slot}");
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







        //List<string> ScreensShown = new List<string>();

        //void GuiControlCreated(object screen)
        //{
        //    string name = screen.GetType().FullName;
        //    ScreensShown.Add(name);
        //}

        //void GuiControlRemoved(object screen)
        //{
        //    string name = screen.GetType().FullName;
        //    ScreensShown.Remove(name);
        //}

        //private HudAPIv2.HUDMessage debugScreenInfo;
        //private HudAPIv2.HUDMessage debugAllCharacters;

        //public override void UpdateDraw()
        //{
        //    if(!TextAPI.WasDetected)
        //        return;

        //    if(!Config.Debug.Value)
        //    {
        //        if(debugScreenInfo != null)
        //            debugScreenInfo.Visible = false;

        //        if(debugAllCharacters != null)
        //            debugAllCharacters.Visible = false;

        //        return;
        //    }

        //    {
        //        if(debugScreenInfo == null)
        //            debugScreenInfo = new HudAPIv2.HUDMessage(new StringBuilder(256), new Vector2D(-0.98, 0.98), Shadowing: true, Blend: BlendType.PostPP);

        //        var sb = debugScreenInfo.Message.Clear();

        //        sb.Append("ActiveGamePlayScreen=").Append(MyAPIGateway.Gui.ActiveGamePlayScreen);
        //        sb.Append("\nGetCurrentScreen=").Append(MyAPIGateway.Gui.GetCurrentScreen.ToString());
        //        sb.Append("\nIsCursorVisible=").Append(MyAPIGateway.Gui.IsCursorVisible);
        //        sb.Append("\nChatEntryVisible=").Append(MyAPIGateway.Gui.ChatEntryVisible);
        //        sb.Append("\nInteractedEntity=").Append(MyAPIGateway.Gui.InteractedEntity);

        //        sb.Append("\n\nScreensShown:");
        //        foreach(var screen in ScreensShown)
        //        {
        //            sb.Append("\n - ").Append(screen);
        //        }

        //        debugScreenInfo.Visible = true;
        //    }

        //    if(MyAPIGateway.Input.IsAnyShiftKeyPressed())
        //    {
        //        int charsPerRow = (int)Dev.GetValueScroll("charsPerRow", 16, 1, VRage.Input.MyKeys.D1);
        //        var chars = Main.FontsHandler.CharSize;

        //        if(debugAllCharacters == null)
        //        {
        //            debugAllCharacters = new HudAPIv2.HUDMessage(new StringBuilder(chars.Count * 15), new Vector2D(-0.8, 0.7), Blend: BlendType.PostPP);
        //        }

        //        var sb = debugAllCharacters.Message.Clear();
        //        int perRow = 0;
        //        foreach(var kv in chars)
        //        {
        //            sb.Append(kv.Key);

        //            sb.Append(" <color=0,100,0>").AppendFormat("{0:X}", (int)kv.Key).Append("<color=white>   ");

        //            perRow++;
        //            if(perRow > charsPerRow)
        //            {
        //                perRow = 0;
        //                sb.Append('\n');
        //            }
        //        }

        //        debugAllCharacters.Visible = true;
        //    }
        //    else
        //    {
        //        if(debugAllCharacters != null)
        //            debugAllCharacters.Visible = false;
        //    }
        //}

        //private HudAPIv2.HUDMessage debugHudMsg;

        //public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
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

        //public override void UpdateDraw()
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
}
