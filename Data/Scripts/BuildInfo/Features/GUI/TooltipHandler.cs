using System;
using Digi.BuildInfo.Systems;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.GUI
{
    public interface ITooltipHandler
    {
        void Draw(Vector2D mouseOnScreen);
        void Hover(string tooltip);
        void HoverEnd();
    }

    public class TooltipHandler : ITooltipHandler
    {
        static readonly MyStringId TooltipBg = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBgHover");
        const float TooltipBgEdge = 0.01f;
        const float TooltipOffset = 0.05f;

        readonly TextAPI.TextPackage Label;

        Vector2D TextSize;
        string PrevTooltipRef;
        bool DrawThisTick;

        public TooltipHandler()
        {
            Label = new TextAPI.TextPackage(256, backgroundTexture: TooltipBg);
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
