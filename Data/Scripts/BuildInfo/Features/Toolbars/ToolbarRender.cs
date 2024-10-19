using System;
using System.Text;
using Digi.BuildInfo.Features.GUI.Elements;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Draygo.API;
using Sandbox.ModAPI;
using VRage.Input;
using VRageMath;
using VRageRender;

namespace Digi.BuildInfo.Features.Toolbars
{
    public class ToolbarRender
    {
        static readonly bool DebugDraw = false;

        public static readonly Color HeaderColor = new Color(255, 240, 220);
        public static readonly Color GroupColor = new Color(155, 220, 255);
        public static readonly Color ArgColor = new Color(55, 200, 155);
        public static readonly Color WeaponColor = new Color(255, 220, 155);
        public static readonly Color OtherItemColor = new Color(200, 210, 215);

        const float BackgroundOpacityMul = 0.98f;
        const float BackgroundOpacityHoverMin = 0.8f;
        public static readonly Color BackgroundColor = Constants.Color_UIBackground;
        public static readonly Color BackgroundColorSelected = new Color(40, 80, 65);

        public const int MaxNameLength = 32; // last X characters
        public const int MaxNameLengthIfPbArg = MaxNameLength - MaxArgLength; // last X characters
        public const int MaxActionNameLength = 28; // first X characters
        public const int MaxArgLength = 16; // first X characters

        public const double ScaleMultiplier = 0.75;

        public bool IsInit { get; private set; } = false;

        public readonly StringBuilder TextSB = new StringBuilder(512);

        public CornerBackground Box;
        public TextAPI.TextPackage Text;
        HudAPIv2.BillBoardHUDMessage DebugPivot;

        public BoxDragging BoxDrag;

        public bool IsVisible => Text.Visible;
        public Vector2D HUDPosition;
        public float GUIScale;

        readonly BuildInfoMod Main;

        public ToolbarRender()
        {
            Main = BuildInfoMod.Instance;

            BoxDrag = new BoxDragging(MyMouseButtonsEnum.Left);
            BoxDrag.BoxSelected += () => UpdateBgOpacity();
            BoxDrag.BoxDeselected += () => UpdateBgOpacity();
            BoxDrag.Dragging += (newPos) =>
            {
                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(scroll != 0)
                {
                    ConfigLib.FloatSetting setting = Main.Config.ToolbarLabelsMenuScale;
                    float scale = setting.Value + (scroll > 0 ? 0.05f : -0.05f);
                    scale = MathHelper.Clamp(scale, setting.Min, setting.Max);
                    setting.SetValue((float)Math.Round(scale, 3));
                }

                Main.Config.ToolbarLabelsMenuPosition.SetValue(newPos);
                UpdateProperties();
            };
            BoxDrag.FinishedDragging += (finalPos) =>
            {
                Main.Config.Save();
                Main.ConfigMenuHandler.RefreshAll();
            };
        }

        /// <summary>
        /// Must be called after TextAPI confirms initialization!
        /// </summary>
        public void CreateUI()
        {
            Box = new CornerBackground(BackgroundColor, CornerFlag.BottomLeft | CornerFlag.TopRight, debugMode: DebugDraw);
            Text = new TextAPI.TextPackage(TextSB);

            if(DebugDraw)
            {
                DebugPivot = new HudAPIv2.BillBoardHUDMessage();
                DebugPivot.BillBoardColor = new Color(255, 255, 0);
                DebugPivot.Material = Constants.MatUI_Square;
                DebugPivot.Options = HudAPIv2.Options.Pixel;
                DebugPivot.Blend = MyBillboard.BlendTypeEnum.PostPP;
                DebugPivot.Width = 4;
                DebugPivot.Height = 4;
                DebugPivot.Visible = false;
            }

            UpdateProperties();

            IsInit = true;
        }

        public void Reset()
        {
            Text.TextStringBuilder.Clear();
        }

        public void UpdateProperties()
        {
            Text.TextStringBuilder.TrimEndWhitespace();

            Text.Scale = GUIScale;

            Vector2D textSize = Text.Text.GetTextLength();
            Vector2D px = HudAPIv2.APIinfo.ScreenPositionOnePX;

            Vector2D padding = (new Vector2D(12, -12) * GUIScale * px);
            textSize += padding * 2;

            Vector2D pos = HUDPosition + new Vector2D(0, -textSize.Y); // bottom-left pivot

            Text.Position = pos + padding;

            Vector2 posPx = (Vector2)Main.DrawUtils.TextAPIHUDToPixels(pos);

            Vector2D boxSize = new Vector2D(Math.Abs(textSize.X), Math.Abs(textSize.Y));
            Vector2 boxSizePx = (Vector2)(boxSize / px);

            Box.SetProperties(posPx, boxSizePx, new CornerBackground.CornerSize(0, 24 * GUIScale, 12 * GUIScale, 0));

            Vector2D min = Main.DrawUtils.PixelsToTextAPIHUD(posPx);
            Vector2D max = Main.DrawUtils.PixelsToTextAPIHUD(posPx + boxSizePx);
            BoxDrag.DragHitbox = new BoundingBox2D(Vector2D.Min(min, max), Vector2D.Max(min, max));

            if(DebugDraw)
            {
                DebugPivot.Origin = Main.DrawUtils.TextAPIHUDToPixels(HUDPosition);
                DebugPivot.Offset = Vector2D.Zero;
            }

            UpdateBgOpacity();
        }

        public void SetVisible(bool visible)
        {
            if(Box == null || Text == null)
                return;

            Box.SetVisible(visible);
            Text.Visible = visible;

            if(DebugDraw)
                DebugPivot.Visible = visible;
        }

        void UpdateBgOpacity()
        {
            if(Box == null)
                return;

            Color color = (BoxDrag.Hovered ? BackgroundColorSelected : BackgroundColor);
            float opacity = Main.GameConfig.UIBackgroundOpacity * BackgroundOpacityMul;
            opacity = (BoxDrag.Hovered ? Math.Max(opacity, BackgroundOpacityHoverMin) : opacity);

            Utils.FadeColorHUD(ref color, opacity);
            Box.SetColor(color);
        }

        public void UpdateBoxDrag()
        {
            BoxDrag.Position = HUDPosition;
            BoxDrag.Update();
        }
    }
}
