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

            if(!RenderBoxContent(sb, blocks))
                return;

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

        bool RenderBoxContent(StringBuilder sb, ListReader<IMyTerminalBlock> blocks)
        {
            // TODO: event toolbar for event controller
            // HACK: backwards compatible
            //#if !(VERSION_190 || VERSION_191 || VERSION_192 || VERSION_193 || VERSION_194 || VERSION_195 || VERSION_196 || VERSION_197 || VERSION_198 || VERSION_199 || VERSION_200 || VERSION_201)
            //IMyEventControllerBlock eventController = TargetBlock as IMyEventControllerBlock;
            //if(eventController != null)
            //{
            //    string eventName = null;
            //
            //    foreach(MyComponentBase comp in eventController.Components)
            //    {
            //        IMyEventControllerEntityComponent eventComp = comp as IMyEventControllerEntityComponent;
            //        if(eventComp != null && eventComp.IsSelected)
            //        {
            //            eventName = MyTexts.GetString(eventComp.EventDisplayName);
            //            break;
            //        }
            //    }
            //
            //    if(eventName != null)
            //    {
            //        const int TitleMaxLen = 32;
            //        string title;
            //        if(eventName.Length > TitleMaxLen)
            //            title = $"'{eventName.Substring(0, TitleMaxLen)}...' toolbar for ";
            //        else
            //            title = $"'{eventName}' toolbar for ";
            //
            //        RenderBoxHeader(sb, blocks.Count, title);
            //    }
            //    else
            //        RenderBoxHeader(sb, blocks.Count, "'(No event)' toolbar for ");
            //
            //    sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": ").Append("when condition is true").Append('\n');
            //    sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": ").Append("when condition is false").Append('\n');
            //    sb.Append("Other pages work the same way.\n");
            //    sb.Append("Same action can be used in both slots by using different pages.\n");
            //    return true;
            //}
            //#endif

            IMyButtonPanel button = TargetBlock as IMyButtonPanel;
            if(button != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Append("Each slot is a button.\n");

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

                return true;
            }

            IMyAirVent vent = TargetBlock as IMyAirVent;
            if(vent != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": room pressurized\n");
                sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": room no longer pressurized\n");
                return true;
            }

            IMySensorBlock sensor = TargetBlock as IMySensorBlock;
            if(sensor != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": on first detection\n");
                sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": when nothing is detected anymore\n");
                return true;
            }

            IMyTargetDummyBlock targetDummy = TargetBlock as IMyTargetDummyBlock;
            if(targetDummy != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": dummy is hit (or destroyed)\n");
                sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": dummy is destroyed\n");
                return true;
            }

            IMyShipController shipCtrl = TargetBlock as IMyShipController;
            if(shipCtrl != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                if(Main.EventToolbarMonitor.LastOpenedToolbarType == EventToolbarMonitor.ToolbarType.LockOnVictim)
                {
                    sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": once this ship is locked on\n");
                    sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": no longer locked on\n");
                    return true;
                }

                if(TargetBlock is IMyRemoteControl && Main.EventToolbarMonitor.LastOpenedToolbarType == EventToolbarMonitor.ToolbarType.RCWaypoint)
                {
                    sb.Append("All slots: waypoint reached\n");
                    return true;
                }
            }

            IMyTimerBlock timer = TargetBlock as IMyTimerBlock;
            if(timer != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Append("All slots: timer countdown reached\n");
                return true;
            }

            IMyTurretControlBlock tcb = TargetBlock as IMyTurretControlBlock;
            if(tcb != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": turret aligned with target (angle deviation)\n");
                sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": turret no longer aligned with target\n");
                return true;
            }

            return false; // unknown block, don't draw
        }

        void RenderBoxHeader(StringBuilder sb, int blockCount, string customTitle = null, bool includeBlocks = true)
        {
            sb.Color(HeaderColor);

            if(customTitle != null)
            {
                sb.Append(customTitle);
            }
            else
            {
                switch(Main.EventToolbarMonitor.LastOpenedToolbarType)
                {
                    default: sb.Append("Event Toolbar for "); break;
                    case EventToolbarMonitor.ToolbarType.RCWaypoint: sb.Append("Waypoint Event Toolbar for "); break;
                    case EventToolbarMonitor.ToolbarType.LockOnVictim: sb.Append("LockOn Event Toolbar for "); break;
                }
            }

            if(includeBlocks)
            {
                if(blockCount > 1)
                {
                    sb.Append(blockCount).Append(" blocks");
                }
                else
                {
                    sb.Append("\"").AppendMaxLength(TargetBlock.CustomName, 24).Append("\"");
                }
            }

            sb.ResetFormatting().Append('\n');
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
