using Digi.BuildInfo.Features.GUI;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.ModAPI;
using VRage.Input;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class ScreenCoords : ModComponent
    {
        bool ShowCoords = false;
        HudAPIv2.BillBoardHUDMessage ScreenCoordCursor;
        HudAPIv2.BillBoardHUDMessage ScreenCoordRulerH;
        HudAPIv2.BillBoardHUDMessage ScreenCoordRulerV;
        ITooltipHandler ScreenCoordInfo;
        int BoxCorner = 0;

        public ScreenCoords(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        public void Toggle()
        {
            ShowCoords = !ShowCoords;
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, ShowCoords);
            MyAPIGateway.Utilities.ShowMessage("BuildInfo", $"Screen coordinates now {(ShowCoords ? "shown" : "hidden")}");
        }

        public override void UpdateDraw()
        {
            if(ShowCoords && Main.TextAPI.IsEnabled)
            {
                if(ScreenCoordInfo == null)
                {
                    ScreenCoordInfo = new TooltipHandler();
                }

                Vector2 resolution = MyAPIGateway.Session.Camera.ViewportSize;

                if(ScreenCoordCursor == null)
                {
                    ScreenCoordCursor = new HudAPIv2.BillBoardHUDMessage(Constants.MatUI_Corner, Vector2D.Zero, Color.White);
                    ScreenCoordCursor.Options = HudAPIv2.Options.Pixel;
                    ScreenCoordCursor.Visible = false;
                    ScreenCoordCursor.Width = 16;
                    ScreenCoordCursor.Height = 16;
                    ScreenCoordCursor.Rotation = MathHelper.Pi;

                    ScreenCoordRulerH = new HudAPIv2.BillBoardHUDMessage(Constants.MatUI_Square, Vector2D.Zero, Color.White);
                    ScreenCoordRulerH.Options = HudAPIv2.Options.Pixel;
                    ScreenCoordRulerH.Visible = false;
                    ScreenCoordRulerH.Width = resolution.X * 2;
                    ScreenCoordRulerH.Height = 1;
                    ScreenCoordRulerH.Offset = new Vector2D(ScreenCoordRulerH.Width / -2, 0);

                    ScreenCoordRulerV = new HudAPIv2.BillBoardHUDMessage(Constants.MatUI_Square, Vector2D.Zero, Color.White);
                    ScreenCoordRulerV.Options = HudAPIv2.Options.Pixel;
                    ScreenCoordRulerV.Visible = false;
                    ScreenCoordRulerV.Width = 1;
                    ScreenCoordRulerV.Height = resolution.Y * 2;
                    ScreenCoordRulerV.Offset = new Vector2D(0, ScreenCoordRulerV.Height / -2);
                }

                Vector2 mousePx = MyAPIGateway.Input.GetMousePosition() / MyAPIGateway.Input.GetMouseAreaSize();
                mousePx *= resolution;
                ScreenCoordCursor.Origin = mousePx;
                ScreenCoordRulerH.Origin = mousePx;
                ScreenCoordRulerV.Origin = mousePx;

                Vector2D pos = MenuHandler.GetMousePositionGUI();
                ScreenCoordInfo.Hover($"{pos.X:0.######} / {pos.Y:0.######}\n{mousePx.X:0.####}px x {mousePx.Y:0.####}px\n<color=gray>Shift to cycle this box's position");

                if(MyAPIGateway.Input.IsNewKeyPressed(MyKeys.Shift))
                    BoxCorner = (BoxCorner + 1) % 5;

                Vector2D onePx = HudAPIv2.APIinfo.ScreenPositionOnePX;
                Vector2D tooltipOffset = onePx * 16;

                switch(BoxCorner)
                {
                    case 0: ScreenCoordInfo.Draw(pos, true); break;
                    case 1: ScreenCoordInfo.Draw(new Vector2D(-1 - tooltipOffset.X, 1 + tooltipOffset.Y), true); break;
                    case 2: ScreenCoordInfo.Draw(new Vector2D(1, 1 + tooltipOffset.Y), true); break;
                    case 3: ScreenCoordInfo.Draw(new Vector2D(1, -1 - tooltipOffset.Y), true); break;
                    case 4: ScreenCoordInfo.Draw(new Vector2D(-1 - tooltipOffset.X, -1 - tooltipOffset.Y), true); break;
                }

                ScreenCoordRulerH.Draw();
                ScreenCoordRulerV.Draw();
                ScreenCoordCursor.Draw();
            }
        }
    }
}