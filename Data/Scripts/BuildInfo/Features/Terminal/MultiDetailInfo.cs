using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
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
        public HudAPIv2.HUDMessage Text;
        public HudAPIv2.HUDMessage TextShadow;

        HudAPIv2.BillBoardHUDMessage MoveIcon;
        Vector2D? DragOffset;
        bool IconHovered;

        HudAPIv2.HUDMessage Hint;

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
            TextShadow.Offset = Text.Offset + new Vector2D(0.002, -0.002) * TextShadow.Scale;

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

            int selectedNum = Main.TerminalInfo.SelectedInTerminal.Count;
            if(selectedNum <= 1
            || !Main.TextAPI.IsEnabled
            || !MyAPIGateway.Gui.IsCursorVisible
            || MyAPIGateway.Gui.GetCurrentScreen != MyTerminalPageEnum.ControlPanel
            || Main.GUIMonitor.InAnyToolbarGUI
            || MyAPIGateway.Gui.ActiveGamePlayScreen != "MyGuiScreenTerminal")
                return;

            if(Text == null)
            {
                StringBuilder sharedSB = new StringBuilder(512);

                Text = new HudAPIv2.HUDMessage(sharedSB, Vector2D.Zero, HideHud: false, Shadowing: false, Blend: BlendType);
                Text.Visible = false;

                TextShadow = new HudAPIv2.HUDMessage(sharedSB, Vector2D.Zero, HideHud: false, Shadowing: false, Blend: BlendType);
                TextShadow.InitialColor = Color.Black;
                TextShadow.Visible = false;

                MoveIcon = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("BuildInfo_UI_Square"), Vector2D.Zero, Color.White, HideHud: false, Shadowing: false, Blend: BlendType);
                MoveIcon.Visible = false;

                StringBuilder hintSB = new StringBuilder("UI bg opacity too high to see multi-select info provided by buildinfo.");
                Hint = new HudAPIv2.HUDMessage(hintSB, new Vector2D(0, -0.98), Scale: 0.6f, HideHud: false, Shadowing: true, Blend: BlendType);
                Hint.Visible = false;
                Vector2D hintTextSize = Hint.GetTextLength();
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

                Vector2D newPos = mouseOnScreen + DragOffset.Value;
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
        readonly Dictionary<MyStringHash, ResInfo> ResStorage = new Dictionary<MyStringHash, ResInfo>(MyStringHash.Comparer);

        struct ResInfo
        {
            public float Current;
            public float Max;
            public int Blocks;
        }

        static void IncrementResInfo(Dictionary<MyStringHash, ResInfo> dict, MyStringHash key, float addCurrent, float addMax)
        {
            ResInfo resInfo = dict.GetValueOrDefault(key);

            resInfo.Current += addCurrent;
            resInfo.Max += addMax;
            resInfo.Blocks++;

            dict[key] = resInfo;
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
                    MyStringHash key = thrustInfo.Fuel.SubtypeId;

                    IncrementResInfo(ResInput, key, thrustInfo.CurrentUsage, thrustInfo.MaxUsage);
                }
                else if(gyro != null)
                {
                    if(gyro.IsFunctional && gyro.Enabled)
                    {
                        MyStringHash key = MyResourceDistributorComponent.ElectricityId.SubtypeId;
                        MyGyro internalGyro = (MyGyro)gyro;
                        MyGyroDefinition gyroDef = (MyGyroDefinition)internalGyro.BlockDefinition;

                        IncrementResInfo(ResInput, key, internalGyro.RequiredPowerInput, gyroDef.RequiredPowerInput * gyro.PowerConsumptionMultiplier);
                    }
                }
                else
                {
                    MyResourceSinkComponent sink = block.Components.Get<MyResourceSinkComponent>();
                    if(sink != null)
                    {
                        foreach(MyDefinitionId resId in sink.AcceptedResources)
                        {
                            MyStringHash key = resId.SubtypeId;
                            IncrementResInfo(ResInput, key, sink.CurrentInputByType(resId), sink.MaxRequiredInputByType(resId));
                        }
                    }

                    MyResourceSourceComponent source = block.Components.Get<MyResourceSourceComponent>();
                    if(source != null)
                    {
                        bool isHydrogenEngine = (block.BlockDefinition.TypeId == typeof(MyObjectBuilder_HydrogenEngine));
                        IMyBatteryBlock battery = (!isHydrogenEngine ? block as IMyBatteryBlock : null);
                        IMyGasTank tank = (battery == null ? block as IMyGasTank : null);

                        foreach(MyDefinitionId resId in source.ResourceTypes)
                        {
                            MyStringHash key = resId.SubtypeId;

                            IncrementResInfo(ResOutput, key, source.CurrentOutputByType(resId), source.MaxOutputByType(resId));

                            if(key == MyResourceDistributorComponent.ElectricityId.SubtypeId)
                            {
                                if(battery != null)
                                {
                                    IncrementResInfo(ResStorage, key, battery.CurrentStoredPower, battery.MaxStoredPower);
                                }
                            }
                            else
                            {
                                if(tank != null)
                                {
                                    IncrementResInfo(ResStorage, key, (float)(tank.FilledRatio * tank.Capacity), tank.Capacity);
                                }
                            }
                        }

                        if(isHydrogenEngine)
                        {
                            MyHydrogenEngineDefinition def = (MyHydrogenEngineDefinition)block.SlimBlock.BlockDefinition;
                            MyStringHash key = def.Fuel.FuelId.SubtypeId;
                            float filled = source.RemainingCapacityByType(MyResourceDistributorComponent.ElectricityId);

                            IncrementResInfo(ResStorage, key, filled, def.FuelCapacity);
                        }
                    }
                }

                if(block.HasInventory)
                {
                    for(int invIdx = 0; invIdx < block.InventoryCount; invIdx++)
                    {
                        IMyInventory inv = block.GetInventory(invIdx);

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
                info.IdTypeFormat(firstBlock.BlockDefinition.TypeId).Append(" blocks");
            }
            else
            {
                info.Append("mixed blocks");
            }

            info.Append(" ---\n");

            foreach(KeyValuePair<MyStringHash, ResInfo> kv in ResInput)
            {
                MyStringHash resource = kv.Key;
                ResInfo resInfo = kv.Value;
                AppendInputFormat(info, resource, resInfo);

                // get output for this resource too
                if(ResOutput.TryGetValue(resource, out resInfo))
                {
                    ResOutput.Remove(resource); // and consume it
                    AppendOutputFormat(info, resource, resInfo);
                }

                // and storage too!
                if(ResStorage.TryGetValue(resource, out resInfo))
                {
                    ResStorage.Remove(resource); // and consume it
                    AppendStorageFormat(info, resource, resInfo);
                }
            }

            // print leftover outputs that have no inputs
            foreach(KeyValuePair<MyStringHash, ResInfo> kv in ResOutput)
            {
                MyStringHash resource = kv.Key;
                ResInfo resInfo = kv.Value;
                AppendOutputFormat(info, resource, resInfo);

                // print storage for this resource
                if(ResStorage.TryGetValue(resource, out resInfo))
                {
                    ResStorage.Remove(resource); // and consume it
                    AppendStorageFormat(info, resource, resInfo);
                }
            }

            // print leftover storage
            foreach(KeyValuePair<MyStringHash, ResInfo> kv in ResStorage)
            {
                MyStringHash resource = kv.Key;
                ResInfo resInfo = kv.Value;
                AppendStorageFormat(info, resource, resInfo);
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

        static void AppendInputFormat(StringBuilder info, MyStringHash resource, ResInfo resInfo)
        {
            if(resource == MyResourceDistributorComponent.ElectricityId.SubtypeId)
                info.Append(resInfo.Blocks).Append("x Power Consumers: ").PowerFormat(resInfo.Current);
            else
                info.Append(resInfo.Blocks).Append("x ").Append(resource.String).Append(" Consumers: ").VolumeFormat(resInfo.Current).Append("/s");
            info.Append('\n');
        }

        static void AppendOutputFormat(StringBuilder info, MyStringHash resource, ResInfo resInfo)
        {
            if(resource == MyResourceDistributorComponent.ElectricityId.SubtypeId)
                info.Append(resInfo.Blocks).Append("x Power Producers: ").PowerFormat(resInfo.Current);
            else
                info.Append(resInfo.Blocks).Append("x ").Append(resource.String).Append(" Producers: ").VolumeFormat(resInfo.Current).Append("/s");
            info.Append('\n');
        }

        static void AppendStorageFormat(StringBuilder info, MyStringHash resource, ResInfo resInfo)
        {
            if(resource == MyResourceDistributorComponent.ElectricityId.SubtypeId)
                info.Append(resInfo.Blocks).Append("x Power Storage: ").PowerStorageFormat(resInfo.Current);
            else
                info.Append(resInfo.Blocks).Append("x ").Append(resource.String).Append(" Storage: ").VolumeFormat(resInfo.Current).Append(" (max: ").VolumeFormat(resInfo.Max).Append(")");
            info.Append('\n');
        }

        #region Per-type formatters
        void SetupPerTypeFormatters()
        {
            //MultiInfoPerType.Add(typeof(MyObjectBuilder_BatteryBlock), Info_Battery);
        }

        //void Info_Battery(StringBuilder info, List<IMyTerminalBlock> blocks, bool allSameId)
        //{
        //    for(int blockIdx = 0; blockIdx < blocks.Count; blockIdx++)
        //    {
        //        IMyBatteryBlock block = (IMyBatteryBlock)blocks[blockIdx];

        //        currentStorage += block.CurrentStoredPower;
        //        maxStorage += block.MaxStoredPower;
        //    }

        //    info.Append("Stored Power: ").PowerStorageFormat(currentStorage).Append(" (max: ").PowerStorageFormat(maxStorage).Append(")\n");
        //}
        #endregion
    }
}