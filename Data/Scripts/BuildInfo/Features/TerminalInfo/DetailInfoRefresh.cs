using System;
using System.Collections.Generic;
using System.Text;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features.Terminal
{
    public class DetailInfoRefresh : ModComponent
    {
        readonly MyStringId ButtonBg = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBg");
        readonly MyStringId ButtonBgHover = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBgHover");
        readonly MyStringId ButtonBgActivate = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBgActivate");
        const float ButtonScaleOffset = 1.2f;
        const float ButtonBgEdge = 0.02f;
        readonly BlendTypeEnum BlendType = BlendTypeEnum.PostPP;

        IMyTerminalBlock ViewedInTerminal;

        HudAPIv2.BillBoardHUDMessage Background;
        HudAPIv2.HUDMessage Text;
        bool SelectedBox = false;
        int Cooldown;

        Vector2D? DragOffset;

        public DetailInfoRefresh(BuildInfoMod main) : base(main)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);
        }

        public override void RegisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter += TerminalCustomControlGetter;

            Main.Config.Handler.SettingsLoaded += RefreshPositions;
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= TerminalCustomControlGetter;

            if(!Main.ComponentsRegistered)
                return;

            Main.Config.Handler.SettingsLoaded -= RefreshPositions;
        }

        void TerminalCustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            ViewedInTerminal = block;
        }

        void RefreshPositions()
        {
            Vector2D pos = Main.Config.TerminalRefreshInfoPosition.Value;
            float scale = Main.Config.TerminalRefreshInfoScale.Value * ButtonScaleOffset;

            Background.Scale = scale;
            Background.Origin = pos;
            Text.Scale = scale;
            Text.Origin = pos;

            var textSize = Text.GetTextLength();

            // bottom-right pivot and some padding
            Text.Offset = new Vector2D(-textSize.X, -textSize.Y) + new Vector2D((ButtonBgEdge / 2) * scale);

            Background.Width = (float)Math.Abs(textSize.X) + (ButtonBgEdge * scale);
            Background.Height = (float)Math.Abs(textSize.Y) + (ButtonBgEdge * scale);
            Background.Offset = Text.Offset + (textSize / 2);
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

            if(Background == null)
            {
                Background = new HudAPIv2.BillBoardHUDMessage(ButtonBg, Vector2D.Zero, Color.White, HideHud: false, Shadowing: false, Blend: BlendType);
                Text = new HudAPIv2.HUDMessage(new StringBuilder("Refresh Detailed Info"), Vector2D.Zero, HideHud: false, Shadowing: true, Blend: BlendType);

                // for manual draw only
                Background.Visible = false;
                Text.Visible = false;

                RefreshPositions();
            }

            Vector2 screenSize = MyAPIGateway.Input.GetMouseAreaSize();
            Vector2 mousePos = MyAPIGateway.Input.GetMousePosition() / screenSize;
            Vector2D mouseOnScreen = new Vector2D(mousePos.X * 2 - 1, 1 - 2 * mousePos.Y); // turn from 0~1 to -1~1

            Vector2D centerPos = Background.Origin + Background.Offset;
            Vector2D halfSize = new Vector2D(Background.Width * Background.Scale, Background.Height * Background.Scale) / 2;
            BoundingBox2D bb = new BoundingBox2D(centerPos - halfSize, centerPos + halfSize);

            if(bb.Contains(mouseOnScreen) == ContainmentType.Contains)
            {
                if(!SelectedBox)
                {
                    SelectedBox = true;
                    Background.Material = ButtonBgHover;
                }

                if(MyAPIGateway.Input.IsLeftMousePressed())
                {
                    if(--Cooldown <= 0)
                    {
                        Cooldown = TerminalInfo.RefreshMinTicks;
                        Background.Material = ButtonBgActivate;

                        // HACK: one way to refresh detail info and consequently terminal controls, causes network spam though.
                        var orig = ViewedInTerminal.ShowInToolbarConfig;
                        ViewedInTerminal.ShowInToolbarConfig = !orig;
                        ViewedInTerminal.ShowInToolbarConfig = orig;
                    }
                }

                if(MyAPIGateway.Input.IsNewRightMousePressed())
                {
                    DragOffset = Text.Origin - mouseOnScreen;
                }
            }
            else
            {
                if(SelectedBox)
                {
                    SelectedBox = false;
                    Background.Material = ButtonBg;
                }
            }

            if(SelectedBox && MyAPIGateway.Input.IsNewLeftMouseReleased())
            {
                Cooldown = 0;
                SelectedBox = false;
                Background.Material = ButtonBg;
            }

            if(DragOffset.HasValue && MyAPIGateway.Input.IsRightMousePressed())
            {
                const int Rounding = 4;

                var newPos = mouseOnScreen + DragOffset.Value;
                newPos = new Vector2D(Math.Round(newPos.X, Rounding), Math.Round(newPos.Y, Rounding));
                newPos = Vector2D.Clamp(newPos, -Vector2D.One, Vector2D.One);

                Main.Config.TerminalRefreshInfoPosition.Value = newPos;
                RefreshPositions();
            }

            Background.Draw();
            Text.Draw();
        }
    }
}