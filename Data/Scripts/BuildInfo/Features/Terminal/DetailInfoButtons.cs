using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.GUI;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.Terminal
{
    public class DetailInfoButtons : ModComponent
    {
        const float ButtonScale = 1f;
        const float TooltipScaleOffset = 1f;

        IMyTerminalBlock ViewedInTerminal;

        Button[] Buttons;
        TooltipHandler Tooltip;

        Button CopyButton;
        Button RefreshButton;
        Button OverlaysButton;

        Vector2D MousePos;
        Vector2D? DragOffset;
        int DragShowTooltipTicks;

        int CopyClickHighlightTicks;
        bool CopyCopied;

        int RefreshClickHighlightTicks;

        int OverlaysClickHighlightTicks;

        public DetailInfoButtons(BuildInfoMod main) : base(main)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);
        }

        public override void RegisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter += TerminalCustomControlGetter;

            Main.Config.Handler.SettingsLoaded += RefreshPositions;
            Main.Config.TerminalButtonsScale.ValueAssigned += TerminalButtonsScale_ValueAssigned;
            Main.Config.TerminalButtonsPosition.ValueAssigned += TerminalButtonsPosition_ValueAssigned;
            Main.GUIMonitor.OptionsMenuClosed += RefreshPositions;
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= TerminalCustomControlGetter;

            if(!Main.ComponentsRegistered)
                return;

            Main.Config.Handler.SettingsLoaded -= RefreshPositions;
            Main.Config.TerminalButtonsScale.ValueAssigned -= TerminalButtonsScale_ValueAssigned;
            Main.Config.TerminalButtonsPosition.ValueAssigned -= TerminalButtonsPosition_ValueAssigned;
            Main.GUIMonitor.OptionsMenuClosed -= RefreshPositions;
        }

        void TerminalButtonsScale_ValueAssigned(float oldValue, float newValue, ConfigLib.SettingBase<float> setting)
        {
            RefreshPositions();
        }

        void TerminalButtonsPosition_ValueAssigned(Vector2D oldValue, Vector2D newValue, ConfigLib.SettingBase<Vector2D> setting)
        {
            RefreshPositions();
        }

        void TerminalCustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            ViewedInTerminal = block;
        }

        void CreateButtons()
        {
            Tooltip = new TooltipHandler();

            string moveHint = $"\n\n<i>Hold RMB to move this set of buttons. Added by {BuildInfoMod.ModName} mod.";

            // starting from right side and filing buttons towards left

            Buttons = new Button[]
            {
                CopyButton = new Button("Copy",
                    tooltip: "Copies the detailed info text to clipboard." + moveHint, tooltipHandler: Tooltip,
                    hover: CopyHover, hoverEnd: CopyHoverEnd,
                    pivot: Align.TopRight,
                    directDraw: true),

                OverlaysButton = new Button("Overlays: Off",
                    tooltip: "Toggle showing <i>specialized</i> overlays for selected blocks (up to 30)." + moveHint, tooltipHandler: Tooltip,
                    hover: OverlaysHover, hoverEnd: OverlaysHoverEnd,
                    pivot: Align.TopRight,
                    directDraw: true),

                RefreshButton = new Button("Auto-Refresh: On",
                    tooltip: "Toggle if the detailed info area is forced to self-refresh twice a second." +
                             "\nValue not saved to config as it is meant as a temporary thing to mess with." + moveHint, tooltipHandler: Tooltip,
                    hover: RefreshHover, hoverEnd: RefreshHoverEnd,
                    pivot: Align.TopRight,
                    directDraw: true),
            };
        }

        #region Refresh button
        void RefreshHover(Button button)
        {
            CheckForDragInput();

            if(MyAPIGateway.Input.IsNewLeftMousePressed())
            {
                RefreshClickHighlightTicks = 30;

                bool on = (Main.TerminalInfo.AutoRefresh = !Main.TerminalInfo.AutoRefresh);

                button.Label.TextStringBuilder.Clear().Append("Auto-Refresh: ").Append(on ? "On" : "Off");
            }

            if(RefreshClickHighlightTicks > 0 && --RefreshClickHighlightTicks > 0)
            {
                button.Label.Background.Material = Constants.MatUI_ButtonBgActivate;
                button.Label.Background.BillBoardColor = Color.Lime;
            }
        }

        void RefreshHoverEnd(Button button)
        {
            RefreshClickHighlightTicks = 0;
        }
        #endregion

        #region Copy button
        void CopyHover(Button button)
        {
            CheckForDragInput();

            if(ViewedInTerminal == null)
                return;

            if(MyAPIGateway.Input.IsNewLeftMousePressed())
            {
                CopyClickHighlightTicks = 30;

                string text = null;
                if(Main.TerminalInfo.SelectedInTerminal.Count > 1)
                {
                    text = Main.MultiDetailInfo?.InfoText?.ToString();
                }
                else
                {
                    string detailInfo = ViewedInTerminal.DetailedInfo;
                    string customInfo = ViewedInTerminal.CustomInfo;
                    if(customInfo.Length > 0)
                    {
                        StringBuilder sb = new StringBuilder(detailInfo.Length + 2 + customInfo.Length);

                        // same as in MyGuiControlGenericFunctionalBlock.UpdateDetailedInfo()
                        sb.Append(detailInfo).TrimEndWhitespace().AppendLine().Append(customInfo);

                        text = sb.ToString();
                    }
                    else
                    {
                        text = detailInfo;
                    }
                }

                if(!string.IsNullOrEmpty(text))
                {
                    MyClipboardHelper.SetClipboard(text);
                    CopyCopied = true;
                }
                else
                {
                    CopyCopied = false;
                }
            }
            //else if(MyAPIGateway.Input.IsNewLeftMouseReleased())
            //{
            //    CopyClickHighlightTicks = 0;
            //}

            if(CopyClickHighlightTicks > 0 && --CopyClickHighlightTicks > 0)
            {
                Tooltip?.Hover(CopyCopied ? "Copied to clipboard" : "Nothing to copy");

                button.Label.Background.Material = Constants.MatUI_ButtonBgActivate;
                button.Label.Background.BillBoardColor = (CopyCopied ? Color.Lime : Color.Red);
            }
        }

        void CopyHoverEnd(Button button)
        {
            CopyClickHighlightTicks = 0;
        }
        #endregion

        #region Overlays button
        void OverlaysHover(Button button)
        {
            CheckForDragInput();

            if(MyAPIGateway.Input.IsNewLeftMousePressed())
            {
                OverlaysClickHighlightTicks = 30;

                bool on = (Main.TerminalUnderlays.ShowSpecializedOverlays = !Main.TerminalUnderlays.ShowSpecializedOverlays);

                button.DefaultColor = (on ? Color.Yellow : Color.White);
                button.Label.TextStringBuilder.Clear().Append("Overlays: ").Append(on ? "On" : "Off");
            }

            if(OverlaysClickHighlightTicks > 0 && --OverlaysClickHighlightTicks > 0)
            {
                button.Label.Background.Material = Constants.MatUI_ButtonBgActivate;
                button.Label.Background.BillBoardColor = Color.Lime;
            }
        }

        void OverlaysHoverEnd(Button button)
        {
            if(Main.TerminalUnderlays.ShowSpecializedOverlays)
            {
                button.Label.Background.Material = Constants.MatUI_ButtonBgActivate;
            }

            OverlaysClickHighlightTicks = 0;
        }
        #endregion

        void CheckForDragInput()
        {
            if(MyAPIGateway.Input.IsNewRightMousePressed())
            {
                DragOffset = Main.Config.TerminalButtonsPosition.Value - MousePos;
                DragShowTooltipTicks = 60 * 3;
            }
        }

        void RefreshPositions()
        {
            if(Buttons == null)
                return;

            Vector2D pos = Main.Config.TerminalButtonsPosition.Value;
            float scale = Main.Config.TerminalButtonsScale.Value;
            float buttonScale = scale * ButtonScale;

            for(int i = 0; i < Buttons.Length; i++)
            {
                Button button = Buttons[i];
                button.Scale = buttonScale;
                button.Refresh(pos);
                pos -= new Vector2D(button.Label.Background.Width + ((Button.EdgePadding * buttonScale) / 4), 0);
            }

            Tooltip?.Refresh(scale * TooltipScaleOffset);
        }

        public override void UpdateDraw()
        {
            if(DragOffset.HasValue && MyAPIGateway.Input.IsNewRightMouseReleased())
            {
                DragOffset = null;
                Main.Config.Save();
                Main.ConfigMenuHandler.RefreshAll();
            }

            if(ViewedInTerminal != null && ViewedInTerminal.MarkedForClose)
            {
                ViewedInTerminal = null;
            }

            if(ViewedInTerminal == null
            || !Main.TextAPI.IsEnabled
            || !MyAPIGateway.Gui.IsCursorVisible
            || MyAPIGateway.Gui.GetCurrentScreen != MyTerminalPageEnum.ControlPanel
            || Main.GUIMonitor.InAnyToolbarGUI
            || MyAPIGateway.Gui.ActiveGamePlayScreen != "MyGuiScreenTerminal")
                return;

            if(Buttons == null)
            {
                CreateButtons();
                RefreshPositions();
            }

            // using GUI size because this is a real UI where we're tracking mouse
            Vector2 guiSize = MyAPIGateway.Input.GetMouseAreaSize();
            Vector2 mousePos = MyAPIGateway.Input.GetMousePosition() / guiSize;
            MousePos = new Vector2D(mousePos.X * 2 - 1, 1 - 2 * mousePos.Y); // turn from 0~1 to -1~1

            RefreshButton.SetVisible(Main.TerminalInfo.SelectedInTerminal.Count <= 1);

            for(int i = 0; i < Buttons.Length; i++)
            {
                Button button = Buttons[i];
                button.Update(MousePos);
            }

            if(DragOffset.HasValue && MyAPIGateway.Input.IsRightMousePressed())
            {
                // using this instead of MousePos because this works after mouse is at the edge of screen
                Vector2D deltaMouse = new Vector2D(MyAPIGateway.Input.GetMouseX() / guiSize.X, -MyAPIGateway.Input.GetMouseY() / guiSize.Y);

                Vector2D newPos = Main.Config.TerminalButtonsPosition.Value;

                bool isAnyShift = MyAPIGateway.Input.IsAnyShiftKeyPressed();

                if(isAnyShift)
                    newPos += deltaMouse / 4;
                else
                    newPos += deltaMouse;

                //if(MyAPIGateway.Input.IsAnyCtrlKeyPressed())
                //    newPos = new Vector2D(Math.Round(newPos.X * screenSize.X) / screenSize.X, Math.Round(newPos.Y * screenSize.Y) / screenSize.Y);

                newPos = Vector2D.Clamp(newPos, -Vector2D.One, Vector2D.One);

                Main.Config.TerminalButtonsPosition.SetValue(newPos);

                int deltaScroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(deltaScroll != 0)
                {
                    float scale = Main.Config.TerminalButtonsScale.Value;
                    float amount = (isAnyShift ? 0.01f : 0.05f);

                    if(deltaScroll > 0)
                        scale += amount;
                    else if(deltaScroll < 0)
                        scale -= amount;

                    Main.Config.TerminalButtonsScale.SetValue((float)Math.Round(MathHelper.Clamp(scale, Main.Config.TerminalButtonsScale.Min, Main.Config.TerminalButtonsScale.Max), 2));
                }

                RefreshPositions();

                if(DragShowTooltipTicks > 0 && --DragShowTooltipTicks > 0)
                {
                    Tooltip?.Hover("Hold Shift to slow down.\nScroll to rescale (and Shift rescale slower).");
                    Tooltip?.Draw(newPos, drawNow: true);
                }
            }
            else
            {
                Tooltip?.Draw(MousePos, drawNow: true);
            }
        }
    }
}