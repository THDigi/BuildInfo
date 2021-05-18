using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features.Terminal
{
    public class MultiDetailInfo : ModComponent
    {
        readonly BlendTypeEnum BlendType = BlendTypeEnum.PostPP;

        bool RefreshNext;
        HudAPIv2.HUDMessage Text;
        HudAPIv2.HUDMessage TextShadow;

        HudAPIv2.BillBoardHUDMessage MoveIcon;
        Vector2D? DragOffset;
        bool IconHovered;

        HudAPIv2.HUDMessage Hint;

        readonly Dictionary<MyObjectBuilderType, string> TypeToFriendlyName = new Dictionary<MyObjectBuilderType, string>()
        {
            [typeof(MyObjectBuilder_MotorSuspension)] = "Suspension",
            [typeof(MyObjectBuilder_MotorStator)] = "Rotor Base",
            [typeof(MyObjectBuilder_MotorAdvancedStator)] = "Adv. Rotor Base",
            [typeof(MyObjectBuilder_MotorRotor)] = "Rotor Top",
            [typeof(MyObjectBuilder_MotorAdvancedRotor)] = "Adv. Rotor Top",
            [typeof(MyObjectBuilder_ExtendedPistonBase)] = "Piston",
            [typeof(MyObjectBuilder_PistonBase)] = "Piston",
            [typeof(MyObjectBuilder_PistonTop)] = "Piston Top",

            [typeof(MyObjectBuilder_OxygenGenerator)] = "Gas Generator",
            [typeof(MyObjectBuilder_OxygenTank)] = "Gas Tank",
            [typeof(MyObjectBuilder_HydrogenEngine)] = "Hydrogen Engine",

            [typeof(MyObjectBuilder_LargeGatlingTurret)] = "Gatling Turret",
            [typeof(MyObjectBuilder_LargeMissileTurret)] = "Missile Turret",
            [typeof(MyObjectBuilder_InteriorTurret)] = "Interior Turret",
            [typeof(MyObjectBuilder_SmallGatlingGun)] = "Gatling Gun",
            [typeof(MyObjectBuilder_SmallMissileLauncher)] = "Missile Launcher",
            [typeof(MyObjectBuilder_SmallMissileLauncherReload)] = "Reloadable Missile Launcher",

            [typeof(MyObjectBuilder_ShipConnector)] = "Connector",
            [typeof(MyObjectBuilder_MergeBlock)] = "Merge",
            [typeof(MyObjectBuilder_ExhaustBlock)] = "Exhaust",
            [typeof(MyObjectBuilder_CameraBlock)] = "Camera",
            [typeof(MyObjectBuilder_BatteryBlock)] = "Battery",

            [typeof(MyObjectBuilder_SensorBlock)] = "Sensor",
            [typeof(MyObjectBuilder_ReflectorLight)] = "Spotlight",
            [typeof(MyObjectBuilder_InteriorLight)] = "Interior Light",

            [typeof(MyObjectBuilder_OreDetector)] = "Ore Detector",
            [typeof(MyObjectBuilder_RadioAntenna)] = "Radio Antenna",
            [typeof(MyObjectBuilder_LaserAntenna)] = "Laser Antenna",
            [typeof(MyObjectBuilder_LandingGear)] = "Landing Gear",
            [typeof(MyObjectBuilder_JumpDrive)] = "Jump Drive",
            [typeof(MyObjectBuilder_GravityGenerator)] = "Gravity Generator",
            [typeof(MyObjectBuilder_GravityGeneratorSphere)] = "Spherical Gravity Generator",
            [typeof(MyObjectBuilder_CryoChamber)] = "Cryo Chamber",
            [typeof(MyObjectBuilder_ConveyorSorter)] = "Conveyor Sorter",
            [typeof(MyObjectBuilder_ControlPanel)] = "Control Panel",
            [typeof(MyObjectBuilder_CargoContainer)] = "Cargo Container",
            [typeof(MyObjectBuilder_ButtonPanel)] = "Button Panel",
            [typeof(MyObjectBuilder_AirVent)] = "Air Vent",
            [typeof(MyObjectBuilder_AirtightSlideDoor)] = "Slide Door",
            [typeof(MyObjectBuilder_AirtightHangarDoor)] = "Hangar Door",
            [typeof(MyObjectBuilder_AdvancedDoor)] = "Advanced Door",

            [typeof(MyObjectBuilder_ShipGrinder)] = "Grinder",
            [typeof(MyObjectBuilder_ShipWelder)] = "Welder",

            [typeof(MyObjectBuilder_TextPanel)] = "LCD",
            [typeof(MyObjectBuilder_LCDPanelsBlock)] = "Decorative with LCD",
        };

        delegate void MultiInfoDelegate(StringBuilder info, List<IMyTerminalBlock> blocks, bool allSameId);
        readonly Dictionary<MyObjectBuilderType, MultiInfoDelegate> MultiInfoPerType = new Dictionary<MyObjectBuilderType, MultiInfoDelegate>();

        public MultiDetailInfo(BuildInfoMod main) : base(main)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);
        }

        public override void RegisterComponent()
        {
            Main.Config.Handler.SettingsLoaded += RefreshPositions;
            Main.TerminalInfo.SelectedChanged += TerminalSelectedChanged;

            SetupPerTypeFormatters();
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.Config.Handler.SettingsLoaded -= RefreshPositions;
            Main.TerminalInfo.SelectedChanged -= TerminalSelectedChanged;
        }

        void RefreshPositions()
        {
            if(Text == null)
                return;

            Vector2D pos = Main.Config.TerminalMultiDetailedInfoPosition.Value;
            const float scale = 1f;

            float iconHeight = 0.55f;
            float iconWidth = (float)(0.0025 / Main.GameConfig.AspectRatio);

            Text.Scale = scale * 1.3f;
            Text.Origin = pos;

            TextShadow.Scale = Text.Scale;
            TextShadow.Origin = pos;
            TextShadow.Offset = Text.Offset + new Vector2D(0.0025, -0.0025) * TextShadow.Scale;

            MoveIcon.Scale = scale;
            MoveIcon.Origin = pos;
            MoveIcon.Width = iconWidth;
            MoveIcon.Height = iconHeight;
            MoveIcon.Offset = new Vector2D(-0.0363f, iconHeight / -2);
        }

        void TerminalSelectedChanged()
        {
            RefreshNext = true;
        }

        public override void UpdateDraw()
        {
            if(DragOffset.HasValue && MyAPIGateway.Input.IsNewRightMouseReleased())
            {
                DragOffset = null;
                Main.Config.Save();
                Main.ConfigMenuHandler.RefreshAll();
            }

            var selectedNum = Main.TerminalInfo.SelectedInTerminal.Count;
            if(selectedNum <= 1
            || !Main.TextAPI.IsEnabled
            || !MyAPIGateway.Gui.IsCursorVisible
            || MyAPIGateway.Gui.GetCurrentScreen != MyTerminalPageEnum.ControlPanel
            || Main.GUIMonitor.InAnyToolbarGUI
            || MyAPIGateway.Gui.ActiveGamePlayScreen != "MyGuiScreenTerminal")
                return;

            if(Text == null)
            {
                var sharedSB = new StringBuilder(512);

                Text = new HudAPIv2.HUDMessage(sharedSB, Vector2D.Zero, HideHud: false, Shadowing: false, Blend: BlendType);
                Text.Visible = false;

                TextShadow = new HudAPIv2.HUDMessage(sharedSB, Vector2D.Zero, HideHud: false, Shadowing: false, Blend: BlendType);
                TextShadow.InitialColor = Color.Black;
                TextShadow.Visible = false;

                MoveIcon = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("BuildInfo_UI_Square"), Vector2D.Zero, Color.White, HideHud: false, Shadowing: false, Blend: BlendType);
                MoveIcon.Visible = false;

                var hintSB = new StringBuilder("UI bg opacity too high to see multi-select info provided by buildinfo.");
                Hint = new HudAPIv2.HUDMessage(hintSB, new Vector2D(0, -0.98), Scale: 0.6f, HideHud: false, Shadowing: true, Blend: BlendType);
                Hint.Visible = false;
                var hintTextSize = Hint.GetTextLength();
                Hint.Offset = new Vector2D(hintTextSize.X / -2, 0);

                RefreshPositions();
            }

            if(Main.GameConfig.UIBackgroundOpacity >= 0.9f && Main.Config.TerminalMultiDetailedInfoPosition.Value == Main.Config.TerminalMultiDetailedInfoPosition.DefaultValue)
            {
                Hint.Draw();
            }

            int skipEveryTicks;
            if(selectedNum > 1000)
                skipEveryTicks = 120;
            else if(selectedNum > 300)
                skipEveryTicks = 60;
            else if(selectedNum > 100)
                skipEveryTicks = 30;
            else
                skipEveryTicks = 15;

            if(RefreshNext || Main.Tick % skipEveryTicks == 0)
            {
                RefreshNext = false;
                UpdateText();
            }

            Vector2 screenSize = MyAPIGateway.Input.GetMouseAreaSize();
            Vector2 mousePos = MyAPIGateway.Input.GetMousePosition() / screenSize;
            Vector2D mouseOnScreen = new Vector2D(mousePos.X * 2 - 1, 1 - 2 * mousePos.Y); // turn from 0~1 to -1~1

            Vector2D center = MoveIcon.Origin + MoveIcon.Offset;
            Vector2D halfSize = new Vector2D(0.02, MoveIcon.Height) / 2;
            BoundingBox2D bb = new BoundingBox2D(center - halfSize, center + halfSize);

            if(bb.Contains(mouseOnScreen) == ContainmentType.Contains)
            {
                if(!IconHovered)
                {
                    IconHovered = true;
                    MoveIcon.BillBoardColor = Color.Lime;
                }

                if(MyAPIGateway.Input.IsNewRightMousePressed())
                {
                    DragOffset = Text.Origin - mouseOnScreen;
                }
            }
            else
            {
                if(IconHovered)
                {
                    IconHovered = false;
                    MoveIcon.BillBoardColor = Color.White;
                }
            }

            if(DragOffset.HasValue && MyAPIGateway.Input.IsRightMousePressed())
            {
                const int Rounding = 4;

                var newPos = mouseOnScreen + DragOffset.Value;
                newPos = new Vector2D(Math.Round(newPos.X, Rounding), Math.Round(newPos.Y, Rounding));
                newPos = Vector2D.Clamp(newPos, -Vector2D.One, Vector2D.One);

                Main.Config.TerminalMultiDetailedInfoPosition.Value = newPos;
                RefreshPositions();
            }

            TextShadow.Draw();
            Text.Draw();
            MoveIcon.Draw();
        }

        readonly Dictionary<MyStringHash, ResInfo> ResInput = new Dictionary<MyStringHash, ResInfo>(MyStringHash.Comparer);
        readonly Dictionary<MyStringHash, ResInfo> ResOutput = new Dictionary<MyStringHash, ResInfo>(MyStringHash.Comparer);

        struct ResInfo
        {
            public float Total;
            public int Blocks;
        }

        void UpdateText()
        {
            List<IMyTerminalBlock> selected = Main.TerminalInfo.SelectedInTerminal;
            int totalBlocks = selected.Count;
            if(totalBlocks <= 0)
                return;

            ResInput.Clear();
            ResOutput.Clear();

            float inventoryCurrentM3 = 0f;
            float inventoryMaxM3 = 0f;
            float inventoryMass = 0f;
            int inventoryBlocks = 0;

            IMyTerminalBlock firstBlock = selected[0];
            bool allSameId = true;
            bool allSameType = true;

            #region Block compute loop
            for(int blockIdx = 0; blockIdx < selected.Count; blockIdx++)
            {
                IMyTerminalBlock block = selected[blockIdx];

                if(allSameType && block.BlockDefinition.TypeId != firstBlock.BlockDefinition.TypeId)
                    allSameType = false;

                if(allSameId && !block.SlimBlock.BlockDefinition.Id.Equals(firstBlock.SlimBlock.BlockDefinition.Id))
                    allSameId = false;

                IMyThrust thrust = block as IMyThrust; // HACK: thrusters have shared sinks and not reliable to get
                IMyGyro gyro = (thrust == null ? block as IMyGyro : null); // HACK: gyro has no sink 

                if(thrust != null)
                {
                    Hardcoded.ThrustInfo thrustInfo = Hardcoded.Thrust_GetUsage(thrust);

                    MyStringHash key;
                    if(thrustInfo.Fuel != MyResourceDistributorComponent.ElectricityId)
                        key = thrustInfo.Fuel.SubtypeId;
                    else
                        key = MyResourceDistributorComponent.ElectricityId.SubtypeId;

                    ResInfo resInfo = ResInput.GetValueOrDefault(key);

                    resInfo.Total += thrustInfo.CurrentUsage;
                    resInfo.Blocks++;

                    ResInput[key] = resInfo;
                }
                else if(gyro != null)
                {
                    if(gyro.IsFunctional && gyro.Enabled)
                    {
                        MyStringHash key = MyResourceDistributorComponent.ElectricityId.SubtypeId;
                        ResInfo resInfo = ResInput.GetValueOrDefault(key);

                        resInfo.Total += ((MyGyro)gyro).RequiredPowerInput;
                        resInfo.Blocks++;

                        ResInput[key] = resInfo;
                    }
                }
                else
                {
                    var sink = block.Components.Get<MyResourceSinkComponent>();
                    if(sink != null)
                    {
                        foreach(var resId in sink.AcceptedResources)
                        {
                            MyStringHash key = resId.SubtypeId;
                            ResInfo resInfo = ResInput.GetValueOrDefault(key);

                            resInfo.Total += sink.CurrentInputByType(resId);
                            resInfo.Blocks++;

                            ResInput[key] = resInfo;
                        }
                    }

                    var source = block.Components.Get<MyResourceSourceComponent>();
                    if(source != null)
                    {
                        foreach(var resId in source.ResourceTypes)
                        {
                            MyStringHash key = resId.SubtypeId;
                            ResInfo resInfo = ResOutput.GetValueOrDefault(key);

                            resInfo.Total += source.CurrentOutputByType(resId);
                            resInfo.Blocks++;

                            ResOutput[key] = resInfo;
                        }
                    }
                }

                if(block.HasInventory)
                {
                    for(int invIdx = 0; invIdx < block.InventoryCount; invIdx++)
                    {
                        var inv = block.GetInventory(invIdx);

                        inventoryCurrentM3 += (float)inv.CurrentVolume;
                        inventoryMaxM3 += (float)inv.MaxVolume;
                        inventoryMass += (float)inv.CurrentMass;
                    }

                    inventoryBlocks++;
                }
            }
            #endregion Block compute loop

            // NOTE: the same SB is used by both the text and the shadow, therefore it can't use colors.
            StringBuilder info = Text.Message.Clear();

            info.Append("--- ").Append(totalBlocks).Append("x ");

            if(allSameId)
            {
                info.AppendMaxLength(firstBlock.DefinitionDisplayNameText, 40);
            }
            else if(allSameType)
            {
                string friendlyName = TypeToFriendlyName.GetValueOrDefault(firstBlock.BlockDefinition.TypeId, null);
                if(friendlyName != null)
                    info.Append(friendlyName);
                else
                    info.IdTypeFormat(firstBlock.BlockDefinition.TypeId);
                info.Append(" blocks");
            }
            else
            {
                info.Append("mixed blocks");
            }

            info.Append(" ---\n");

            foreach(var kv in ResInput)
            {
                ResInfo resInfo = kv.Value;

                if(kv.Key == MyResourceDistributorComponent.ElectricityId.SubtypeId)
                    info.Append(resInfo.Blocks).Append("x Consume Power: ").PowerFormat(resInfo.Total);
                else
                    info.Append(resInfo.Blocks).Append("x Consume ").Append(kv.Key.String).Append(": ").VolumeFormat(resInfo.Total).Append("/s");

                info.Append('\n');

                // get output for this resource too
                if(ResOutput.TryGetValue(kv.Key, out resInfo))
                {
                    ResOutput.Remove(kv.Key); // and consume it

                    if(kv.Key == MyResourceDistributorComponent.ElectricityId.SubtypeId)
                        info.Append(resInfo.Blocks).Append("x Produce Power: ").PowerFormat(resInfo.Total);
                    else
                        info.Append(resInfo.Blocks).Append("x Produce ").Append(kv.Key.String).Append(": ").VolumeFormat(resInfo.Total).Append("/s");

                    info.Append('\n');
                }
            }

            // print outputs that have no inputs
            foreach(var kv in ResOutput)
            {
                ResInfo resInfo = kv.Value;

                if(kv.Key == MyResourceDistributorComponent.ElectricityId.SubtypeId)
                    info.Append(resInfo.Blocks).Append("x Produce Power: ").PowerFormat(resInfo.Total);
                else
                    info.Append(resInfo.Blocks).Append("x Produce ").Append(kv.Key.String).Append(": ").VolumeFormat(resInfo.Total).Append("/s");

                info.Append('\n');
            }

            if(inventoryBlocks > 0)
            {
                info.Append(inventoryBlocks).Append("x Inventories: ").VolumeFormat(inventoryCurrentM3 * 1000).Append(" / ").VolumeFormat(inventoryMaxM3 * 1000).Append(" (").MassFormat(inventoryMass).Append(")\n");
            }

            if(allSameType)
            {
                info.Append('\n');
                MultiInfoPerType.GetValueOrDefault(firstBlock.BlockDefinition.TypeId, null)?.Invoke(info, selected, allSameId);
            }
        }

        #region Per-type formatters
        void SetupPerTypeFormatters()
        {
            MultiInfoPerType.Add(typeof(MyObjectBuilder_BatteryBlock), Info_Battery);
        }

        void Info_Battery(StringBuilder info, List<IMyTerminalBlock> blocks, bool allSameId)
        {
            float currentStorage = 0f;
            float maxStorage = 0f;

            for(int blockIdx = 0; blockIdx < blocks.Count; blockIdx++)
            {
                IMyBatteryBlock block = (IMyBatteryBlock)blocks[blockIdx];

                currentStorage += block.CurrentStoredPower;
                maxStorage += block.MaxStoredPower;
            }

            info.Append("Stored Power: ").PowerStorageFormat(currentStorage).Append(" (max: ").PowerStorageFormat(maxStorage).Append(")\n");
        }
        #endregion
    }
}