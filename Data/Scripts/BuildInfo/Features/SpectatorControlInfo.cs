using System;
using System.Text;
using Digi.BuildInfo.Features.GUI.Elements;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Engine.Utils;
using Sandbox.ModAPI;
using VRage;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class SpectatorControlInfo : ModComponent
    {
        static readonly Color BackgroundColor = new Color(41, 54, 62) * 0.96f;

        public bool Visible { get; private set; } = false;
        CornerTextBox TextBox;
        int SkipDraw;

        Vector3D? PrevSpecPos = null;

        public SpectatorControlInfo(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_DRAW;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            TextBox?.Dispose();
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
                && Main.Config.SpectatorControlInfo.Value
                && Main.GameConfig.IsHudVisible // includes cursor check to avoid showing up in admin menu or respawn screen
                && MyAPIGateway.Session.CameraController is MySpectatorCameraController
                && MySpectator.Static.SpectatorCameraMovement == MySpectatorCameraMovementEnum.UserControlled; // only user spectator, ignoring other modes and mods using spec cam.

            if(Visible != visible)
            {
                Visible = visible;
                TextBox?.SetVisible(visible);
                if(!visible)
                    PrevSpecPos = null;
            }

            if(!visible)
                return;
            #endregion

            if(++SkipDraw % 4 != 0)
                return;

            #region Create HUD element
            if(TextBox == null)
            {
                const float Scale = 0.75f;
                TextBox = new CornerTextBox(BackgroundColor, CornerFlag.All, Scale);

                // to the right, to clear "Game paused" top banner
                TextBox.Position = new Vector2D(0.12, 0.985);
                TextBox.CornerSizes = new CornerBackground.CornerSize(12 * Scale, 48 * Scale, 12 * Scale, 12 * Scale);

                // HACK: measure widest expected
                TextBox.Text.Scale = 1f;
                TextBox.Text.TextStringBuilder.Clear().Append("Speed: 000,000.00m/s (Shift+MMB set on aimed, Shift+RMB clear, Shift+Scroll adjust) ");
                TextBox.MinWidthUnscaled = TextBox.Text.Text.GetTextLength().X;
                TextBox.Text.Scale = Scale;

                TextBox.SetVisible(true);
            }
            #endregion

            // HACK: bindings for spectator are hardcoded in MySpectatorCameraController.MoveAndRotate() & UpdateVelocity()

            #region Update text
            StringBuilder sb = TextBox.TextSB.Clear();

            sb.Append("Speed Modifier: ")
                .Append("x").Append(Math.Round(MySpectator.Static.SpeedModeLinear, 4))
                .Color(Color.Gray).Append(" (Shift+Scroll adjust; Shift temp boost; Ctrl temp slow)")
                //.Append(' ', 8).NewCleanLine(); // to acomodate the cut corner
                .NewCleanLine();

            sb.Append("Rotation Modifier: ");

            if(Math.Abs(MySpectator.Static.SpeedModeAngular - 1f) > 0.001f)
                sb.Color(Color.Yellow);

            sb.Append("x").Append(Math.Round(MySpectator.Static.SpeedModeAngular, 4)).Color(Color.Gray).Append(" (");
            sb.Append("Alt temp boost; ");
            if(allowAngularMod)
                sb.Append("Ctrl+Scroll adjust");
            else
                sb.Append("adjust disabled from mod config");
            sb.Append(")").NewCleanLine();

            sb.Append("Speed: ").SpeedFormat((float)speed).Color(Color.Gray).Append(" (Shift+MMB set on aimed, Shift+RMB clear, Shift+Scroll adjust)").NewCleanLine();

            sb.Color(Color.Gray).Append("Ctrl+Space teleport character here").NewCleanLine();

            sb.Length -= 1; // remove last newline
            #endregion

            TextBox.UpdatePosition();
        }
    }
}