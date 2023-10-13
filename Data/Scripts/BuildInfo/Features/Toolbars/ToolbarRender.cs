﻿using System;
using System.Text;
using Digi.BuildInfo.Features.GUI;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Draygo.API;
using Sandbox.ModAPI;
using VRage.Input;
using VRage.Utils;
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

        const float BackgroundOpacity = 0.75f;
        public static readonly Color BackgroundColor = new Color(41, 54, 62);
        public static readonly Color BackgroundColorSelected = new Color(40, 80, 65);

        public const int MaxNameLength = 32; // last X characters
        public const int MaxNameLengthIfPbArg = MaxNameLength - MaxArgLength; // last X characters
        public const int MaxActionNameLength = 28; // first X characters
        public const int MaxArgLength = 16; // first X characters

        const double ScaleMultiplier = 0.75;

        public bool IsInit { get; private set; } = false;

        public readonly StringBuilder TextSB = new StringBuilder(512);

        CornerBackground Box;
        TextAPI.TextPackage Text;
        HudAPIv2.BillBoardHUDMessage DebugPivot;

        BoxDragging BoxDrag;
        float GUIScale;

        readonly BuildInfoMod Main;

        public ToolbarRender()
        {
            Main = BuildInfoMod.Instance;

            BoxDrag = new BoxDragging(MyMouseButtonsEnum.Left);
            BoxDrag.BoxSelected += () => UpdateBgOpacity(BackgroundOpacity, BackgroundColorSelected);
            BoxDrag.BoxDeselected += () => UpdateBgOpacity(BackgroundOpacity);
            BoxDrag.Dragging += (newPos) =>
            {
                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(scroll != 0)
                {
                    ConfigLib.FloatSetting setting = Main.Config.ToolbarLabelsMenuScale;
                    float scale = setting.Value + (scroll > 0 ? 0.05f : -0.05f);
                    scale = MathHelper.Clamp(scale, setting.Min, setting.Max);
                    setting.Value = (float)Math.Round(scale, 3);
                }

                Main.Config.ToolbarLabelsMenuPosition.Value = newPos;
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
            Box = new CornerBackground("BuildInfo_UI_Square", "BuildInfo_UI_Corner", BackgroundColor, CornerFlag.BottomLeft | CornerFlag.TopRight, debugMode: DebugDraw);
            Text = new TextAPI.TextPackage(TextSB);

            if(DebugDraw)
            {
                DebugPivot = new HudAPIv2.BillBoardHUDMessage();
                DebugPivot.BillBoardColor = new Color(255, 255, 0);
                DebugPivot.Material = MyStringId.GetOrCompute("Square");
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

            Vector2D configPos = Main.Config.ToolbarLabelsMenuPosition.Value;
            GUIScale = (float)(ScaleMultiplier * Main.Config.ToolbarLabelsMenuScale.Value);

            Text.Scale = GUIScale;

            Vector2D textSize = Text.Text.GetTextLength();
            Vector2D px = HudAPIv2.APIinfo.ScreenPositionOnePX;

            Vector2D padding = (new Vector2D(12, -12) * GUIScale * px);
            textSize += padding * 2;

            Vector2D pos = configPos + new Vector2D(0, -textSize.Y); // bottom-left pivot

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
                DebugPivot.Origin = Main.DrawUtils.TextAPIHUDToPixels(configPos);
                DebugPivot.Offset = Vector2D.Zero;
            }
        }

        public void SetVisible(bool visible)
        {
            Box.SetVisible(visible);
            Text.Visible = visible;

            if(DebugDraw)
                DebugPivot.Visible = visible;
        }

        void UpdateBgOpacity(float opacity, Color? colorOverride = null)
        {
            if(Box == null)
                return;

            Color color = (colorOverride ?? BackgroundColor);
            Utils.FadeColorHUD(ref color, opacity);
            Box.SetColor(color);
        }

        public void UpdateBoxDrag()
        {
            BoxDrag.Position = Main.Config.ToolbarLabelsMenuPosition.Value;
            BoxDrag.Update();
        }
    }
}
