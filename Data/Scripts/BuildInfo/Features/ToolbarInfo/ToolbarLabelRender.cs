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
using VRage.Input;
using VRage.Utils;
using VRageMath;

// TODO: alternate rendering for gamepad HUD mode
// TODO: convert this one to be the HUD-only one and use ToolbarInfoInMenu one for all in-menu ones (currently ship controller's is this)

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

        readonly Color BackgroundColor = new Color(41, 54, 62);
        readonly Color BackgroundColorSelected = new Color(40, 80, 65);
        const float OpacityInMenu = 0.75f;
        const float MinOpacityWhenHovered = 0.8f;

        const string TextFont = FontsHandler.BI_SEOutlined;
        const bool UseShadowMessage = false;
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

        BoxDragging BoxDrag;

        HudAPIv2.BillBoardHUDMessage Background;
        HudAPIv2.BillBoardHUDMessage BackgroundBottom;
        HudAPIv2.BillBoardHUDMessage CornerBottomLeft;
        HudAPIv2.BillBoardHUDMessage BackgroundTop;
        HudAPIv2.BillBoardHUDMessage CornerTopRight;
        List<HudAPIv2.BillBoardHUDMessage> Backgrounds;

        TextAPI.TextPackage List;
        TextAPI.TextPackage ListColumn2;

        public ToolbarLabelRender(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.TextAPI.Detected += TextAPIDetected;

            Main.Config.ToolbarLabelsHeader.ValueAssigned += ConfigBoolChanged;
            Main.Config.ToolbarLabelsMenuPosition.ValueAssigned += ConfigPositionChanged;
            Main.Config.ToolbarLabelsPosition.ValueAssigned += ConfigPositionChanged;
            Main.Config.ToolbarLabelsOffsetForInvBar.ValueAssigned += ConfigPositionChanged;
            Main.Config.ToolbarLabelsScale.ValueAssigned += ConfigFloatChanged;
            Main.Config.ToolbarStyleMode.ValueAssigned += ConfigIntChanged;
            Main.Config.ToolbarItemNameMode.ValueAssigned += ConfigIntChanged;
            Main.Config.ToolbarLabels.ValueAssigned += ConfigIntChanged;
            Main.GUIMonitor.OptionsMenuClosed += UpdateFromConfig;

            Main.EquipmentMonitor.ControlledChanged += EquipmentMonitor_ControlledChanged;

            Main.ToolbarMonitor.ToolbarPageChanged += ToolbarPageChanged;

            MyVisualScriptLogicProvider.PlayerEnteredCockpit += EnteredCockpit;

            BoxDrag = new BoxDragging(MyMouseButtonsEnum.Left);
            BoxDrag.BoxSelected += () => UpdateBgOpacity(InToolbarConfig ? Math.Min(OpacityInMenu, MinOpacityWhenHovered) : Math.Min(Main.GameConfig.HudBackgroundOpacity, MinOpacityWhenHovered), BackgroundColorSelected);
            BoxDrag.BoxDeselected += () => UpdateBgOpacity(InToolbarConfig ? OpacityInMenu : Main.GameConfig.HudBackgroundOpacity);
            BoxDrag.Dragging += (newPos) =>
            {
                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(scroll != 0)
                {
                    ConfigLib.FloatSetting setting = Main.Config.ToolbarLabelsScale;
                    float scale = setting.Value + (scroll > 0 ? 0.05f : -0.05f);
                    scale = MathHelper.Clamp(scale, setting.Min, setting.Max);
                    setting.SetValue((float)Math.Round(scale, 3));
                }

                if(InToolbarConfig)
                    Main.Config.ToolbarLabelsMenuPosition.SetValue(newPos);
                else
                    Main.Config.ToolbarLabelsPosition.SetValue(newPos);
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

            Main.TextAPI.Detected -= TextAPIDetected;

            Main.Config.ToolbarLabelsHeader.ValueAssigned -= ConfigBoolChanged;
            Main.Config.ToolbarLabelsMenuPosition.ValueAssigned -= ConfigPositionChanged;
            Main.Config.ToolbarLabelsPosition.ValueAssigned -= ConfigPositionChanged;
            Main.Config.ToolbarLabelsOffsetForInvBar.ValueAssigned -= ConfigPositionChanged;
            Main.Config.ToolbarLabelsScale.ValueAssigned -= ConfigFloatChanged;
            Main.Config.ToolbarStyleMode.ValueAssigned -= ConfigIntChanged;
            Main.Config.ToolbarItemNameMode.ValueAssigned -= ConfigIntChanged;
            Main.Config.ToolbarLabels.ValueAssigned -= ConfigIntChanged;
            Main.GUIMonitor.OptionsMenuClosed -= UpdateFromConfig;

            Main.EquipmentMonitor.ControlledChanged -= EquipmentMonitor_ControlledChanged;

            Main.ToolbarMonitor.ToolbarPageChanged -= ToolbarPageChanged;

            MyVisualScriptLogicProvider.PlayerEnteredCockpit -= EnteredCockpit;

            BoxDrag = null;
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
            PosInGUI = Main.Config.ToolbarLabelsMenuPosition.Value;
            PosOnHUD = Main.Config.ToolbarLabelsPosition.Value;
            Scale = (float)(TextScaleMultiplier * Main.Config.ToolbarLabelsScale.Value);

            WasInToolbarConfig = null; // force origin refresh

            if(List != null)
            {
                List.Scale = Scale;
                ListColumn2.Scale = Scale;

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

            List.Position = bottomLeftPos;
            ListColumn2.Position = bottomLeftPos;
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

            List.Text.InitialColor = whiteFade;
            ListColumn2.Text.InitialColor = whiteFade;

            if(List.Shadow != null)
            {
                Color blackFade = Color.Black * opacity;

                List.Shadow.InitialColor = blackFade;
                ListColumn2.Shadow.InitialColor = blackFade;
            }

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

            MyStringId squareMaterial = MyStringId.GetOrCompute("BuildInfo_UI_Square");
            MyStringId cornerMaterial = MyStringId.GetOrCompute("BuildInfo_UI_Corner");

            // creation order important for draw order
            Background = TextAPI.CreateHUDTexture(squareMaterial, Color.White, PosOnHUD);
            Backgrounds.Add(Background);

            BackgroundTop = TextAPI.CreateHUDTexture(squareMaterial, Color.White, PosOnHUD);
            Backgrounds.Add(BackgroundTop);

            CornerTopRight = TextAPI.CreateHUDTexture(cornerMaterial, Color.White, PosOnHUD);
            CornerTopRight.Rotation = MathHelper.ToRadians(90);
            Backgrounds.Add(CornerTopRight);

            BackgroundBottom = TextAPI.CreateHUDTexture(squareMaterial, Color.White, PosOnHUD);
            Backgrounds.Add(BackgroundBottom);

            CornerBottomLeft = TextAPI.CreateHUDTexture(cornerMaterial, Color.White, PosOnHUD);
            CornerBottomLeft.Rotation = MathHelper.ToRadians(-90);
            Backgrounds.Add(CornerBottomLeft);

            foreach(HudAPIv2.BillBoardHUDMessage bg in Backgrounds)
            {
                //bg.Visible = false;
                //bg.Blend = BlendType.PostPP;
                //bg.Options = HudAPIv2.Options.HideHud;
                bg.Width = 0f;
                bg.Height = 0f;
            }

            List = new TextAPI.TextPackage(SBCapacity, useShadow: UseShadowMessage);
            ListColumn2 = new TextAPI.TextPackage(SBCapacity, useShadow: UseShadowMessage);

            List.Font = TextFont;
            ListColumn2.Font = TextFont;

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
                        ShowUntilTick = Main.Tick + (int)(Main.Config.ToolbarLabelsEnterCockpitTime.Value * Constants.TicksPerSecond);
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

        bool ComputeVisible()
        {
            if(Main.GameConfig.HudState == HudState.OFF)
                return false;

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
                float showForTicks = (Main.Config.ToolbarLabelsEnterCockpitTime.Value * Constants.TicksPerSecond);
                float fadeTicks = (Constants.TicksPerSecond * 1.5f);

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
                UpdateBgOpacity(InToolbarConfig ? OpacityInMenu : Main.GameConfig.HudBackgroundOpacity);
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

                List.Visible = MustBeVisible;

                // forced single list in toolbar config GUI
                ListColumn2.Visible = MustBeVisible && !InToolbarConfig && StyleMode == ToolbarStyle.TwoColumns;

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

            #region Draggable box update
            if(MustBeVisible && (InToolbarConfig || Main.TextAPI.InModMenu))
            {
                BoxDrag.Position = List.Text.Origin;
                BoxDrag.Update();
            }
            else
            {
                if(BoxDrag.Hovered)
                    BoxDrag.Unhover();
            }
            #endregion
        }

        void EquipmentMonitor_ControlledChanged(VRage.Game.ModAPI.Interfaces.IMyControllableEntity controlled)
        {
            bool update = controlled is IMyShipController;
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, update);

            UpdateRender();
        }

        void UpdateRender()
        {
            if(List == null || !MustBeVisible)
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

            double firstLineWidth = 0;

            float opacity = TextOpacity;

            StringBuilder sb = List.Text.Message.Clear();
            StringBuilder sb2 = null;
            bool splitMode = (!InToolbarConfig && StyleMode == ToolbarStyle.TwoColumns);
            if(splitMode)
                sb2 = ListColumn2.Text.Message.Clear();

            if(InToolbarConfig || Main.Config.ToolbarLabelsHeader.Value)
            {
                sb.Color(new Color(255, 240, 220) * opacity).Append("Toolbar Info - Page ").Append(toolbarPage + 1).Append(" <i>").Color(Color.Gray * opacity).Append("(").Append(BuildInfoMod.ModName).Append(" Mod)").NewCleanLine();
            }

            for(int i = 0; i < slotsPerPage; i++)
            {
                if(splitMode && i >= 4)
                    sb = sb2;

                int index = startIndex + i;
                ToolbarItem item = slots[index];

                // process display name after all slots have been computed
                if(item.DisplayName == null && item.OriginalName != null && item.Block != null)
                {
                    item.DisplayName = Main.ToolbarMonitor.ComputeShortName(item.OriginalName, item.LabelData?.ErasePrefix, item.Block?.CubeGrid);
                }

                if(item.DisplayName == null)
                    sb.Color(Color.Gray * opacity);

                if(gamepadHUD)
                    sb.Append(Main.Constants.DPadIcons[i]).Append("  ");
                else
                    sb.Append(i + 1).Append(". ");

                if(item.DisplayName == null)
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
                        || (NamesMode == ToolbarNameMode.GroupsOnly && item.GroupId != null)
                        || (NamesMode == ToolbarNameMode.InMenuOnly && InToolbarConfig))
                        {
                            if(item.GroupId != null)
                                sb.Color(new Color(155, 220, 255) * opacity).Append('*');

                            int maxNameLength = (item.PBArgument != null ? MaxBlockNameLengthIfPbArg : MaxBlockNameLength);
                            sb.AppendMaxLength(item.DisplayName, maxNameLength).ResetFormatting();

                            if(item.GroupId != null)
                                sb.Color(new Color(155, 220, 255) * opacity).Append('*');

                            sb.Color(Color.Gray * opacity).Append(" - ").ResetFormatting();
                        }

                        sb.AppendMaxLength(item.ActionName, MaxActionNameLength);

                        if(item.PBArgument != null)
                        {
                            sb.Append(": <i>").Color(new Color(55, 200, 155) * opacity).AppendMaxLength(item.PBArgument, MaxArgLength).ResetFormatting();
                        }
                    }
                }
                else if(item.DisplayName != null)
                {
                    bool isWeaponSlot = (item.SlotOB.Data is MyObjectBuilder_ToolbarItemWeapon);

                    if(isWeaponSlot)
                        sb.Color(new Color(255, 220, 155) * opacity);
                    else
                        sb.Color(new Color(200, 210, 215) * opacity);

                    if(item.SlotOB.Data is MyObjectBuilder_ToolbarItemEmote || item.SlotOB.Data is MyObjectBuilder_ToolbarItemAnimation)
                        sb.Append("Emote - ");

                    sb.AppendMaxLength(item.DisplayName, MaxBlockNameLength).ResetFormatting();
                }

                if(i == 1)
                {
                    firstLineWidth = List.Text.GetTextLength().X;
                }

                sb.NewLine();
            }

            // remove last new line
            if(splitMode)
            {
                sb = List.Text.Message;
                sb2.Length -= 1;
            }
            sb.Length -= 1;

            if(UseShadowMessage)
            {
                TextAPI.CopyWithoutColor(sb, List.Shadow.Message);

                if(splitMode)
                    TextAPI.CopyWithoutColor(sb2, ListColumn2.Shadow.Message);
            }

            float separator = 0f;

            Vector2D labelsTextSize = List.Text.GetTextLength();
            Vector2D labelsLine2TextSize = Vector2D.Zero;
            Vector2D textSize;

            if(splitMode)
            {
                labelsTextSize.X = Math.Max(labelsTextSize.X, SplitModeLeftSideMinWidth);

                separator = (0.015f * Scale);
                labelsLine2TextSize = ListColumn2.Text.GetTextLength();
                textSize = new Vector2D(labelsTextSize.X + labelsLine2TextSize.X + separator, Math.Min(labelsTextSize.Y, labelsLine2TextSize.Y)); // min because Y is always negative
            }
            else
            {
                textSize = labelsTextSize;
            }

            float cornerHeight = (float)(CornerSize * Scale);
            float cornerWidth = (float)(cornerHeight / Main.GameConfig.AspectRatio);

            float edge = (float)(BackgroundPadding * Scale);
            float bgWidth = (float)Math.Abs(textSize.X) + edge;
            float bgHeight = (float)Math.Abs(textSize.Y) + edge;
            Vector2D halfEdgeVec = new Vector2D(edge / 2);

            Vector2D shadowOffset = new Vector2D(ShadowOffset, -ShadowOffset);

            Vector2D textOffset = new Vector2D(0, -textSize.Y); // bottom-left pivot
            List.Text.Offset = textOffset + halfEdgeVec;

            if(UseShadowMessage)
                List.Shadow.Offset = textOffset + halfEdgeVec + shadowOffset;

            if(splitMode)
            {
                Vector2D l2offset = new Vector2D(labelsTextSize.X + separator, -textSize.Y);
                ListColumn2.Text.Offset = l2offset + halfEdgeVec;

                if(UseShadowMessage)
                    ListColumn2.Shadow.Offset = l2offset + halfEdgeVec + shadowOffset;
            }

            BackgroundBottom.Width = bgWidth - cornerWidth;
            BackgroundBottom.Height = cornerHeight;
            BackgroundBottom.Offset = new Vector2D((bgWidth + cornerWidth) / 2, (cornerHeight) / 2);

            CornerBottomLeft.Width = cornerWidth;
            CornerBottomLeft.Height = cornerHeight;
            CornerBottomLeft.Offset = new Vector2D((cornerWidth) / 2, (cornerHeight) / 2);

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
            Background.Offset = textOffset + (textSize / 2) + halfEdgeVec + new Vector2D(0, (cornerHeight - (cornerHeight * topRightCornerScale)) / 2);

            // update draggable box
            Vector2D center = List.Text.Origin;
            BoundingBox2D box = new BoundingBox2D(center, center + new Vector2D(bgWidth, bgHeight));
            BoxDrag.DragHitbox = box;
        }
    }
}
