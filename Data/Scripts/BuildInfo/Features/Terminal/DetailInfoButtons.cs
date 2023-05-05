using System;
using System.Collections.Generic;
using System.Text;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using Digi.BuildInfo.Utilities;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using Digi.BuildInfo.Systems;

namespace Digi.BuildInfo.Features.Terminal
{
    public class DetailInfoButtons : ModComponent
    {
        static readonly MyStringId ButtonBg = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBg");
        static readonly MyStringId ButtonBgHover = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBgHover");
        static readonly MyStringId ButtonBgActivate = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBgActivate");
        const BlendTypeEnum BlendType = BlendTypeEnum.PostPP;

        const float TooltipScaleOffset = 1f;
        const float TooltipBgEdge = 0.01f;
        const float TooltipOffset = 0.05f;

        const float ButtonScaleOffset = 1.2f;
        const float ButtonBgEdge = 0.025f;

        IMyTerminalBlock ViewedInTerminal;

        Button[] Buttons;
        TooltipHandler Tooltip;

        Button RefreshButton;
        Button CopyButton;

        Vector2D MousePos;
        Vector2D? DragOffset;
        int DragShowTooltipTicks;

        int CopyClickHighlightTicks;
        bool CopyCopied;

        int RefreshCooldown;

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
            Main.GameConfig.OptionsMenuClosed += RefreshPositions;
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= TerminalCustomControlGetter;

            if(!Main.ComponentsRegistered)
                return;

            Main.Config.Handler.SettingsLoaded -= RefreshPositions;
            Main.Config.TerminalButtonsScale.ValueAssigned -= TerminalButtonsScale_ValueAssigned;
            Main.Config.TerminalButtonsPosition.ValueAssigned -= TerminalButtonsPosition_ValueAssigned;
            Main.GameConfig.OptionsMenuClosed -= RefreshPositions;
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

            Buttons = new Button[2];

            string moveHint = $"\nHold RMB to move. Added by {BuildInfoMod.ModName} mod.";

            Buttons[0] = CopyButton = new Button("Copy", tooltip: "Copies the detailed info text to clipboard." + moveHint, tooltipHandler: Tooltip,
                hover: CopyHover, hoverEnd: CopyHoverEnd);

            Buttons[1] = RefreshButton = new Button("Refresh",
                tooltip: "Force refresh of the detailed info box." + moveHint, tooltipHandler: Tooltip,
                hover: RefreshHover, hoverEnd: RefreshHoverEnd);
        }

        void RefreshHover(Button button)
        {
            CheckForDragInput();

            if(MyAPIGateway.Input.IsLeftMousePressed())
            {
                if(--RefreshCooldown <= 0)
                {
                    RefreshCooldown = TerminalInfo.RefreshMinTicks;
                    button.Label.Background.Material = ButtonBgActivate;

                    // HACK: one way to refresh detail info and consequently terminal controls, causes network spam though.
                    bool orig = ViewedInTerminal.ShowInToolbarConfig;
                    ViewedInTerminal.ShowInToolbarConfig = !orig;
                    ViewedInTerminal.ShowInToolbarConfig = orig;
                }

                button.Label.Background.Material = ButtonBgActivate;
                button.Label.Background.BillBoardColor = Color.Lime;
            }
        }

        void RefreshHoverEnd(Button button)
        {
            RefreshCooldown = 0;
        }

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

                button.Label.Background.Material = ButtonBgActivate;
                button.Label.Background.BillBoardColor = (CopyCopied ? Color.Lime : Color.Red);
            }
        }

        void CopyHoverEnd(Button button)
        {
            CopyClickHighlightTicks = 0;
        }

        void CheckForDragInput()
        {
            if(MyAPIGateway.Input.IsNewRightMousePressed())
            {
                DragOffset = Main.Config.TerminalButtonsPosition.Value - MousePos;
                DragShowTooltipTicks = 60;
            }
        }

        void RefreshPositions()
        {
            if(Buttons == null)
                return;

            Vector2D pos = Main.Config.TerminalButtonsPosition.Value;
            float scale = Main.Config.TerminalButtonsScale.Value;
            float buttonScale = scale * ButtonScaleOffset;

            for(int i = 0; i < Buttons.Length; i++)
            {
                Button button = Buttons[i];
                button.Refresh(pos, buttonScale);
                pos -= new Vector2D(button.Label.Background.Width + ((ButtonBgEdge * buttonScale) / 4), 0);
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

            Vector2 screenSize = MyAPIGateway.Session.Camera.ViewportSize;
            Vector2 mousePos = MyAPIGateway.Input.GetMousePosition() / screenSize;
            MousePos = new Vector2D(mousePos.X * 2 - 1, 1 - 2 * mousePos.Y); // turn from 0~1 to -1~1

            RefreshButton.Visible = (Main.TerminalInfo.SelectedInTerminal.Count <= 1);

            for(int i = 0; i < Buttons.Length; i++)
            {
                Button button = Buttons[i];
                button.Draw(MousePos);
            }

            if(DragOffset.HasValue && MyAPIGateway.Input.IsRightMousePressed())
            {
                // using this instead of MousePos because this works after mouse is at the edge of screen
                Vector2D deltaMouse = new Vector2D(MyAPIGateway.Input.GetMouseX() / screenSize.X, -MyAPIGateway.Input.GetMouseY() / screenSize.Y);

                Vector2D newPos = Main.Config.TerminalButtonsPosition.Value;

                bool isAnyShift = MyAPIGateway.Input.IsAnyShiftKeyPressed();

                if(isAnyShift)
                    newPos += deltaMouse / 4;
                else
                    newPos += deltaMouse;

                //if(MyAPIGateway.Input.IsAnyCtrlKeyPressed())
                //    newPos = new Vector2D(Math.Round(newPos.X * screenSize.X) / screenSize.X, Math.Round(newPos.Y * screenSize.Y) / screenSize.Y);

                newPos = Vector2D.Clamp(newPos, -Vector2D.One, Vector2D.One);

                Main.Config.TerminalButtonsPosition.Value = newPos;

                int deltaScroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(deltaScroll != 0)
                {
                    float scale = Main.Config.TerminalButtonsScale.Value;
                    float amount = (isAnyShift ? 0.01f : 0.05f);

                    if(deltaScroll > 0)
                        scale += amount;
                    else if(deltaScroll < 0)
                        scale -= amount;

                    Main.Config.TerminalButtonsScale.Value = (float)Math.Round(MathHelper.Clamp(scale, Main.Config.TerminalButtonsScale.Min, Main.Config.TerminalButtonsScale.Max), 2);
                }

                RefreshPositions();

                if(DragShowTooltipTicks > 0 && --DragShowTooltipTicks > 0)
                {
                    Tooltip?.Hover("Hold Shift to slow down.\nScroll to rescale (and Shift rescale slower).");
                    Tooltip?.Draw(newPos);
                }
            }
            else
            {
                Tooltip?.Draw(MousePos);
            }
        }

        // TODO: move to their own files
        class Button
        {
            public bool Selected { get; private set; }
            public bool Visible { get; set; } = true;

            public readonly TextAPI.TextPackage Label;

            readonly Action<Button> HoverAction;
            readonly Action<Button> HoverEndAction;

            readonly string Tooltip;
            readonly ITooltipHandler TooltipHandler;

            BoundingBox2D ButtonBB;

            public Button(string label,
                string tooltip = null,
                ITooltipHandler tooltipHandler = null,
                Action<Button> hover = null, Action<Button> hoverEnd = null)
            {
                Label = new TextAPI.TextPackage(new StringBuilder(label), backgroundTexture: ButtonBg);

                Tooltip = tooltip;
                TooltipHandler = tooltipHandler;

                HoverAction = hover;
                HoverEndAction = hoverEnd;
            }

            public void Refresh(Vector2D pos, float scale)
            {
                Label.Background.Origin = pos;
                Label.Text.Scale = scale;
                Label.Text.Origin = pos;

                Vector2D textSize = Label.Text.GetTextLength();

                // bottom-right pivot and some padding
                Label.Text.Offset = new Vector2D(-textSize.X, -textSize.Y) + new Vector2D((ButtonBgEdge / 2) * scale);

                Label.Background.Width = (float)Math.Abs(textSize.X) + (ButtonBgEdge * scale);
                Label.Background.Height = (float)Math.Abs(textSize.Y) + (ButtonBgEdge * scale);
                Label.Background.Offset = Label.Text.Offset + (textSize / 2);

                Vector2D centerPos = Label.Background.Origin + Label.Background.Offset;
                Vector2D halfSize = new Vector2D(Label.Background.Width, Label.Background.Height) / 2;
                ButtonBB = new BoundingBox2D(centerPos - halfSize, centerPos + halfSize);
            }

            public void Draw(Vector2D mouseOnScreen)
            {
                if(!Visible)
                    return;

                if(ButtonBB.Contains(mouseOnScreen) == ContainmentType.Contains)
                {
                    Selected = true;
                    Label.Background.Material = ButtonBgHover;
                    Label.Background.BillBoardColor = Color.White;

                    TooltipHandler?.Hover(Tooltip);
                    HoverAction.Invoke(this);
                }
                else if(Selected)
                {
                    Selected = false;
                    Label.Background.Material = ButtonBg;
                    Label.Background.BillBoardColor = Color.White;

                    TooltipHandler?.HoverEnd();
                    HoverEndAction?.Invoke(this);
                }

                Label.Background.Draw();
                Label.Text.Draw();
            }
        }

        interface ITooltipHandler
        {
            void Draw(Vector2D mouseOnScreen);
            void Hover(string tooltip);
            void HoverEnd();
        }

        class TooltipHandler : ITooltipHandler
        {
            readonly TextAPI.TextPackage Label;

            Vector2D TextSize;
            string PrevTooltipRef;
            bool DrawThisTick;

            public TooltipHandler()
            {
                Label = new TextAPI.TextPackage(256, backgroundTexture: ButtonBgHover);
            }

            public void Refresh(float scale)
            {
                Label.Text.Scale = scale;
                TextSize = Label.Text.GetTextLength();
            }

            public void Hover(string tooltip)
            {
                if((tooltip == null && PrevTooltipRef != null) || !object.ReferenceEquals(tooltip, PrevTooltipRef))
                {
                    PrevTooltipRef = tooltip;
                    Label.Text.Message.Clear().Append(tooltip);
                    TextSize = Label.Text.GetTextLength();
                }

                // tooltip's Draw() needs to happen after all the buttons's Draw().
                DrawThisTick = true;
            }

            public void HoverEnd()
            {
                DrawThisTick = false;
            }

            public void Draw(Vector2D mouseOnScreen)
            {
                if(!DrawThisTick)
                    return;

                DrawThisTick = false;

                float scale = (float)Label.Text.Scale;

                Vector2D absTextSize = new Vector2D(Math.Abs(TextSize.X), Math.Abs(TextSize.Y));

                float tooltipOffset = (TooltipOffset * scale);
                float tooltipEdge = (TooltipBgEdge * scale);

                Vector2D pos = mouseOnScreen;
                Vector2D offset = new Vector2D(tooltipOffset, -tooltipOffset); // top-left pivot

                if((mouseOnScreen.X + absTextSize.X + tooltipOffset) > 1) // box collides with right side of screen
                {
                    //offset.X = -TextSize.X - TooltipOffset; // flip pivot to right, so it's on the left of the mouse
                    pos.X -= (mouseOnScreen.X + absTextSize.X + tooltipOffset) - 1; // prevent tooltip from exiting scren on the right
                }

                if((mouseOnScreen.Y - absTextSize.Y - tooltipOffset) < -1) // box collides with bottom of screen
                {
                    offset.Y = -TextSize.Y + tooltipOffset; // flip pivot to bottom, so it's above the mouse
                }

                Label.Text.Offset = offset;

                Label.Background.Width = (float)absTextSize.X + tooltipEdge;
                Label.Background.Height = (float)absTextSize.Y + tooltipEdge;
                Label.Background.Offset = offset + (TextSize / 2);

                Label.Background.Origin = pos;
                Label.Background.Draw();

                Label.Text.Origin = pos;
                Label.Text.Draw();
            }
        }
    }
}