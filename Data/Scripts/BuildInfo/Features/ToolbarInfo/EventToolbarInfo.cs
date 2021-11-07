using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.Overlays;
using Digi.BuildInfo.Features.Overlays.Specialized;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Entity.UseObject;
using VRage.Game.ModAPI;
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

        readonly Color BackgroundColor = new Color(41, 54, 62);
        const BlendTypeEnum BlendType = BlendTypeEnum.PostPP;

        const string TextFont = "white";
        const double TextScaleMultiplier = 1f;
        const double ShadowOffset = 0.002;
        const float BackgroundPadding = 0.03f;

        IMyUseObject LastAimedUseObject;
        IMyTerminalBlock TargetBlock;

        Vector2D GUIPosition = new Vector2D(0.5, -0.5);
        double GUIScale;

        Vector2D GuiPositionOffset = new Vector2D(0, 0);

        HudAPIv2.BillBoardHUDMessage Background;
        HudAPIv2.HUDMessage TextShadow;
        HudAPIv2.HUDMessage Text;

        OverlayDrawInstance DrawInstance;

        public EventToolbarInfo(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            MyCharacterDetectorComponent.OnInteractiveObjectChanged += UseObjectChanged;

            Main.GUIMonitor.ScreenAdded += GUIScreenAdded;
            Main.GUIMonitor.ScreenRemoved += GUIScreenRemoved;
            Main.TextAPI.Detected += TextAPIDetected;

            Main.Config.ToolbarLabelsInMenuPosition.ValueAssigned += ConfigPositionChanged;
            Main.Config.ToolbarLabelsScale.ValueAssigned += ConfigFloatChanged;
            Main.GameConfig.OptionsMenuClosed += UpdateFromConfig;

            DrawInstance = new OverlayDrawInstance(Main.Overlays, GetType().Name);
            DrawInstance.LabelRender.ForceDrawLabel = true;
        }

        public override void UnregisterComponent()
        {
            MyCharacterDetectorComponent.OnInteractiveObjectChanged -= UseObjectChanged;

            if(!Main.ComponentsRegistered)
                return;

            Main.GUIMonitor.ScreenAdded -= GUIScreenAdded;
            Main.GUIMonitor.ScreenRemoved -= GUIScreenRemoved;
            Main.TextAPI.Detected -= TextAPIDetected;

            Main.Config.ToolbarLabelsInMenuPosition.ValueAssigned -= ConfigPositionChanged;
            Main.Config.ToolbarLabelsScale.ValueAssigned -= ConfigFloatChanged;
            Main.GameConfig.OptionsMenuClosed -= UpdateFromConfig;
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
            GUIPosition = Main.Config.ToolbarLabelsInMenuPosition.Value;
            GUIScale = (float)(TextScaleMultiplier * Main.Config.ToolbarLabelsScale.Value);

            if(Text != null)
            {
                TextShadow.Scale = GUIScale;
                Text.Scale = GUIScale;

                // compute vertical text size of a single line, for offsetting
                Text.Message.Clear().Append("SampleText");
                Vector2D textSize = Text.GetTextLength();
                GuiPositionOffset = new Vector2D(0, Math.Abs(textSize.Y) * 3); // offset 3 lines
                Text.Message.Clear();

                //Text.Origin = GUIPosition;
                //TextShadow.Origin = GUIPosition;
                //Background.Origin = GUIPosition;

                UpdateBgOpacity(Main.GameConfig.UIBackgroundOpacity);
            }
        }

        void UpdateBgOpacity(float opacity, Color? colorOverride = null)
        {
            if(Background == null)
                return;

            Color color = (colorOverride ?? BackgroundColor);
            Utils.FadeColorHUD(ref color, opacity);

            Background.BillBoardColor = color;
        }

        void TextAPIDetected()
        {
            const int SBCapacity = 512;

            // creation order important for draw order
            Background = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("BuildInfo_UI_Square"), GUIPosition, Color.White);
            Background.Visible = false;
            Background.Blend = BlendType;
            Background.Options = HudAPIv2.Options.HideHud;
            Background.Width = 0f;
            Background.Height = 0f;

            TextShadow = new HudAPIv2.HUDMessage(new StringBuilder(SBCapacity), GUIPosition, HideHud: true, Scale: GUIScale, Font: TextFont, Blend: BlendType);
            TextShadow.InitialColor = Color.Black;
            TextShadow.Visible = false;

            Text = new HudAPIv2.HUDMessage(new StringBuilder(SBCapacity), GUIPosition, HideHud: true, Scale: GUIScale, Font: TextFont, Blend: BlendType);
            Text.Visible = false;

            UpdateFromConfig();
        }

        void UseObjectChanged(IMyUseObject useObject)
        {
            LastAimedUseObject = useObject;
        }

        void GUIScreenAdded(string screenTypeName)
        {
            if(!Main.GUIMonitor.InAnyToolbarGUI || !Main.TextAPI.IsEnabled || Text == null)
                return;

            TargetBlock = null;

            List<IMyTerminalBlock> selectedInTerminal = Main.TerminalInfo.SelectedInTerminal;
            if(selectedInTerminal.Count > 0 && Main.GUIMonitor.Screens.Contains("MyGuiScreenTerminal")) // HACK: should find a better way to tell
            {
                TargetBlock = selectedInTerminal[0]; // only first matters, if there's more then it's not going to matter unless I use specifics from them.
            }
            else
            {
                TargetBlock = LastAimedUseObject?.Owner as IMyTerminalBlock;
            }

            if(TargetBlock == null)
                return;

            StringBuilder sb = Text.Message.Clear();

            sb.Color(HeaderColor).Append("Event Toolbar for \"").Append(TargetBlock.CustomName).Append("\"");

            if(Main.Config.ToolbarLabelsHeader.Value)
            {
                sb.Color(ModNameColor).Append(" <i>(").Append(BuildInfoMod.ModName).Append(" Mod)");
            }

            sb.ResetFormatting().Append('\n');

            do
            {
                IMyButtonPanel button = TargetBlock as IMyButtonPanel;
                if(button != null)
                {
                    // show overlay too
                    SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);

                    sb.Append("Each slot represents a button on the block, in seen order.");

                    string dummyName = LastAimedUseObject?.Dummy?.Name;
                    if(dummyName != null)
                    {
                        BData_ButtonPanel data = Main.LiveDataHandler.Get<BData_ButtonPanel>((MyCubeBlockDefinition)TargetBlock.SlimBlock.BlockDefinition);
                        if(data != null)
                        {
                            BData_ButtonPanel.ButtonInfo buttonInfo;
                            if(data.ButtonInfoByDummyName.TryGetValue(dummyName, out buttonInfo))
                            {
                                sb.Append("\nAimed-at button is in ").Color(SlotColor).Append("slot ").Append(buttonInfo.Index + 1);
                            }
                        }
                    }
                    break;
                }

                IMyRemoteControl rc = TargetBlock as IMyRemoteControl;
                if(rc != null)
                {
                    sb.Append("All these slots will trigger when the selected waypoint is reached.");
                    break;
                }

                IMyAirVent vent = TargetBlock as IMyAirVent;
                if(vent != null)
                {
                    sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(" triggers when room is filled.\n");
                    sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(" triggers when room is emptied.");
                    break;
                }

                IMySensorBlock sensor = TargetBlock as IMySensorBlock;
                if(sensor != null)
                {
                    sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(" triggers on first detection entering area.\n");
                    sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(" triggers when nothing is detected anymore.");
                    break;
                }

                IMyTargetDummyBlock targetDummy = TargetBlock as IMyTargetDummyBlock;
                if(targetDummy != null)
                {
                    sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(" triggers when dummy is hit.\n");
                    sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(" triggers when dummy is destroyed.");
                    break;
                }

                return; // unknown block, don't draw
            }
            while(false);

            TextAPI.CopyWithoutColor(sb, TextShadow.Message);

            Vector2D textSize = Text.GetTextLength();

            Vector2D halfEdgeVec = new Vector2D(BackgroundPadding / 2);
            Vector2D textOffset = new Vector2D(0, -textSize.Y); // bottom-left pivot
            Text.Offset = textOffset + halfEdgeVec;
            TextShadow.Offset = textOffset + halfEdgeVec + new Vector2D(ShadowOffset, -ShadowOffset);

            float bgWidth = (float)Math.Abs(textSize.X) + BackgroundPadding;
            float bgHeight = (float)Math.Abs(textSize.Y) + BackgroundPadding;

            Background.Width = bgWidth;
            Background.Height = bgHeight;
            Background.Offset = new Vector2D(bgWidth / 2, bgHeight / 2);

            Vector2D position = GUIPosition + GuiPositionOffset;
            Background.Origin = position;
            TextShadow.Origin = position;
            Text.Origin = position;

            Text.Visible = true;
            TextShadow.Visible = true;
            Background.Visible = true;
        }

        void GUIScreenRemoved(string screenTypeName)
        {
            if(Text != null && !Main.GUIMonitor.InAnyToolbarGUI)
            {
                Text.Visible = false;
                TextShadow.Visible = false;
                Background.Visible = false;

                TargetBlock = null;
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
            }
        }

        public override void UpdateDraw()
        {
            if(TargetBlock == null)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
                return;
            }

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
        }
    }
}
