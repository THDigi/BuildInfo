using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using BlendType = VRageRender.MyBillboard.BlendTypeEnum;

// TODO: alternate rendering for gamepad HUD mode

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    /// <summary>
    /// TextAPI-based label rendering
    /// </summary>
    public class ToolbarLabelRender : ModComponent
    {
        public const int MaxBlockNameLength = 32; // last X characters
        public const int MaxActionNameLength = 28; // first X characters
        public const int MaxArgLength = 24; // first X characters

        public const int ShowForTicks = (int)(Constants.TICKS_PER_SECOND * 3f);

        Vector2D PosOnHUD = new Vector2D(-0.3, -0.75);
        Vector2D PosInGUI = new Vector2D(0.5, -0.5);

        const BlendType TextBlendType = BlendType.PostPP;
        readonly Color BackgroundColor = new Color(41, 54, 62);
        readonly Color BackgroundColorSelected = new Color(40, 80, 65);
        const string TextFont = "white";
        const double TextScaleMultiplier = 0.75;
        const double ShadowOffset = 0.002;
        const double BackgroundPadding = 0.03;
        const double CornerSize = 0.02;

        ToolbarLabelsMode LabelsMode;
        ToolbarNameMode NamesMode;
        float Scale;

        bool MustBeVisible;
        bool WereVisible;

        bool InToolbarConfig;
        bool? WasInToolbarConfig;

        int ForceRefreshAtTick;

        int ShowUntilTick;
        bool BeenFaded = false;

        int RenderedAtTick = -1;

        Vector2D? ClickOffset;
        Vector2D TextSize;
        bool InTextAPIMenu;
        bool SelectedBox;

        HudAPIv2.BillBoardHUDMessage Background;
        HudAPIv2.BillBoardHUDMessage BackgroundBottom;
        HudAPIv2.BillBoardHUDMessage CornerBotomLeft;
        HudAPIv2.BillBoardHUDMessage BackgroundTop;
        HudAPIv2.BillBoardHUDMessage CornerTopRight;
        HudAPIv2.HUDMessage Shadows;
        HudAPIv2.HUDMessage Labels;
        List<HudAPIv2.BillBoardHUDMessage> Backgrounds;

        public ToolbarLabelRender(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM | UpdateFlags.UPDATE_INPUT;
        }

        protected override void RegisterComponent()
        {
            TextAPI.Detected += TextAPIDetected;

            Config.ToolbarLabelsInMenuPosition.ValueAssigned += ConfigPositionChanged;
            Config.ToolbarLabelsPosition.ValueAssigned += ConfigPositionChanged;
            Config.ToolbarLabelsScale.ValueAssigned += ConfigFloatChanged;
            Config.ToolbarItemNameMode.ValueAssigned += ConfigIntChanged;
            Config.ToolbarLabels.ValueAssigned += ConfigIntChanged;
            GameConfig.OptionsMenuClosed += UpdateFromConfig;

            Main.EquipmentMonitor.ControlledChanged += EquipmentMonitor_ControlledChanged;

            Main.ToolbarMonitor.ToolbarPageChanged += UpdateRender;

            MyVisualScriptLogicProvider.PlayerEnteredCockpit += EnteredCockpit;
        }

        protected override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            TextAPI.Detected -= TextAPIDetected;

            Config.ToolbarLabelsInMenuPosition.ValueAssigned -= ConfigPositionChanged;
            Config.ToolbarLabelsPosition.ValueAssigned -= ConfigPositionChanged;
            Config.ToolbarLabelsScale.ValueAssigned -= ConfigFloatChanged;
            Config.ToolbarItemNameMode.ValueAssigned -= ConfigIntChanged;
            Config.ToolbarLabels.ValueAssigned -= ConfigIntChanged;
            GameConfig.OptionsMenuClosed -= UpdateFromConfig;

            Main.EquipmentMonitor.ControlledChanged -= EquipmentMonitor_ControlledChanged;

            Main.ToolbarMonitor.ToolbarPageChanged -= UpdateRender;

            MyVisualScriptLogicProvider.PlayerEnteredCockpit -= EnteredCockpit;
        }

        void ConfigIntChanged(int oldValue, int newValue, ConfigLib.SettingBase<int> setting)
        {
            UpdateFromConfig();
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
            LabelsMode = (ToolbarLabelsMode)Config.ToolbarLabels.Value;
            NamesMode = (ToolbarNameMode)Config.ToolbarItemNameMode.Value;
            PosInGUI = Config.ToolbarLabelsInMenuPosition.Value;
            PosOnHUD = Config.ToolbarLabelsPosition.Value;
            Scale = (float)(TextScaleMultiplier * Config.ToolbarLabelsScale.Value);

            WasInToolbarConfig = null; // force origin refresh

            if(Labels != null)
            {
                Shadows.Scale = Scale;
                Labels.Scale = Scale;
                UpdateBgOpacity(GameConfig.HudBackgroundOpacity);
            }
        }

        void UpdateBgOpacity(float opacity, Color? colorOverride = null)
        {
            if(Backgrounds == null)
                return;

            var color = (colorOverride.HasValue ? colorOverride.Value : BackgroundColor);

            // HACK: matching vanilla HUD transparency better
            color *= opacity * (opacity * 1.075f);
            color.A = (byte)(opacity * 255);

            foreach(var bg in Backgrounds)
            {
                bg.BillBoardColor = color;
            }
        }

        void TextAPIDetected()
        {
            Backgrounds = new List<HudAPIv2.BillBoardHUDMessage>(6);

            // creation order important for draw order
            Background = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("BuildInfo_Square"), PosOnHUD, Color.White, HideHud: true, Blend: TextBlendType);
            Background.Visible = false;
            Backgrounds.Add(Background);

            BackgroundTop = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("BuildInfo_Square"), PosOnHUD, Color.White, HideHud: true, Blend: TextBlendType);
            BackgroundTop.Visible = false;
            Backgrounds.Add(BackgroundTop);

            CornerTopRight = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("BuildInfo_UI_Corner"), PosOnHUD, Color.White, HideHud: true, Blend: TextBlendType);
            CornerTopRight.Visible = false;
            CornerTopRight.Rotation = MathHelper.Pi;
            Backgrounds.Add(CornerTopRight);

            BackgroundBottom = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("BuildInfo_Square"), PosOnHUD, Color.White, HideHud: true, Blend: TextBlendType);
            BackgroundBottom.Visible = false;
            Backgrounds.Add(BackgroundBottom);

            CornerBotomLeft = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("BuildInfo_UI_Corner"), PosOnHUD, Color.White, HideHud: true, Blend: TextBlendType);
            CornerBotomLeft.Visible = false;
            Backgrounds.Add(CornerBotomLeft);

            foreach(var bg in Backgrounds)
            {
                bg.Width = 0f;
                bg.Height = 0f;
            }

            Shadows = new HudAPIv2.HUDMessage(new StringBuilder(512), PosOnHUD, HideHud: true, Scale: Scale, Font: TextFont, Blend: TextBlendType);
            Shadows.InitialColor = Color.Black;
            Shadows.Visible = false;

            Labels = new HudAPIv2.HUDMessage(new StringBuilder(512), PosOnHUD, HideHud: true, Scale: Scale, Font: TextFont, Blend: TextBlendType);
            Labels.Visible = false;

            WereVisible = false;

            UpdateFromConfig();
        }

        void EnteredCockpit(string entityName, long playerId, string gridName)
        {
            try
            {
                Utils.AssertMainThread();

                var player = MyAPIGateway.Session?.Player;
                if(player != null && player.IdentityId == playerId)
                {
                    ShowUntilTick = Main.Tick + ShowForTicks;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        //HudAPIv2.HUDMessage DebugMousePos;

        protected override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(ClickOffset.HasValue && MyAPIGateway.Input.IsNewLeftMouseReleased())
            {
                ClickOffset = null;
                Config.Save();
            }

            // TODO: move to TextAPI module?
            if(!InTextAPIMenu && MyAPIGateway.Gui.ChatEntryVisible && MyAPIGateway.Input.IsNewKeyPressed(VRage.Input.MyKeys.F2))
            {
                InTextAPIMenu = true;
            }

            if(InTextAPIMenu && !MyAPIGateway.Gui.ChatEntryVisible)
            {
                InTextAPIMenu = false;
            }

            if(MustBeVisible && (InToolbarConfig || InTextAPIMenu))
            {
                var screenSize = MyAPIGateway.Input.GetMouseAreaSize();
                var mousePos = MyAPIGateway.Input.GetMousePosition() / screenSize;
                var mouseOnScreen = new Vector2D(mousePos.X * 2 - 1, 1 - 2 * mousePos.Y); // turn from 0~1 to -1~1

                Vector2D bottomLeftPos = (InToolbarConfig ? PosInGUI : PosOnHUD);
                float edge = (float)(BackgroundPadding * Scale);
                var box = new BoundingBox2D(bottomLeftPos, bottomLeftPos + new Vector2D(Math.Abs(TextSize.X), Math.Abs(TextSize.Y)) + edge);
                box.Min = Vector2D.Min(box.Min, box.Max);
                box.Max = Vector2D.Max(box.Min, box.Max);

                //if(DebugMousePos == null)
                //    DebugMousePos = new HudAPIv2.HUDMessage(new StringBuilder(128), Vector2D.Zero, Shadowing: true, Blend: BlendType.PostPP);
                //DebugMousePos.Origin = Config.ToolbarLabelsInMenuPosition.Value + new Vector2D(-0.1, 0.4);
                //DebugMousePos.Message.Clear().Append($"MousePos={mousePos.X:0.##},{mousePos.Y:0.##}" +
                //    $"\nMouseOnScreen={mouseOnScreen.X:0.##},{mouseOnScreen.Y:0.##}" +
                //    $"\nTextSize={TextSize.X:0.##},{TextSize.Y:0.##}" +
                //    $"\nBoxMin={box.Min.X:0.##},{box.Min.Y:0.##}; Max={box.Max.X:0.##},{box.Max.Y:0.##}");

                if(box.Contains(mouseOnScreen) == ContainmentType.Contains)
                {
                    if(!SelectedBox)
                    {
                        SelectedBox = true;
                        UpdateBgOpacity(InToolbarConfig ? 1f : Math.Min(1f, GameConfig.HudBackgroundOpacity * 1.2f), BackgroundColorSelected);
                    }

                    if(MyAPIGateway.Input.IsNewLeftMousePressed())
                    {
                        if(InToolbarConfig)
                            ClickOffset = Config.ToolbarLabelsInMenuPosition.Value - mouseOnScreen;
                        else
                            ClickOffset = Config.ToolbarLabelsPosition.Value - mouseOnScreen;
                    }

                    if(ClickOffset.HasValue && MyAPIGateway.Input.IsLeftMousePressed())
                    {
                        var newPos = mouseOnScreen + ClickOffset.Value;
                        newPos = new Vector2D(Math.Round(newPos.X, 3), Math.Round(newPos.Y, 3));
                        newPos = Vector2D.Clamp(newPos, -Vector2D.One, Vector2D.One);

                        if(InToolbarConfig)
                            Config.ToolbarLabelsInMenuPosition.Value = newPos;
                        else
                            Config.ToolbarLabelsPosition.Value = newPos;
                    }
                }
                else
                {
                    if(SelectedBox)
                    {
                        SelectedBox = false;
                        UpdateBgOpacity(InToolbarConfig ? 1f : GameConfig.HudBackgroundOpacity);
                    }
                }
            }
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(Backgrounds == null)
                return;

            MustBeVisible = (TextAPIEnabled && LabelsMode != ToolbarLabelsMode.Off && MyAPIGateway.SpectatorTools.GetMode() == MyCameraMode.None);

            if(MustBeVisible)
            {
                var shipController = MyAPIGateway.Session.ControlledObject as MyShipController;
                if(shipController == null || shipController.BuildingMode)
                    MustBeVisible = false;
            }

            if(!ToolbarMonitor.EnableGamepadSupport && MustBeVisible && MyAPIGateway.Input.IsJoystickLastUsed)
            {
                MustBeVisible = false;
            }

            if(MustBeVisible)
            {
                // if pressing alt in the right mode it just ignores the cockpit entering fade stuff
                if(ShowUntilTick > tick && (LabelsMode == ToolbarLabelsMode.AltKey || LabelsMode == ToolbarLabelsMode.HudHints) && MyAPIGateway.Input.IsAnyAltKeyPressed())
                {
                    ShowUntilTick = 0;

                    if(BeenFaded)
                        UpdateBgOpacity(GameConfig.HudBackgroundOpacity);
                }

                // show and fade out when entering cockpit
                if(ShowUntilTick > tick && LabelsMode != ToolbarLabelsMode.AlwaysOn)
                {
                    MustBeVisible = true;

                    float fadeSection = ((float)ShowForTicks * 0.5f);
                    float opacity = GameConfig.HudBackgroundOpacity;
                    if(ShowUntilTick <= tick + fadeSection)
                        opacity = MathHelper.Lerp(0, opacity, (ShowUntilTick - tick) / fadeSection);

                    UpdateBgOpacity(opacity);
                    BeenFaded = true;

                    // TODO: fade text+shadow too
                }
                else
                {
                    // reset colors after fade
                    if(BeenFaded)
                    {
                        UpdateBgOpacity(GameConfig.HudBackgroundOpacity);
                        BeenFaded = false;
                    }

                    if(LabelsMode == ToolbarLabelsMode.HudHints && !(Main.GameConfig.HudState == HudState.HINTS || MyAPIGateway.Input.IsAnyAltKeyPressed()))
                    {
                        MustBeVisible = false;
                    }

                    if(LabelsMode == ToolbarLabelsMode.AltKey && !MyAPIGateway.Input.IsAnyAltKeyPressed())
                    {
                        MustBeVisible = false;
                    }
                }
            }

            if(MustBeVisible)
            {
                string screenName = MyAPIGateway.Gui.ActiveGamePlayScreen;
                InToolbarConfig = (screenName == "MyGuiScreenCubeBuilder");
                if(MyAPIGateway.Gui.IsCursorVisible && !InToolbarConfig)
                    MustBeVisible = false;
            }

            if(!WasInToolbarConfig.HasValue || InToolbarConfig != WasInToolbarConfig.Value)
            {
                var bottomLeftPos = (InToolbarConfig ? PosInGUI : PosOnHUD);

                foreach(var bg in Backgrounds)
                {
                    bg.Origin = bottomLeftPos;
                }

                Labels.Origin = bottomLeftPos;
                Shadows.Origin = bottomLeftPos;

                WasInToolbarConfig = InToolbarConfig;

                UpdateBgOpacity(InToolbarConfig ? 1f : GameConfig.HudBackgroundOpacity);

                // refresh instantly to update names
                if(MustBeVisible && NamesMode == ToolbarNameMode.InMenuOnly)
                    UpdateRender();
            }

            if(MustBeVisible != WereVisible)
            {
                foreach(var bg in Backgrounds)
                {
                    bg.Visible = MustBeVisible;
                }

                Shadows.Visible = MustBeVisible;
                Labels.Visible = MustBeVisible;

                WereVisible = MustBeVisible;

                if(MustBeVisible)
                    UpdateRender();
            }

            if(MustBeVisible)
            {
                if(ForceRefreshAtTick == tick || tick % 60 == 0)
                {
                    UpdateRender();
                }
            }
        }

        void EquipmentMonitor_ControlledChanged(VRage.Game.ModAPI.Interfaces.IMyControllableEntity controlled)
        {
            UpdateRender();
        }

        void UpdateRender()
        {
            if(Labels == null)
                return;

            if(!MustBeVisible)
                return;

            // avoid re-triggering same tick
            if(RenderedAtTick == Main.Tick)
                return;
            RenderedAtTick = Main.Tick;

            bool gamepadHUD = ToolbarMonitor.EnableGamepadSupport && MyAPIGateway.Input.IsJoystickLastUsed;
            int slotsPerPage = (gamepadHUD ? ToolbarMonitor.SlotsPerPageGamepad : ToolbarMonitor.SlotsPerPage);
            int toolbarPage = (gamepadHUD ? Main.ToolbarMonitor.GamepadToolbarPage : Main.ToolbarMonitor.ToolbarPage);
            int startIndex = (gamepadHUD ? toolbarPage * ToolbarMonitor.SlotsPerPageGamepad : toolbarPage * ToolbarMonitor.SlotsPerPage);
            int maxIndexPage = (startIndex + slotsPerPage - 1);
            int highestUsedIndex = Main.ToolbarMonitor.HighestIndexUsed;
            int maxUsedIndex = Math.Min(highestUsedIndex, maxIndexPage);

            if(ForceRefreshAtTick != Main.Tick)
            {
                // if any slot is partially filled (needs info from wrapper) then skip this tick.
                for(int i = startIndex; i <= maxUsedIndex; i++)
                {
                    var item = Main.ToolbarMonitor.Slots[i];
                    if(item.ActionId != null && item.ActionWrapper == null)
                    {
                        // wait until wrapper fills in the details
                        ForceRefreshAtTick = Main.Tick + 1;
                        return;
                    }
                }
            }

            double topLinesWidth = 0;

            var sb = Labels.Message.Clear();

            for(int i = 0; i < slotsPerPage; i++)
            {
                int index = startIndex + i;
                var item = Main.ToolbarMonitor.Slots[index];
                if(item.Name == null)
                    sb.Color(Color.Gray);

                if(gamepadHUD)
                    sb.Append(Constants.DPAD_CHARS[i]).Append("  ");
                else
                    sb.Append(i + 1).Append(". ");

                if(item.Name == null)
                {
                    sb.Append("—").ResetFormatting().NewLine();
                    continue;
                }

                if(item.ActionId != null && item.Block != null)
                {
                    if(item.CustomLabel != null)
                    {
                        sb.AppendMaxLength(item.CustomLabel, ToolbarCustomLabels.CustomLabelMaxLength).ResetFormatting();
                    }
                    else
                    {
                        if(NamesMode == ToolbarNameMode.AlwaysShow
                        || (NamesMode == ToolbarNameMode.GroupsOnly && item.GroupName != null)
                        || (NamesMode == ToolbarNameMode.InMenuOnly && InToolbarConfig))
                        {
                            if(item.GroupName != null)
                                sb.Color(new Color(155, 220, 255)).Append('*');

                            string blockName = item.GroupName ?? item.Name;
                            int blockNameLen = blockName.Length;
                            if(blockNameLen > MaxBlockNameLength)
                                sb.Append("...").Append(blockName, blockNameLen - MaxBlockNameLength, MaxBlockNameLength);
                            else
                                sb.Append(blockName);

                            if(item.GroupName != null)
                                sb.Append('*'); // .ResetFormatting();

                            sb.Color(Color.Gray).Append(" - ").ResetFormatting();
                        }

                        string actionName = item.ActionName;
                        int actionNameLen = actionName.Length;
                        if(actionNameLen > MaxActionNameLength)
                            sb.Append(actionName, 0, MaxActionNameLength).Append("...");
                        else
                            sb.Append(actionName);

                        if(item.PBArgument != null)
                        {
                            sb.Append(": <i>").Color(new Color(55, 200, 155));

                            string arg = item.PBArgument;
                            int argLen = arg.Length;
                            if(argLen > MaxArgLength)
                                sb.Append(arg, 0, MaxArgLength).Append("...");
                            else
                                sb.Append(arg);

                            sb.ResetFormatting();
                        }
                    }
                }
                else if(item.Name != null)
                {
                    sb.Color(new Color(255, 220, 155));

                    string name = item.CustomLabel ?? item.Name;
                    int nameLen = name.Length;
                    if(nameLen > MaxBlockNameLength)
                        sb.Append("...").Append(name, nameLen - MaxBlockNameLength, MaxBlockNameLength);
                    else
                        sb.Append(name);

                    sb.ResetFormatting();
                }

                if(i == 1)
                {
                    topLinesWidth = Labels.GetTextLength().X;
                }

                sb.NewLine();
            }

            // remove last new line
            if(sb.Length > 0)
                sb.Length -= 1;

            #region Update shadow SB
            var shadowSB = Shadows.Message.Clear();
            shadowSB.EnsureCapacity(sb.Length);

            // append to shadow without color tags
            for(int i = 0; i < sb.Length; ++i)
            {
                char c = sb[i];

                // skip <color=...>
                if(c == '<' && i + 6 <= sb.Length)
                {
                    if(sb[i + 1] == 'c'
                    && sb[i + 2] == 'o'
                    && sb[i + 3] == 'l'
                    && sb[i + 4] == 'o'
                    && sb[i + 5] == 'r'
                    && sb[i + 6] == '=')
                    {
                        // seek ahead for end char
                        int endChar = -1;
                        for(int s = i + 6; s < sb.Length; s++)
                        {
                            if(sb[s] == '>')
                            {
                                endChar = s;
                                break;
                            }
                        }

                        if(endChar != -1)
                        {
                            i = endChar;
                            continue;
                        }
                    }
                }

                shadowSB.Append(c);
            }
            #endregion Update shadow SB

            TextSize = Labels.GetTextLength();

            float cornerHeight = (float)(CornerSize * Scale);
            float cornerWidth = (float)(cornerHeight / GameConfig.AspectRatio);

            float edge = (float)(BackgroundPadding * Scale);
            float bgWidth = (float)Math.Abs(TextSize.X) + edge;
            float bgHeight = (float)Math.Abs(TextSize.Y) + edge;
            Vector2D halfEdgeVec = new Vector2D(edge / 2);

            var textOffset = new Vector2D(0, -TextSize.Y); // bottom-left pivot
            Labels.Offset = textOffset + halfEdgeVec;
            Shadows.Offset = textOffset + new Vector2D(ShadowOffset, -ShadowOffset) + halfEdgeVec;

            BackgroundBottom.Width = bgWidth - cornerWidth;
            BackgroundBottom.Height = cornerHeight;
            BackgroundBottom.Offset = new Vector2D((bgWidth + cornerWidth) / 2, (cornerHeight) / 2);

            CornerBotomLeft.Width = cornerWidth;
            CornerBotomLeft.Height = cornerHeight;
            CornerBotomLeft.Offset = new Vector2D((cornerWidth) / 2, (cornerHeight) / 2);

            // DEBUG TODO better maffs!
            float topRightCornerScale = (float)MathHelper.Clamp((1f - (topLinesWidth / TextSize.X)) * 4, 1, 3);

            CornerTopRight.Width = cornerWidth * topRightCornerScale;
            CornerTopRight.Height = cornerHeight * topRightCornerScale;
            CornerTopRight.Offset = new Vector2D(bgWidth - (cornerWidth * topRightCornerScale) / 2, bgHeight - (cornerHeight * topRightCornerScale) / 2);

            BackgroundTop.Width = bgWidth - cornerWidth * topRightCornerScale;
            BackgroundTop.Height = cornerHeight * topRightCornerScale;
            BackgroundTop.Offset = new Vector2D((bgWidth - (cornerWidth * topRightCornerScale)) / 2, bgHeight - (cornerHeight * topRightCornerScale) / 2);

            Background.Width = bgWidth;
            Background.Height = bgHeight - cornerHeight - (cornerHeight * topRightCornerScale);
            Background.Offset = textOffset + (TextSize / 2) + halfEdgeVec + new Vector2D(0, (cornerHeight - (cornerHeight * topRightCornerScale)) / 2);
        }
    }
}
