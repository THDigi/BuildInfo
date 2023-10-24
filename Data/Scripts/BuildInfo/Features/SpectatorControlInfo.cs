using System;
using System.Text;
using Digi.BuildInfo.Features.GUI;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Engine.Utils;
using Sandbox.ModAPI;
using VRage;
using VRageMath;
using static Digi.BuildInfo.Systems.TextAPI;

namespace Digi.BuildInfo.Features
{
    public class SpectatorControlInfo : ModComponent
    {
        static readonly Color BackgroundColor = new Color(41, 54, 62) * 0.96f;
        const float GUIScale = 0.75f;

        bool Visible = false;
        CornerBackground Box;
        TextPackage Text;

        Vector3D? PrevSpecPos = null;

        public SpectatorControlInfo(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_DRAW;
        }

        public override void RegisterComponent()
        {
            Main.GUIMonitor.OptionsMenuClosed += RecalculateColor;
        }

        public override void UnregisterComponent()
        {
            Main.GUIMonitor.OptionsMenuClosed -= RecalculateColor;
        }

        void RecalculateColor()
        {
            if(Box != null)
            {
                Color bgColor = BackgroundColor;
                Utils.FadeColorHUD(ref bgColor, Main.GameConfig.HudBackgroundOpacity);

                Box.SetColor(bgColor);

                //Text.Background.BillBoardColor = bgColor;
            }
        }

        public override void UpdateDraw()
        {
            #region Compute speed
            double speed = 0;

            if(PrevSpecPos != null)
                speed = (PrevSpecPos.Value - MySpectator.Static.Position).Length() * Constants.TicksPerSecond;

            PrevSpecPos = MySpectator.Static.Position;
            #endregion

            #region Prevent/allow rotation sensitivity to be changed
            bool allowAngularMod = Main.Config.SpectatorAllowRotationModifier.Value;
            if(!allowAngularMod)
            {
                MySpectator.Static.SpeedModeAngular = 1f;
            }
            #endregion

            #region Check if the HUD element should be shown
            bool visible = Main.TextAPI.IsEnabled
                && Main.GameConfig.IsHudVisible // includes cursor check to avoid showing up in admin menu or respawn screen
                && MyAPIGateway.Session.CameraController is MySpectatorCameraController;

            if(Visible != visible)
            {
                Visible = visible;

                if(Text != null)
                {
                    Visible = visible;
                    Text.Visible = visible;
                    Box.SetVisible(visible);
                }

                if(!visible)
                    PrevSpecPos = null;
            }

            if(!visible)
                return;
            #endregion

            if(Main.Tick % 4 != 0)
                return;

            #region Create HUD element
            if(Text == null)
            {
                Box = new CornerBackground("BuildInfo_UI_Square", "BuildInfo_UI_Corner", BackgroundColor, CornerFlag.All);
                RecalculateColor();

                //Text = new TextPackage(new StringBuilder(512), useShadow: false, backgroundTexture: MyStringId.GetOrCompute("Square"));
                Text = new TextPackage(new StringBuilder(512));
                Text.HideWithHUD = false;
                Text.Scale = GUIScale;

                Text.Visible = true;
                Box.SetVisible(true);
            }
            #endregion

            // HACK: bindings for spectator are hardcoded in MySpectatorCameraController.MoveAndRotate() & UpdateVelocity()

            #region Update text
            StringBuilder sb = Text.TextStringBuilder.Clear();

            sb.Append("Speed Modifier: ")
                .Append("x").Append(Math.Round(MySpectator.Static.SpeedModeLinear, 2))
                .Color(Color.Gray).Append(" (Shift+Scroll adjust; Shift temp boost; Ctrl temp slow)").NewCleanLine();

            sb.Append("Rotation Modifier: ");

            if(Math.Abs(MySpectator.Static.SpeedModeAngular - 1f) > 0.001f)
                sb.Color(Color.Yellow);

            sb.Append("x").Append(Math.Round(MySpectator.Static.SpeedModeAngular, 2)).Color(Color.Gray).Append(" (");
            if(allowAngularMod)
                sb.Append("Ctrl+Scroll adjust; ");
            sb.Append("Alt temp boost)");
            sb.NewCleanLine();

            sb.Append("Speed: ").SpeedFormat((float)speed).Color(Color.Gray).Append(" (Shift+MMB set on aimed, Shift+RMB clear, Shift+Scroll adjust)").NewCleanLine();

            sb.Color(Color.Gray).Append("Ctrl+Space teleport character here").NewCleanLine();

            sb.Length -= 1; // remove last newline
            #endregion

            #region Update size and position
            Vector2D textSize = Text.Text.GetTextLength();

            //Text.Position = new Vector2D(textSize.X / -2, 0.99);
            //Text.UpdateBackgroundSize(provideTextLength: textSize);

            Vector2D px = HudAPIv2.APIinfo.ScreenPositionOnePX;

            Vector2D padding = (new Vector2D(12, -12) * GUIScale * px);
            textSize += padding * 2;

            //Vector2D pos = new Vector2D(textSize.X / -2, 1.0);
            Vector2D pos = new Vector2D(0.12, 0.985); // to the right, to clear "Game paused" top banner

            Text.Position = pos + padding;

            Vector2 posPx = (Vector2)Main.DrawUtils.TextAPIHUDToPixels(pos);

            Vector2D boxSize = new Vector2D(Math.Abs(textSize.X), Math.Abs(textSize.Y));
            Vector2 boxSizePx = (Vector2)(boxSize / px);

            Box.SetProperties(posPx, boxSizePx, new CornerBackground.CornerSize(12 * GUIScale, 48 * GUIScale, 12 * GUIScale, 12 * GUIScale));
            #endregion
        }
    }
}