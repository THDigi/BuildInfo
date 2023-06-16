using System;
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

        /// <summary>
        /// Requires TextAPI to've been initialized!
        /// </summary>
        public TooltipHandler()
        {
            //MyStringId bg = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBgHover");
            //Color bgColor = Color.White;

            MyStringId bg = MyStringId.GetOrCompute("BuildInfo_UI_Square");
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
            Label.Text.Origin = pos;

            if(drawNow)
            {
                Label.Background.Draw();
                Label.Text.Draw();
            }
        }
    }
}
