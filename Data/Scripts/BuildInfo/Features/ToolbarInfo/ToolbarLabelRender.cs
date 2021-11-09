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
        public const int MaxBlockNameLengthIfPbArg = MaxBlockNameLength - MaxArgLength; // last X characters
        public const int MaxActionNameLength = 28; // first X characters
        public const int MaxArgLength = 16; // first X characters

        public const double SplitModeLeftSideMinWidth = 0.32;

        public int ForceRefreshAtTick;
        public int IgnoreTick;

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
        ToolbarStyle StyleMode;
        float Scale;

        public bool MustBeVisible;
        bool? WereVisible;

        bool InToolbarConfig;
        bool? WasInToolbarConfig;

        bool WasShipBarShown;

        int ShowUntilTick;
        bool BeenFaded = false;

        int RenderedAtTick = -1;

        Vector2D? ClickOffset;
        Vector2D TextSize;
        bool SelectedBox;

        HudAPIv2.BillBoardHUDMessage Background;
        HudAPIv2.BillBoardHUDMessage BackgroundBottom;
        HudAPIv2.BillBoardHUDMessage CornerBotomLeft;
        HudAPIv2.BillBoardHUDMessage BackgroundTop;
        HudAPIv2.BillBoardHUDMessage CornerTopRight;
        HudAPIv2.HUDMessage Shadows;
        HudAPIv2.HUDMessage Labels;
        HudAPIv2.HUDMessage ShadowsLine2;
        HudAPIv2.HUDMessage LabelsLine2;
        List<HudAPIv2.BillBoardHUDMessage> Backgrounds;

        public ToolbarLabelRender(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.TextAPI.Detected += TextAPIDetected;

            Main.Config.ToolbarLabelsHeader.ValueAssigned += ConfigBoolChanged;
            Main.Config.ToolbarLabelsInMenuPosition.ValueAssigned += ConfigPositionChanged;
            Main.Config.ToolbarLabelsPosition.ValueAssigned += ConfigPositionChanged;
            Main.Config.ToolbarLabelsOffsetForInvBar.ValueAssigned += ConfigPositionChanged;
            Main.Config.ToolbarLabelsScale.ValueAssigned += ConfigFloatChanged;
            Main.Config.ToolbarStyleMode.ValueAssigned += ConfigIntChanged;
            Main.Config.ToolbarItemNameMode.ValueAssigned += ConfigIntChanged;
            Main.Config.ToolbarLabels.ValueAssigned += ConfigIntChanged;
            Main.GameConfig.OptionsMenuClosed += UpdateFromConfig;

            Main.EquipmentMonitor.ControlledChanged += EquipmentMonitor_ControlledChanged;

            Main.ToolbarMonitor.ToolbarPageChanged += ToolbarPageChanged;

            MyVisualScriptLogicProvider.PlayerEnteredCockpit += EnteredCockpit;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.TextAPI.Detected -= TextAPIDetected;

            Main.Config.ToolbarLabelsHeader.ValueAssigned -= ConfigBoolChanged;
            Main.Config.ToolbarLabelsInMenuPosition.ValueAssigned -= ConfigPositionChanged;
            Main.Config.ToolbarLabelsPosition.ValueAssigned -= ConfigPositionChanged;
            Main.Config.ToolbarLabelsOffsetForInvBar.ValueAssigned -= ConfigPositionChanged;
            Main.Config.ToolbarLabelsScale.ValueAssigned -= ConfigFloatChanged;
            Main.Config.ToolbarStyleMode.ValueAssigned -= ConfigIntChanged;
            Main.Config.ToolbarItemNameMode.ValueAssigned -= ConfigIntChanged;
            Main.Config.ToolbarLabels.ValueAssigned -= ConfigIntChanged;
            Main.GameConfig.OptionsMenuClosed -= UpdateFromConfig;

            Main.EquipmentMonitor.ControlledChanged -= EquipmentMonitor_ControlledChanged;

            Main.ToolbarMonitor.ToolbarPageChanged -= ToolbarPageChanged;

            MyVisualScriptLogicProvider.PlayerEnteredCockpit -= EnteredCockpit;
        }

        void ConfigBoolChanged(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            UpdateFromConfig();
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
            LabelsMode = (ToolbarLabelsMode)Main.Config.ToolbarLabels.Value;
            NamesMode = (ToolbarNameMode)Main.Config.ToolbarItemNameMode.Value;
            StyleMode = (ToolbarStyle)Main.Config.ToolbarStyleMode.Value;
            PosInGUI = Main.Config.ToolbarLabelsInMenuPosition.Value;
            PosOnHUD = Main.Config.ToolbarLabelsPosition.Value;
            Scale = (float)(TextScaleMultiplier * Main.Config.ToolbarLabelsScale.Value);

            WasInToolbarConfig = null; // force origin refresh

            if(Labels != null)
            {
                Shadows.Scale = Scale;
                ShadowsLine2.Scale = Scale;
                Labels.Scale = Scale;
                LabelsLine2.Scale = Scale;
                UpdateBgOpacity(Main.GameConfig.HudBackgroundOpacity);
                UpdateTextOpacity(1f);
            }

            WereVisible = null;
            ForceRefreshAtTick = Main.Tick + 10;
        }

        void UpdatePosition()
        {
            Vector2D bottomLeftPos;
            if(InToolbarConfig)
            {
                bottomLeftPos = PosInGUI;
            }
            else
            {
                if(Main.ShipToolInventoryBar.Shown)
                    bottomLeftPos = PosOnHUD + Main.Config.ToolbarLabelsOffsetForInvBar.Value;
                else
                    bottomLeftPos = PosOnHUD;
            }

            foreach(HudAPIv2.BillBoardHUDMessage bg in Backgrounds)
            {
                bg.Origin = bottomLeftPos;
            }

            Shadows.Origin = bottomLeftPos;
            ShadowsLine2.Origin = bottomLeftPos;
            Labels.Origin = bottomLeftPos;
            LabelsLine2.Origin = bottomLeftPos;
        }

        void UpdateBgOpacity(float opacity, Color? colorOverride = null)
        {
            if(Backgrounds == null)
                return;

            Color color = (colorOverride.HasValue ? colorOverride.Value : BackgroundColor);
            Utils.FadeColorHUD(ref color, opacity);

            foreach(HudAPIv2.BillBoardHUDMessage bg in Backgrounds)
            {
                bg.BillBoardColor = color;
            }
        }

        float TextOpacity = 1f;
        bool TextOpacityNeedsReset = false;

        void UpdateTextOpacity(float opacity)
        {
            Color whiteFade = Color.White * opacity;

            Labels.InitialColor = whiteFade;
            LabelsLine2.InitialColor = whiteFade;

            Color blackFade = Color.Black * opacity;

            Shadows.InitialColor = blackFade;
            ShadowsLine2.InitialColor = blackFade;

            TextOpacity = opacity;

            // TODO: fade text+shadow more efficiently once textAPI has a transparency setting for text
            if(opacity < 1)
            {
                if(Main.Tick % 6 == 0)
                {
                    TextOpacityNeedsReset = true;
                    UpdateRender();
                }
            }
            else
            {
                if(TextOpacityNeedsReset)
                {
                    TextOpacityNeedsReset = false;
                    UpdateRender();
                }
            }
        }

        void TextAPIDetected()
        {
            const int SBCapacity = 512;

            Backgrounds = new List<HudAPIv2.BillBoardHUDMessage>(6);

            // creation order important for draw order
            Background = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("BuildInfo_UI_Square"), PosOnHUD, Color.White);
            Backgrounds.Add(Background);

            BackgroundTop = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("BuildInfo_UI_Square"), PosOnHUD, Color.White);
            Backgrounds.Add(BackgroundTop);

            CornerTopRight = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("BuildInfo_UI_Corner"), PosOnHUD, Color.White);
            CornerTopRight.Rotation = MathHelper.Pi;
            Backgrounds.Add(CornerTopRight);

            BackgroundBottom = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("BuildInfo_UI_Square"), PosOnHUD, Color.White);
            Backgrounds.Add(BackgroundBottom);

            CornerBotomLeft = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("BuildInfo_UI_Corner"), PosOnHUD, Color.White);
            Backgrounds.Add(CornerBotomLeft);

            foreach(HudAPIv2.BillBoardHUDMessage bg in Backgrounds)
            {
                bg.Visible = false;
                bg.Blend = TextBlendType;
                bg.Options = HudAPIv2.Options.HideHud;
                bg.Width = 0f;
                bg.Height = 0f;
            }

            Shadows = new HudAPIv2.HUDMessage(new StringBuilder(SBCapacity), PosOnHUD, HideHud: true, Scale: Scale, Font: TextFont, Blend: TextBlendType);
            Shadows.InitialColor = Color.Black;
            Shadows.Visible = false;

            ShadowsLine2 = new HudAPIv2.HUDMessage(new StringBuilder(SBCapacity), PosOnHUD, HideHud: true, Scale: Scale, Font: TextFont, Blend: TextBlendType);
            ShadowsLine2.InitialColor = Color.Black;
            ShadowsLine2.Visible = false;

            Labels = new HudAPIv2.HUDMessage(new StringBuilder(SBCapacity), PosOnHUD, HideHud: true, Scale: Scale, Font: TextFont, Blend: TextBlendType);
            Labels.Visible = false;

            LabelsLine2 = new HudAPIv2.HUDMessage(new StringBuilder(SBCapacity), PosOnHUD, HideHud: true, Scale: Scale, Font: TextFont, Blend: TextBlendType);
            LabelsLine2.Visible = false;

            WereVisible = null;

            UpdateFromConfig();
        }

        void EnteredCockpit(string entityName, long playerId, string gridName)
        {
            try
            {
                Utils.AssertMainThread();

                if(Main.Config.ToolbarLabelsEnterCockpitTime.Value > 0)
                {
                    IMyPlayer player = MyAPIGateway.Session?.Player;
                    if(player != null && player.IdentityId == playerId)
                    {
                        ShowUntilTick = Main.Tick + (int)(Main.Config.ToolbarLabelsEnterCockpitTime.Value * Constants.TICKS_PER_SECOND);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void ToolbarPageChanged()
        {
            Main.ToolbarLabelRender.ForceRefreshAtTick = Main.Tick + 1;
        }

        //HudAPIv2.HUDMessage DebugMousePos;

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(ClickOffset.HasValue && MyAPIGateway.Input.IsNewLeftMouseReleased())
            {
                ClickOffset = null;
                Main.Config.Save();
                Main.ConfigMenuHandler.RefreshAll();
            }

            if(MustBeVisible && (InToolbarConfig || Main.TextAPI.InModMenu))
            {
                const int Rounding = 6;
                Vector2 screenSize = MyAPIGateway.Input.GetMouseAreaSize();
                Vector2 mousePos = MyAPIGateway.Input.GetMousePosition() / screenSize;
                Vector2D mouseOnScreen = new Vector2D(mousePos.X * 2 - 1, 1 - 2 * mousePos.Y); // turn from 0~1 to -1~1

                Vector2D bottomLeftPos = Labels.Origin;
                float edge = (float)(BackgroundPadding * Scale);
                BoundingBox2D box = new BoundingBox2D(bottomLeftPos, bottomLeftPos + new Vector2D(Math.Abs(TextSize.X), Math.Abs(TextSize.Y)) + edge);
                box.Min = Vector2D.Min(box.Min, box.Max);
                box.Max = Vector2D.Max(box.Min, box.Max);

                //{
                //    MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
                //    float w = (0.032f * DrawUtils.ScaleFOV);
                //    float h = w;

                //    Vector3D worldPos = DrawUtils.TextAPIHUDtoWorld(box.Min);
                //    VRage.Game.MyTransparentGeometry.AddBillboardOriented(MyStringId.GetOrCompute("WhiteDot"), Color.Lime, worldPos, camMatrix.Left, camMatrix.Up, w, h, Vector2.Zero, blendType: BlendType.PostPP);

                //    worldPos = DrawUtils.TextAPIHUDtoWorld(box.Max);
                //    VRage.Game.MyTransparentGeometry.AddBillboardOriented(MyStringId.GetOrCompute("WhiteDot"), Color.Red, worldPos, camMatrix.Left, camMatrix.Up, w, h, Vector2.Zero, blendType: BlendType.PostPP);
                //}

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
                        UpdateBgOpacity(InToolbarConfig ? 1f : Math.Min(1f, Main.GameConfig.HudBackgroundOpacity * 1.2f), BackgroundColorSelected);
                    }

                    if(MyAPIGateway.Input.IsNewLeftMousePressed())
                    {
                        if(InToolbarConfig)
                            ClickOffset = Main.Config.ToolbarLabelsInMenuPosition.Value - mouseOnScreen;
                        else
                            ClickOffset = Main.Config.ToolbarLabelsPosition.Value - mouseOnScreen;
                    }
                }
                else
                {
                    if(SelectedBox)
                    {
                        SelectedBox = false;
                        UpdateBgOpacity(InToolbarConfig ? 1f : Main.GameConfig.HudBackgroundOpacity);
                    }
                }

                if(ClickOffset.HasValue && MyAPIGateway.Input.IsLeftMousePressed())
                {
                    Vector2D newPos = mouseOnScreen + ClickOffset.Value;
                    newPos = new Vector2D(Math.Round(newPos.X, Rounding), Math.Round(newPos.Y, Rounding));
                    newPos = Vector2D.Clamp(newPos, -Vector2D.One, Vector2D.One);

                    if(InToolbarConfig)
                        Main.Config.ToolbarLabelsInMenuPosition.Value = newPos;
                    else
                        Main.Config.ToolbarLabelsPosition.Value = newPos;
                }
            }
        }

        bool ComputeVisible()
        {
            if(!Main.TextAPI.IsEnabled || LabelsMode == ToolbarLabelsMode.Off)
                return false;

            MyShipController shipController = MyAPIGateway.Session.ControlledObject as MyShipController;
            if(shipController == null || shipController.BuildingMode)
                return false;

            if(!ToolbarMonitor.EnableGamepadSupport && MyAPIGateway.Input.IsJoystickLastUsed)
                return false;

            bool newInToolbarConfig = Main.GUIMonitor.InToolbarConfig;
            if(newInToolbarConfig != InToolbarConfig)
            {
                InToolbarConfig = newInToolbarConfig;
                WereVisible = null; // refresh as it could be switching styles
            }

            if(MyAPIGateway.Gui.IsCursorVisible && !InToolbarConfig)
                return false;

            if(InToolbarConfig)
                return true;

            // below is for HUD version only

            bool emptyToolbar = (Main.ToolbarMonitor.HighestIndexUsed < 0);
            bool modeForFadeOut = (LabelsMode == ToolbarLabelsMode.ShowOnPress || LabelsMode == ToolbarLabelsMode.HudHints);

            if(modeForFadeOut && emptyToolbar) // don't fade for empty toolbars
            {
                ShowUntilTick = 0;
            }

            int tick = Main.Tick;

            // if holding show toolbar bind in the right mode while just entering cockpit, skip the fade-out.
            if(modeForFadeOut && ShowUntilTick > tick && Main.Config.ShowToolbarInfoBind.Value.IsPressed(Input.Devices.ControlContext.VEHICLE))
            {
                ShowUntilTick = 0;

                if(BeenFaded)
                {
                    BeenFaded = false;
                    UpdateBgOpacity(Main.GameConfig.HudBackgroundOpacity);
                    UpdateTextOpacity(1f);
                }
            }

            // show and fade out when entering cockpit
            if(modeForFadeOut && ShowUntilTick >= tick && (LabelsMode != ToolbarLabelsMode.HudHints || Main.GameConfig.HudState != HudState.HINTS))
            {
                float showForTicks = (Main.Config.ToolbarLabelsEnterCockpitTime.Value * Constants.TICKS_PER_SECOND);
                float fadeTicks = (Constants.TICKS_PER_SECOND * 1.5f);

                float bgOpacity = Main.GameConfig.HudBackgroundOpacity;
                float textOpacity = 1f;
                if(ShowUntilTick <= tick + fadeTicks)
                {
                    float lerpAmount = (ShowUntilTick - tick) / fadeTicks;
                    bgOpacity = MathHelper.Lerp(0, bgOpacity, lerpAmount);
                    textOpacity = lerpAmount;
                }

                UpdateBgOpacity(bgOpacity);
                UpdateTextOpacity(textOpacity);
                BeenFaded = true;

                return true;
            }

            if(BeenFaded) // reset colors after fade
            {
                BeenFaded = false;
                UpdateBgOpacity(Main.GameConfig.HudBackgroundOpacity);
                UpdateTextOpacity(1f);
            }

            bool showPressed = Main.Config.ShowToolbarInfoBind.Value.IsPressed(Input.Devices.ControlContext.VEHICLE);

            if(emptyToolbar && !showPressed) // don't show for empty toolbars
                return false;

            if(LabelsMode == ToolbarLabelsMode.HudHints && !(showPressed || Main.GameConfig.HudState == HudState.HINTS))
                return false;

            if(LabelsMode == ToolbarLabelsMode.ShowOnPress && !showPressed)
                return false;

            return true;
        }

        public override void UpdateAfterSim(int tick)
        {
            if(Backgrounds == null)
                return;

            MustBeVisible = ComputeVisible();

            if(!WasInToolbarConfig.HasValue || InToolbarConfig != WasInToolbarConfig.Value)
            {
                WasInToolbarConfig = InToolbarConfig;

                UpdatePosition();
                UpdateBgOpacity(InToolbarConfig ? 1f : Main.GameConfig.HudBackgroundOpacity);
                UpdateTextOpacity(1f);

                // refresh instantly to update names
                if(MustBeVisible && NamesMode == ToolbarNameMode.InMenuOnly)
                    UpdateRender();
            }

            if(WasShipBarShown != Main.ShipToolInventoryBar.Shown)
            {
                WasShipBarShown = Main.ShipToolInventoryBar.Shown;
                UpdatePosition();
            }

            if(!WereVisible.HasValue || MustBeVisible != WereVisible)
            {
                foreach(HudAPIv2.BillBoardHUDMessage bg in Backgrounds)
                {
                    bg.Visible = MustBeVisible;
                }

                Shadows.Visible = MustBeVisible;
                Labels.Visible = MustBeVisible;

                bool splitMode = (!InToolbarConfig && StyleMode == ToolbarStyle.TwoColumns);
                ShadowsLine2.Visible = splitMode && MustBeVisible;
                LabelsLine2.Visible = splitMode && MustBeVisible;

                WereVisible = MustBeVisible;

                if(MustBeVisible)
                    UpdateRender();
            }

            if(MustBeVisible)
            {
                // offset on tick to avoid it synchronizing with other things
                if(ForceRefreshAtTick == tick || (tick + 10) % 60 == 0)
                {
                    UpdateRender();
                }
            }
        }

        void EquipmentMonitor_ControlledChanged(VRage.Game.ModAPI.Interfaces.IMyControllableEntity controlled)
        {
            bool update = controlled is IMyShipController;
            SetUpdateMethods(UpdateFlags.UPDATE_INPUT, update);
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, update);

            UpdateRender();
        }

        void UpdateRender()
        {
            if(Labels == null || !MustBeVisible)
                return;

            int tick = Main.Tick;

            // avoid re-triggering same tick
            if(RenderedAtTick == tick)
                return;
            RenderedAtTick = tick;

            // avoid rendering the same tick that toolbar updated, meaning it doesn't have full data yet
            if(IgnoreTick == tick)
            {
                ForceRefreshAtTick = tick + 1;
                return;
            }

            bool gamepadHUD = ToolbarMonitor.EnableGamepadSupport && MyAPIGateway.Input.IsJoystickLastUsed;
            int slotsPerPage = (gamepadHUD ? ToolbarMonitor.SlotsPerPageGamepad : ToolbarMonitor.SlotsPerPage);
            int toolbarPage = (gamepadHUD ? Main.ToolbarMonitor.GamepadToolbarPage : Main.ToolbarMonitor.ToolbarPage);
            int startIndex = (gamepadHUD ? toolbarPage * ToolbarMonitor.SlotsPerPageGamepad : toolbarPage * ToolbarMonitor.SlotsPerPage);
            int maxIndexPage = (startIndex + slotsPerPage - 1);
            int highestUsedIndex = Math.Max(Main.ToolbarMonitor.HighestIndexUsed, 0);
            int maxUsedIndex = Math.Min(highestUsedIndex, maxIndexPage);

            ToolbarItem[] slots = Main.ToolbarMonitor.Slots;

            // TODO: still required?
            // if any slot is not fully updated, skip this render update.
            //for(int i = 0; i < slotsPerPage; i++)
            //{
            //    int index = startIndex + i;
            //    ToolbarItem item = slots[index];

            //    if(item.ActionId != null && item.Name == null)
            //        return;
            //}

            double topLinesWidth = 0;

            float opacity = TextOpacity;

            StringBuilder sb = Labels.Message.Clear();
            StringBuilder sb2 = null;

            bool splitMode = (!InToolbarConfig && StyleMode == ToolbarStyle.TwoColumns);

            if(splitMode)
            {
                sb2 = LabelsLine2.Message.Clear();
            }

            if(Main.Config.ToolbarLabelsHeader.Value)
            {
                sb.ColorA(new Color(255, 240, 220) * opacity).Append("Toolbar Info - Page ").Append(toolbarPage + 1).Append(" <i>").ColorA(Color.Gray * opacity).Append("(BuildInfo Mod)<reset>\n");
            }

            for(int i = 0; i < slotsPerPage; i++)
            {
                if(splitMode && i >= 4)
                    sb = sb2;

                int index = startIndex + i;
                ToolbarItem item = slots[index];
                if(item.Name == null)
                    sb.ColorA(Color.Gray * opacity);

                if(gamepadHUD)
                    sb.Append(Main.Constants.DPAD_CHARS[i]).Append("  ");
                else
                    sb.Append(i + 1).Append(". ");

                if(item.Name == null)
                {
                    sb.Append("—").NewCleanLine();
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
                                sb.ColorA(new Color(155, 220, 255) * opacity).Append('*');

                            int maxNameLength = (item.PBArgument != null ? MaxBlockNameLengthIfPbArg : MaxBlockNameLength);
                            string blockName = item.GroupName ?? item.Name;
                            int blockNameLen = blockName.Length;
                            if(blockNameLen > maxNameLength)
                                sb.Append("...").Append(blockName, blockNameLen - maxNameLength, maxNameLength);
                            else
                                sb.Append(blockName);

                            if(item.GroupName != null)
                                sb.Append('*'); // .ResetFormatting();

                            sb.ColorA(Color.Gray * opacity).Append(" - ").ResetFormatting();
                        }

                        string actionName = item.ActionName;
                        int actionNameLen = actionName.Length;
                        if(actionNameLen > MaxActionNameLength)
                            sb.Append(actionName, 0, MaxActionNameLength).Append("...");
                        else
                            sb.Append(actionName);

                        if(item.PBArgument != null)
                        {
                            sb.Append(": <i>").ColorA(new Color(55, 200, 155) * opacity);

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
                    bool isWeaponSlot = (item.SlotOB.Data is MyObjectBuilder_ToolbarItemWeapon);

                    if(isWeaponSlot)
                        sb.ColorA(new Color(255, 220, 155) * opacity);
                    else
                        sb.ColorA(new Color(200, 210, 215) * opacity);

                    if(item.SlotOB.Data is MyObjectBuilder_ToolbarItemEmote || item.SlotOB.Data is MyObjectBuilder_ToolbarItemAnimation)
                    {
                        sb.Append("Emote - ");
                    }

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
            if(splitMode)
            {
                sb = Labels.Message;
                sb2.Length -= 1;
            }
            sb.Length -= 1;

            TextAPI.CopyWithoutColor(sb, Shadows.Message);

            if(splitMode)
                TextAPI.CopyWithoutColor(sb2, ShadowsLine2.Message);

            float separator = 0f;

            Vector2D labelsTextSize = Labels.GetTextLength();
            Vector2D labelsLine2TextSize = Vector2D.Zero;

            if(splitMode)
            {
                labelsTextSize.X = Math.Max(labelsTextSize.X, SplitModeLeftSideMinWidth);

                separator = (0.015f * Scale);
                labelsLine2TextSize = LabelsLine2.GetTextLength();
                TextSize = new Vector2D(labelsTextSize.X + labelsLine2TextSize.X + separator, Math.Min(labelsTextSize.Y, labelsLine2TextSize.Y)); // min because Y is always negative
            }
            else
            {
                TextSize = labelsTextSize;
            }

            float cornerHeight = (float)(CornerSize * Scale);
            float cornerWidth = (float)(cornerHeight / Main.GameConfig.AspectRatio);

            float edge = (float)(BackgroundPadding * Scale);
            float bgWidth = (float)Math.Abs(TextSize.X) + edge;
            float bgHeight = (float)Math.Abs(TextSize.Y) + edge;
            Vector2D halfEdgeVec = new Vector2D(edge / 2);

            Vector2D shadowOffset = new Vector2D(ShadowOffset, -ShadowOffset);

            Vector2D textOffset = new Vector2D(0, -TextSize.Y); // bottom-left pivot
            Labels.Offset = textOffset + halfEdgeVec;
            Shadows.Offset = textOffset + halfEdgeVec + shadowOffset;

            if(splitMode)
            {
                Vector2D l2offset = new Vector2D(labelsTextSize.X + separator, -TextSize.Y);
                LabelsLine2.Offset = l2offset + halfEdgeVec;
                ShadowsLine2.Offset = l2offset + halfEdgeVec + shadowOffset;
            }

            BackgroundBottom.Width = bgWidth - cornerWidth;
            BackgroundBottom.Height = cornerHeight;
            BackgroundBottom.Offset = new Vector2D((bgWidth + cornerWidth) / 2, (cornerHeight) / 2);

            CornerBotomLeft.Width = cornerWidth;
            CornerBotomLeft.Height = cornerHeight;
            CornerBotomLeft.Offset = new Vector2D((cornerWidth) / 2, (cornerHeight) / 2);

            // TODO: better math needed!
            float topRightCornerScale = 2f; // (float)MathHelper.Clamp((1f - (topLinesWidth / TextSize.X)) * 4, 1, 3);

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
