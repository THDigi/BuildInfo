using System;
using System.Text;
using Digi.BuildInfo.Systems;
using Draygo.API;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.GUI
{
    public enum Align { TopLeft, TopRight, BottomLeft, BottomRight }

    public class Button
    {
        public static readonly MyStringId MaterialBg = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBg");
        public static readonly MyStringId MaterialBgHover = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBgHover");
        public static readonly MyStringId MaterialBgActivate = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBgActivate");
        public const float EdgePadding = 0.025f;

        public bool Selected { get; private set; }
        public bool Visible { get; private set; } = true;

        /// <summary>
        /// If all elements are Draw() in <see cref="Update(Vector2D)"/>, false are rendered by TextAPI.
        /// </summary>
        public readonly bool DirectDraw;

        public Align Pivot = Align.TopLeft;
        public float Scale { get; set; }
        public Color DefaultColor = Color.White;

        public readonly TextAPI.TextPackage Label;

        HudAPIv2.BillBoardHUDMessage DebugPivot;

        readonly Action<Button> HoverAction;
        readonly Action<Button> HoverEndAction;

        readonly string Tooltip;
        readonly ITooltipHandler TooltipHandler;

        BoundingBox2D ButtonBB;

        public Button(string label,
            string tooltip = null, ITooltipHandler tooltipHandler = null,
            Action<Button> hover = null, Action<Button> hoverEnd = null,
            Align pivot = Align.TopLeft,
            bool directDraw = false,
            bool debugPivot = false)
        {
            DirectDraw = directDraw;
            Pivot = pivot;

            Label = new TextAPI.TextPackage(new StringBuilder(label), backgroundTexture: MaterialBg);

            Tooltip = tooltip;
            TooltipHandler = tooltipHandler;

            HoverAction = hover;
            HoverEndAction = hoverEnd;

            if(debugPivot)
            {
                Vector2 pxSize = (Vector2)HudAPIv2.APIinfo.ScreenPositionOnePX;
                DebugPivot = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("WhiteDot"), Vector2D.Zero, Color.Red);
                DebugPivot.Width = pxSize.X * 2;
                DebugPivot.Height = pxSize.Y * 2;
                DebugPivot.Visible = false;
            }
        }

        public void Refresh(Vector2D pos)
        {
            Label.Background.BillBoardColor = DefaultColor;
            Label.Background.Origin = pos;
            Label.Text.Scale = Scale;
            Label.Text.Origin = pos;

            if(DebugPivot != null)
                DebugPivot.Origin = pos;

            Vector2D textSize = Label.Text.GetTextLength();

            float scaledEdge = EdgePadding / 2 * Scale;

            switch(Pivot)
            {
                case Align.TopLeft: Label.Text.Offset = new Vector2D(scaledEdge, -scaledEdge); break;
                case Align.TopRight: Label.Text.Offset = new Vector2D(-textSize.X - scaledEdge, -scaledEdge); break;
                case Align.BottomLeft: Label.Text.Offset = new Vector2D(scaledEdge, -textSize.Y + scaledEdge); break;
                case Align.BottomRight: Label.Text.Offset = new Vector2D(-textSize.X - scaledEdge, -textSize.Y + scaledEdge); break;
            }

            Label.Background.Width = (float)Math.Abs(textSize.X) + (EdgePadding * Scale);
            Label.Background.Height = (float)Math.Abs(textSize.Y) + (EdgePadding * Scale);
            Label.Background.Offset = Label.Text.Offset + (textSize / 2);

            Vector2D centerPos = Label.Background.Origin + Label.Background.Offset;
            Vector2D halfSize = new Vector2D(Label.Background.Width, Label.Background.Height) / 2;
            ButtonBB = new BoundingBox2D(centerPos - halfSize, centerPos + halfSize);
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;

            if(!DirectDraw)
            {
                Label.Visible = visible;

                if(DebugPivot != null)
                    DebugPivot.Visible = visible;
            }
        }

        public void Update(Vector2D mouseOnScreen)
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

            if(DirectDraw)
            {
                Label.Draw();
                DebugPivot?.Draw();
            }
        }
    }
}
