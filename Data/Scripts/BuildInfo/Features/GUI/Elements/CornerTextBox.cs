using System;
using System.Text;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Draygo.API;
using VRageMath;
using static Digi.BuildInfo.Systems.TextAPI;

namespace Digi.BuildInfo.Features.GUI.Elements
{
    public class CornerTextBox
    {
        public bool Visible { get; private set; }

        public Vector2D Position;
        public CornerBackground.CornerSize CornerSizes;

        public readonly Color BackgroundColor;
        public readonly float Scale;
        public readonly string Font;

        public readonly CornerBackground Background;
        public readonly TextAPI.TextPackage Text;
        public readonly StringBuilder TextSB;

        public double? MinWidthUnscaled = null;
        public double? MinHeightUnscaled = null;

        readonly BuildInfoMod Main;

        public CornerTextBox(Color backgroundColor, CornerFlag corners, float scale = 0.75f, string font = FontsHandler.BI_SEOutlined)
        {
            Main = BuildInfoMod.Instance;

            BackgroundColor = backgroundColor;
            Scale = scale;
            Font = font;

            Main.GUIMonitor.OptionsMenuClosed += RecalculateColor;

            Background = new CornerBackground(BackgroundColor, corners);
            CornerSizes = new CornerBackground.CornerSize(12 * scale);
            RecalculateColor();

            TextSB = new StringBuilder(512);

            Text = new TextPackage(TextSB);
            Text.HideWithHUD = false;
            Text.Font = Font;
            Text.Scale = Scale;
        }

        public void Dispose()
        {
            Main.GUIMonitor.OptionsMenuClosed -= RecalculateColor;
        }

        public void RecalculateColor()
        {
            if(Background != null)
            {
                Color bgColor = BackgroundColor;
                Utils.FadeColorHUD(ref bgColor, Main.GameConfig.HudBackgroundOpacity);

                Background.SetColor(bgColor);
            }
        }

        public void SetVisible(bool visible)
        {
            Text.Visible = visible;
            Background.SetVisible(visible);
        }

        /// <summary>
        /// Manual draw for one frame regardless of visible state
        /// </summary>
        public void Draw()
        {
            Background.Draw();
            Text.Draw();
        }

        public void UpdatePosition()
        {
            Vector2D textSize = Text.Text.GetTextLength();

            if(MinWidthUnscaled != null)
                textSize.X = Math.Max(MinWidthUnscaled.Value * Scale, textSize.X);

            if(MinHeightUnscaled != null)
                textSize.Y = Math.Max(MinHeightUnscaled.Value * Scale, textSize.Y);

            Vector2D px = HudAPIv2.APIinfo.ScreenPositionOnePX;

            Vector2D padding = (new Vector2D(12, -12) * Scale * px);
            textSize += padding * 2;

            Text.Position = Position + padding;

            Vector2 posPx = (Vector2)Main.DrawUtils.TextAPIHUDToPixels(Position);

            Vector2D boxSize = new Vector2D(Math.Abs(textSize.X), Math.Abs(textSize.Y));
            Vector2 boxSizePx = (Vector2)(boxSize / px);

            Background.SetProperties(posPx, boxSizePx, CornerSizes);
        }
    }
}
