using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Systems;
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
using VRage.Input;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.Terminal
{
    public class MultiDetailInfo : ModComponent
    {
        const string TextFont = FontsHandler.SEOutlined;
        const bool UseShadowMessage = false;

        public readonly StringBuilder InfoText = new StringBuilder(512);

        bool RefreshNext;
        TextAPI.TextPackage Label;
        TextAPI.TextPackage Hint;
        HudAPIv2.BillBoardHUDMessage MoveIcon;

        BoxDragging Drag;

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

            Drag = new BoxDragging(MyMouseButtonsEnum.Right);
            Drag.BoxSelected += () => MoveIcon.BillBoardColor = Color.Lime;
            Drag.BoxDeselected += () => MoveIcon.BillBoardColor = Color.White;
            Drag.Dragging += (newPos) =>
            {
                Main.Config.TerminalMultiDetailedInfoPosition.Value = newPos;

                // TODO: scale setting?
                //int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                //if(scroll != 0)
                //{
                //    ConfigLib.FloatSetting setting = Main.Config.TerminalMultiDetailedInfoScale???;
                //    float scale = setting.Value + (scroll > 0 ? 0.05f : -0.05f);
                //    scale = MathHelper.Clamp(scale, setting.Min, setting.Max);
                //    setting.Value = (float)Math.Round(scale, 3);
                //}

                RefreshPositions();
            };
            Drag.FinishedDragging += (finalPos) =>
            {
                Main.Config.Save();
                Main.ConfigMenuHandler.RefreshAll();
            };

            SetupPerTypeFormatters();
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.Config.Handler.SettingsLoaded -= RefreshPositions;
            Main.TerminalInfo.SelectedChanged -= TerminalSelectedChanged;

            Drag = null;
        }

        void RefreshPositions()
        {
            if(Label == null)
                return;

            Vector2D pos = Main.Config.TerminalMultiDetailedInfoPosition.Value;
            const float scale = 1f;
            float textScale = scale * 1.3f;

            float iconHeight = 0.4f;
            float iconWidth = (float)(0.0025 / Main.GameConfig.AspectRatio);

            Label.Text.Scale = textScale;
            Label.Text.Origin = pos;

            if(Label.Shadow != null)
            {
                Label.Shadow.Scale = textScale;
                Label.Shadow.Origin = pos;
                Label.Shadow.Offset = Label.Text.Offset + new Vector2D(0.002, -0.002) * textScale;
            }

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
            int selectedNum = Main.TerminalInfo.SelectedInTerminal.Count;
            if(selectedNum <= 1
            || !Main.TextAPI.IsEnabled
            || !MyAPIGateway.Gui.IsCursorVisible
            || MyAPIGateway.Gui.GetCurrentScreen != MyTerminalPageEnum.ControlPanel
            || Main.GUIMonitor.InAnyToolbarGUI
            || MyAPIGateway.Gui.ActiveGamePlayScreen != "MyGuiScreenTerminal")
                return;

            if(Label == null)
            {
                Label = new TextAPI.TextPackage(InfoText);

                MoveIcon = TextAPI.CreateHUDTexture(MyStringId.GetOrCompute("BuildInfo_UI_Square"), Color.White, Vector2D.Zero, hideWithHud: false);

                Hint = new TextAPI.TextPackage(new StringBuilder("UI bg opacity too high to see multi-select info provided by buildinfo."));
                Hint.Text.Origin = new Vector2D(0, -0.98);
                Hint.Text.Scale = 0.6f;
                Hint.Text.Options &= ~HudAPIv2.Options.HideHud;
                Vector2D hintTextSize = Hint.Text.GetTextLength();
                Hint.Text.Offset = new Vector2D(hintTextSize.X / -2, 0);

                RefreshPositions();
            }

            if(Main.GameConfig.UIBackgroundOpacity >= 0.9f && Main.Config.TerminalMultiDetailedInfoPosition.Value == Main.Config.TerminalMultiDetailedInfoPosition.DefaultValue)
            {
                Hint.Text.Draw();
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

            Vector2D center = MoveIcon.Origin + MoveIcon.Offset;
            Vector2D halfSize = new Vector2D(0.02, MoveIcon.Height) / 2;
            Drag.DragHitbox = new BoundingBox2D(center - halfSize, center + halfSize);
            Drag.Position = Label.Text.Origin;
            Drag.Update();

            Label.Shadow?.Draw();
            Label.Text.Draw();
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
                        MyGyroDefinition gyroDef = (MyGyroDefinition)gyro.SlimBlock.BlockDefinition;

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
            // NOTE: this SB is also used on the copy detail info feature, if adding colors, must strip them there.
            StringBuilder info = InfoText.Clear();

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