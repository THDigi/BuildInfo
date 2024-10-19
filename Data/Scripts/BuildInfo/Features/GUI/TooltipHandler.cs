using System;
using System.Text;
using Digi.BuildInfo.Systems;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.GUI
{
    public interface ITooltipHandler
    {
        void Draw(Vector2D mouseOnScreen, bool drawNow);
        void Hover(string tooltip);
        void SetVisible(bool visible);
        void HoverEnd();
    }

    public class TooltipHandler : ITooltipHandler
    {
        const float TooltipBgEdge = 0.01f;
        const float TooltipOffset = 0.05f;

        readonly TextAPI.TextPackage Label;

        Vector2D TextSize;
        string PrevTooltipRef;
        bool DrawThisTick;

        public Vector2 ScreenLimitMax = new Vector2(1, 1);

        /// <summary>
        /// Requires TextAPI to've been initialized!
        /// </summary>
        public TooltipHandler()
        {
            //MyStringId bg = Constants.MatUI_ButtonBgHover;
            //Color bgColor = Color.White;

            MyStringId bg = Constants.MatUI_Square;
            Color bgColor = new Color(70, 83, 90);

            Label = new TextAPI.TextPackage(256, backgroundTexture: bg);
            Label.Background.BillBoardColor = bgColor;
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

                StringBuilder sb = Label.Text.Message.Clear();
                if(tooltip != null)
                {
                    sb.Append(tooltip);
                    TextSize = Label.Text.GetTextLength();
                }
                else
                {
                    TextSize = Vector2D.Zero;
                }
            }

            // tooltip's Draw() needs to happen after all the buttons's Draw().
            DrawThisTick = true;
        }

        public void HoverEnd()
        {
            DrawThisTick = false;
        }

        public void SetVisible(bool visible)
        {
            if(Label != null)
            {
                Label.Visible = visible;
            }
        }

        public void Draw(Vector2D mouseOnScreen, bool drawNow)
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

            // TODO: needs to limit bottom-left chat too... maybe change it to be BB intersection and if it hits it sends the text box to a certain alignment on a certain axis

            if((mouseOnScreen.X + absTextSize.X + tooltipOffset) > ScreenLimitMax.X) // box collides with right side of screen
            {
                //offset.X = -TextSize.X - TooltipOffset; // flip pivot to right, so it's on the left of the mouse
                pos.X -= (mouseOnScreen.X + absTextSize.X + tooltipOffset) - ScreenLimitMax.X; // prevent tooltip from exiting scren on the right
            }

            if(absTextSize.Y < 1) // only flip pivot if tooltip is reasonably sized, if it's larger than the screen then just leave it be.
            {
                if((mouseOnScreen.Y - absTextSize.Y - tooltipOffset) < -ScreenLimitMax.Y) // box collides with bottom of screen
                {
                    offset.Y = -TextSize.Y + tooltipOffset; // flip pivot to bottom, so it's above the mouse
                }
            }

            //DebugDraw.DrawHudText(new System.Text.StringBuilder($"textSize={absTextSize}\npos={pos}\noffset={offset}"), new Vector2D(0, 0), 1.0);

            Label.Text.Offset = offset;

            Label.Background.Width = (float)absTextSize.X + tooltipEdge;
            Label.Background.Height = (float)absTextSize.Y + tooltipEdge;
            Label.Background.Offset = offset + (TextSize / 2);

            Label.Background.Origin = pos;
            Label.Text.Origin = pos;

            if(drawNow)
            {
                Label.Background.Draw();
                Label.Text.Draw();
            }
        }
    }
}
