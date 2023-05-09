using System;
using System.Text;
using Digi.BuildInfo.Systems;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.GUI
{
    public class Button
    {
        public static readonly MyStringId MaterialBg = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBg");
        public static readonly MyStringId MaterialBgHover = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBgHover");
        public static readonly MyStringId MaterialBgActivate = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBgActivate");
        public const float EdgePadding = 0.025f;

        public bool Selected { get; private set; }
        public bool Visible { get; set; } = true;

        public Color DefaultColor = Color.White;

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
            Label = new TextAPI.TextPackage(new StringBuilder(label), backgroundTexture: MaterialBg);

            Tooltip = tooltip;
            TooltipHandler = tooltipHandler;

            HoverAction = hover;
            HoverEndAction = hoverEnd;
        }

        public void Refresh(Vector2D pos, float scale, bool badPivot = false)
        {
            Label.Background.BillBoardColor = DefaultColor;
            Label.Background.Origin = pos;
            Label.Text.Scale = scale;
            Label.Text.Origin = pos;

            Vector2D textSize = Label.Text.GetTextLength();

            if(badPivot)
                // original "bottom-right" pivot which is just middle-ish bottom
                Label.Text.Offset = -textSize + new Vector2D((EdgePadding / 2) * scale);
            else
                // proper bottom-right pivot
                Label.Text.Offset = -textSize + new Vector2D((EdgePadding / 2) * -scale, (EdgePadding / 2) * scale);

            Label.Background.Width = (float)Math.Abs(textSize.X) + (EdgePadding * scale);
            Label.Background.Height = (float)Math.Abs(textSize.Y) + (EdgePadding * scale);
            Label.Background.Offset = Label.Text.Offset + (textSize / 2);

            Vector2D centerPos = Label.Background.Origin + Label.Background.Offset;
            Vector2D halfSize = new Vector2D(Label.Background.Width, Label.Background.Height) / 2;
            ButtonBB = new BoundingBox2D(centerPos - halfSize, centerPos + halfSize);
        }

        public void Draw(Vector2D mouseOnScreen, bool directDraw = true)
        {
            if(!Visible)
                return;

            if(ButtonBB.Contains(mouseOnScreen) == ContainmentType.Contains)
            {
                Selected = true;
                Label.Background.Material = MaterialBgHover;
                Label.Background.BillBoardColor = DefaultColor;

                TooltipHandler?.Hover(Tooltip);
                HoverAction.Invoke(this);
            }
            else if(Selected)
            {
                Selected = false;
                Label.Background.Material = MaterialBg;
                Label.Background.BillBoardColor = DefaultColor;

                TooltipHandler?.HoverEnd();
                HoverEndAction?.Invoke(this);
            }

            if(directDraw)
            {
                Label.Background.Draw();
                Label.Text.Draw();
            }
        }
    }
}
