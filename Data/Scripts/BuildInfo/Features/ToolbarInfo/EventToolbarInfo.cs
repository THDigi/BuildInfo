using System;
using System.Text;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.Overlays;
using Digi.BuildInfo.Features.Overlays.Specialized;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Collections;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    /// <summary>
    /// TextAPI-based event-toolbar info (for example, show what each slot does in airvent's Setup Actions)
    /// </summary>
    public class EventToolbarInfo : ModComponent
    {
        readonly Color HeaderColor = new Color(255, 240, 220);
        readonly Color SlotColor = new Color(55, 200, 155);
        readonly Color ModNameColor = Color.Gray;

        const float BackgroundOpacity = 0.75f;
        readonly Color BackgroundColor = new Color(41, 54, 62);
        readonly Color BackgroundColorSelected = new Color(40, 80, 65);
        const BlendTypeEnum BlendType = BlendTypeEnum.PostPP;

        const string TextFont = FontsHandler.SEOutlined;
        const bool UseShadowMessage = false;
        const double TextScaleMultiplier = 0.75;
        const double ShadowOffset = 0.002;
        const float BackgroundPadding = 0.03f;

        double GUIScale;

        TextAPI.TextPackage Label;

        IMyTerminalBlock TargetBlock;
        ListReader<IMyTerminalBlock> LastSeenBlocks = ListReader<IMyTerminalBlock>.Empty; // required or NRE's on Count

        OverlayDrawInstance DrawInstance;
        BoxDragging BoxDrag;

        public EventToolbarInfo(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.EventToolbarMonitor.OpenedToolbarConfig += OpenedToolbarConfig;
            Main.EventToolbarMonitor.ClosedToolbarConfig += ClosedToolbarConfig;

            Main.TextAPI.Detected += TextAPIDetected;

            Main.Config.EventToolbarInfoPosition.ValueAssigned += ConfigPositionChanged;
            Main.Config.EventToolbarInfoScale.ValueAssigned += ConfigFloatChanged;
            Main.GameConfig.OptionsMenuClosed += UpdateFromConfig;

            DrawInstance = new OverlayDrawInstance(Main.Overlays, GetType().Name);
            DrawInstance.LabelRender.ForceDrawLabel = true;

            BoxDrag = new BoxDragging(MyMouseButtonsEnum.Left);
            BoxDrag.BoxSelected += () => UpdateBgOpacity(BackgroundOpacity, BackgroundColorSelected);
            BoxDrag.BoxDeselected += () => UpdateBgOpacity(BackgroundOpacity);
            BoxDrag.Dragging += (newPos) =>
            {
                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(scroll != 0)
                {
                    ConfigLib.FloatSetting setting = Main.Config.EventToolbarInfoScale;
                    float scale = setting.Value + (scroll > 0 ? 0.05f : -0.05f);
                    scale = MathHelper.Clamp(scale, setting.Min, setting.Max);
                    setting.Value = (float)Math.Round(scale, 3);
                }

                Main.Config.EventToolbarInfoPosition.Value = newPos;
                UpdateFromConfig();
            };
            BoxDrag.FinishedDragging += (finalPos) =>
            {
                Main.Config.Save();
                Main.ConfigMenuHandler.RefreshAll();
            };
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.EventToolbarMonitor.OpenedToolbarConfig -= OpenedToolbarConfig;
            Main.EventToolbarMonitor.ClosedToolbarConfig -= ClosedToolbarConfig;

            Main.TextAPI.Detected -= TextAPIDetected;

            Main.Config.EventToolbarInfoPosition.ValueAssigned -= ConfigPositionChanged;
            Main.Config.EventToolbarInfoScale.ValueAssigned -= ConfigFloatChanged;
            Main.GameConfig.OptionsMenuClosed -= UpdateFromConfig;

            DrawInstance = null;
            BoxDrag = null;
        }

        void TextAPIDetected()
        {
            Label = new TextAPI.TextPackage(256, useShadow: UseShadowMessage, backgroundTexture: MyStringId.GetOrCompute("BuildInfo_UI_Square"));
            Label.Font = TextFont;
            Label.Scale = GUIScale;
            Label.Background.Width = 0f;
            Label.Background.Height = 0f;

            UpdateFromConfig();

            // render if toolbar menu is already open
            if(LastSeenBlocks.Count > 0 && Main.Config.EventToolbarInfo.Value && Main.TextAPI.IsEnabled)
            {
                RenderBox(LastSeenBlocks);
            }
        }

        void ConfigFloatChanged(float oldValue, float newValue, ConfigLib.SettingBase<float> setting)
        {
            UpdateFromConfig();
        }

        void ConfigPositionChanged(Vector2D oldValue, Vector2D newValue, ConfigLib.SettingBase<Vector2D> setting)
        {
            UpdateFromConfig();
        }

        void UpdateFromConfig()
        {
            GUIScale = (float)(TextScaleMultiplier * Main.Config.EventToolbarInfoScale.Value);

            if(Label != null)
            {
                Label.Scale = GUIScale;

                UpdateBgOpacity(BackgroundOpacity);
                UpdateBoxPosition();
                UpdateScale();
            }
        }

        void UpdateBoxPosition()
        {
            if(Label != null)
            {
                Label.Position = Main.Config.EventToolbarInfoPosition.Value;
            }
        }

        void UpdateScale()
        {
            Vector2D textSize = Label.Text.GetTextLength();

            float edge = (float)(BackgroundPadding * GUIScale);
            Vector2D textOffset = new Vector2D(-textSize.X, 0) - new Vector2D(edge / 2); // top-right pivot and offset by background margin aswell
            Label.Text.Offset = textOffset;
            if(Label.Shadow != null)
                Label.Shadow.Offset = textOffset + new Vector2D(ShadowOffset, -ShadowOffset);

            float bgWidth = (float)Math.Abs(textSize.X) + edge;
            float bgHeight = (float)Math.Abs(textSize.Y) + edge;

            Label.Background.Width = bgWidth;
            Label.Background.Height = bgHeight;
            Label.Background.Offset = new Vector2D(bgWidth * -0.5, bgHeight * -0.5); // make it centered
        }

        void UpdateBgOpacity(float opacity, Color? colorOverride = null)
        {
            if(Label == null)
                return;

            Color color = (colorOverride ?? BackgroundColor);
            Utils.FadeColorHUD(ref color, opacity);

            Label.Background.BillBoardColor = color;
        }

        void OpenedToolbarConfig(ListReader<IMyTerminalBlock> blocks)
        {
            LastSeenBlocks = blocks;

            if(Main.Config.EventToolbarInfo.Value && Main.TextAPI.IsEnabled && Label != null)
            {
                RenderBox(blocks);
            }
        }

        void ClosedToolbarConfig(ListReader<IMyTerminalBlock> blocks)
        {
            LastSeenBlocks = ListReader<IMyTerminalBlock>.Empty;
            TargetBlock = null;
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);

            if(Label != null)
            {
                Label.Visible = false;
            }
        }

        void RenderBox(ListReader<IMyTerminalBlock> blocks) // called once
        {
            TargetBlock = blocks[0];
            StringBuilder sb = Label.Text.Message.Clear();

            if(blocks.Count > 1)
            {
                sb.Color(HeaderColor).Append("Event Toolbar for ").Append(blocks.Count).Append(" blocks").ResetFormatting().Append('\n');
            }
            else
            {
                sb.Color(HeaderColor).Append("Event Toolbar for \"").AppendMaxLength(TargetBlock.CustomName, 24).Append("\"").ResetFormatting().Append('\n');
            }

            do
            {
                IMyButtonPanel button = TargetBlock as IMyButtonPanel;
                if(button != null)
                {
                    sb.Append("Each slot is a button.").Append('\n');

                    MyButtonPanelDefinition buttonDef = TargetBlock.SlimBlock.BlockDefinition as MyButtonPanelDefinition;

                    string dummyName = Main.EventToolbarMonitor.LastAimedUseObject?.Dummy?.Name;
                    BData_ButtonPanel data = Main.LiveDataHandler.Get<BData_ButtonPanel>(buttonDef);
                    BData_ButtonPanel.ButtonInfo buttonInfo;
                    if(dummyName != null && data != null && data.ButtonInfoByDummyName.TryGetValue(dummyName, out buttonInfo))
                    {
                        sb.Append("Aimed button is ").Color(SlotColor).Append("slot ").Append(buttonInfo.Index + 1).ResetFormatting().Append('\n');
                    }

                    if(blocks.Count > 1)
                    {
                        int buttonCountMin = int.MaxValue;
                        int buttonCountMax = int.MinValue;

                        for(int i = 0; i < blocks.Count; i++)
                        {
                            MyButtonPanelDefinition def = blocks[i].SlimBlock.BlockDefinition as MyButtonPanelDefinition;
                            if(def != null)
                            {
                                buttonCountMin = Math.Min(def.ButtonCount, buttonCountMin);
                                buttonCountMax = Math.Max(def.ButtonCount, buttonCountMax);
                            }
                        }

                        if(buttonDef.ButtonCount > buttonCountMin)
                        {
                            sb.Color(Color.Yellow).Append("NOTE: ").ResetFormatting().Append("some blocks have less buttons, lowest: ").Append(buttonCountMin).Append('\n');
                        }
                        else if(buttonDef.ButtonCount < buttonCountMax)
                        {
                            sb.Color(Color.Yellow).Append("NOTE: ").ResetFormatting().Append("some blocks have more buttons, highest: ").Append(buttonCountMax).Append('\n');
                        }
                    }

                    break;
                }

                IMyAirVent vent = TargetBlock as IMyAirVent;
                if(vent != null)
                {
                    sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(" triggers when room is filled.").Append('\n');
                    sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(" triggers when room is emptied.").Append('\n');
                    break;
                }

                IMySensorBlock sensor = TargetBlock as IMySensorBlock;
                if(sensor != null)
                {
                    sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(" triggers on first detection.").Append('\n');
                    sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(" triggers when nothing is detected anymore.").Append('\n');
                    break;
                }

                IMyTargetDummyBlock targetDummy = TargetBlock as IMyTargetDummyBlock;
                if(targetDummy != null)
                {
                    sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(" triggers when dummy is hit (or destroyed).").Append('\n');
                    sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(" triggers when dummy is destroyed.").Append('\n');
                    break;
                }

                IMyRemoteControl rc = TargetBlock as IMyRemoteControl;
                if(rc != null)
                {
                    sb.Append("All slots trigger when waypoint is reached.").Append('\n');
                    break;
                }

                IMyTimerBlock timer = TargetBlock as IMyTimerBlock;
                if(timer != null)
                {
                    sb.Append("All slots trigger when timer finishes counting.").Append('\n');
                    break;
                }

                return; // unknown block, don't draw
            }
            while(false);

            if(Main.Config.ToolbarLabelsHeader.Value)
            {
                sb.Color(ModNameColor).Append("<i>(").Append(BuildInfoMod.ModName).Append(" Mod)").NewCleanLine();
            }

            sb.TrimEndWhitespace();

            if(UseShadowMessage)
                TextAPI.CopyWithoutColor(sb, Label.Shadow.Message);

            UpdateScale();

            Label.Visible = true;

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true); // for dragging and overlay
        }

        public override void UpdateDraw()
        {
            if(TargetBlock == null) // redundancy
            {
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
                return;
            }

            #region Draggable box update
            Vector2D center = Label.Background.Origin + Label.Background.Offset;
            Vector2D halfExtents = new Vector2D(Label.Background.Width, Label.Background.Height) / 2;
            BoxDrag.DragHitbox = new BoundingBox2D(center - halfExtents, center + halfExtents);
            BoxDrag.Position = Label.Text.Origin;
            BoxDrag.Update();
            #endregion

            #region Overlays
            IMyButtonPanel button = TargetBlock as IMyButtonPanel;
            if(button != null)
            {
                SpecializedOverlayBase overlay = Main.SpecializedOverlays.Get(TargetBlock.BlockDefinition.TypeId);
                if(overlay != null)
                {
                    MatrixD drawMatrix = TargetBlock.WorldMatrix;
                    overlay.Draw(ref drawMatrix, DrawInstance, (MyCubeBlockDefinition)TargetBlock.SlimBlock.BlockDefinition, TargetBlock.SlimBlock);
                }
            }
            #endregion
        }
    }
}
