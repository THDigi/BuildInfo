using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Draygo.API;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace Digi.BuildInfo.Features.GUI.Elements
{
    public static class CornerBackgroundExtensions
    {
        public static bool IsSet(this CornerFlag flags, CornerFlag flag)
        {
            return (flags & flag) != 0;
        }
    }

    public enum BillboardType { Debug, Flat, Corner }

    [Flags]
    public enum CornerFlag
    {
        None = 0,
        All = int.MaxValue,
        TopLeft = (1 << 0),
        TopRight = (1 << 1),
        BottomLeft = (1 << 2),
        BottomRight = (1 << 3),
    }

    public class CornerBackground
    {
        public struct Element
        {
            public readonly HudAPIv2.BillBoardHUDMessage Billboard;
            public readonly BillboardType Type;

            public Element(out HudAPIv2.BillBoardHUDMessage assign, BillboardType type)
            {
                assign = new HudAPIv2.BillBoardHUDMessage();
                Billboard = assign;
                Type = type;
            }
        }

        public struct CornerSize
        {
            public Vector2 TopLeft;
            public Vector2 TopRight;
            public Vector2 BottomLeft;
            public Vector2 BottomRight;

            /// <summary>
            /// All numbers are in pixels.
            /// </summary>
            public CornerSize(float topLeft, float topRight, float bottomLeft, float bottomRight)
            {
                TopLeft = new Vector2(topLeft);
                TopRight = new Vector2(topRight);
                BottomLeft = new Vector2(bottomLeft);
                BottomRight = new Vector2(bottomRight);
            }

            /// <summary>
            /// All numbers are in pixels.
            /// </summary>
            public CornerSize(Vector2 topLeft, Vector2 topRight, Vector2 bottomLeft, Vector2 bottomRight)
            {
                TopLeft = topLeft;
                TopRight = topRight;
                BottomLeft = bottomLeft;
                BottomRight = bottomRight;
            }

            /// <summary>
            /// All numbers are in pixels.
            /// </summary>
            public CornerSize(float allSize)
            {
                var allSizeVec = new Vector2(allSize);
                TopLeft = allSizeVec;
                TopRight = allSizeVec;
                BottomLeft = allSizeVec;
                BottomRight = allSizeVec;
            }

            public Vector2 GetCorner(CornerFlag corner)
            {
                switch(corner)
                {
                    case CornerFlag.TopLeft: return TopLeft;
                    case CornerFlag.TopRight: return TopRight;
                    case CornerFlag.BottomLeft: return BottomLeft;
                    case CornerFlag.BottomRight: return BottomRight;
                    default: throw new Exception("invalid corner, must be exactly one corner!");
                }
            }

            public void SetCorners(CornerFlag corners, Vector2 value)
            {
                bool valid = false;

                if(corners.IsSet(CornerFlag.TopLeft))
                {
                    valid = true;
                    TopLeft = value;
                }

                if(corners.IsSet(CornerFlag.TopRight))
                {
                    valid = true;
                    TopRight = value;
                }

                if(corners.IsSet(CornerFlag.BottomLeft))
                {
                    valid = true;
                    BottomLeft = value;
                }

                if(corners.IsSet(CornerFlag.BottomRight))
                {
                    valid = true;
                    BottomRight = value;
                }

                if(!valid)
                    throw new Exception("invalid corners input, no corners given");
            }
        }

        public readonly CornerFlag Corners;

        public readonly HudAPIv2.BillBoardHUDMessage CenterLeft;
        public readonly HudAPIv2.BillBoardHUDMessage CenterMiddle;
        public readonly HudAPIv2.BillBoardHUDMessage CenterRight;
        public readonly HudAPIv2.BillBoardHUDMessage CenterTop;
        public readonly HudAPIv2.BillBoardHUDMessage CenterBottom;

        public readonly HudAPIv2.BillBoardHUDMessage CornerTopLeft;
        public readonly HudAPIv2.BillBoardHUDMessage CornerTopRight;
        public readonly HudAPIv2.BillBoardHUDMessage CornerBottomLeft;
        public readonly HudAPIv2.BillBoardHUDMessage CornerBottomRight;

        readonly HudAPIv2.BillBoardHUDMessage DebugPivot;
        readonly HudAPIv2.BillBoardHUDMessage DebugSize;

        public readonly bool DebugMode;

        public readonly List<Element> Elements = new List<Element>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="color">null defaults to <see cref="Constants.Color_UIBackground"/></param>
        /// <param name="corners"></param>
        /// <param name="flatMaterial">plain material for center parts; null uses <see cref="Constants.MatUI_Square"/></param>
        /// <param name="cornerMaterial">Must be top-left variant, gets rotated automatically for other corners; null uses <see cref="Constants.MatUI_Corner"/></param>
        /// <param name="debugMode"></param>
        public CornerBackground(Color? color = null, CornerFlag corners = CornerFlag.All, MyStringId? flatMaterial = null, MyStringId? cornerMaterial = null, bool debugMode = false)
        {
            if(!color.HasValue)
                color = Constants.Color_UIBackground;

            if(!flatMaterial.HasValue)
                flatMaterial = Constants.MatUI_Square;

            if(!cornerMaterial.HasValue)
                cornerMaterial = Constants.MatUI_Corner;

            DebugMode = debugMode;
            Corners = corners;

            if(Corners.IsSet(CornerFlag.TopLeft))
            {
                Elements.Add(new Element(out CornerTopLeft, BillboardType.Corner));
            }

            if(Corners.IsSet(CornerFlag.TopRight))
            {
                Elements.Add(new Element(out CornerTopRight, BillboardType.Corner));
                CornerTopRight.Rotation = MathHelper.ToRadians(90);
            }

            if(Corners.IsSet(CornerFlag.BottomLeft))
            {
                Elements.Add(new Element(out CornerBottomLeft, BillboardType.Corner));
                CornerBottomLeft.Rotation = MathHelper.ToRadians(-90);
            }

            if(Corners.IsSet(CornerFlag.BottomRight))
            {
                Elements.Add(new Element(out CornerBottomRight, BillboardType.Corner));
                CornerBottomRight.Rotation = MathHelper.ToRadians(180);
            }

            Elements.Add(new Element(out CenterMiddle, BillboardType.Flat));

            if(CornerTopLeft != null || CornerBottomLeft != null)
                Elements.Add(new Element(out CenterLeft, BillboardType.Flat));

            if(CornerTopRight != null || CornerBottomRight != null)
                Elements.Add(new Element(out CenterRight, BillboardType.Flat));

            if(CornerTopLeft != null || CornerTopRight != null)
                Elements.Add(new Element(out CenterTop, BillboardType.Flat));

            if(CornerBottomLeft != null || CornerBottomRight != null)
                Elements.Add(new Element(out CenterBottom, BillboardType.Flat));

            if(DebugMode)
            {
                Elements.Add(new Element(out DebugSize, BillboardType.Debug));
                DebugSize.BillBoardColor = Color.Yellow * 0.2f;

                Elements.Add(new Element(out DebugPivot, BillboardType.Debug));
                DebugPivot.BillBoardColor = new Color(255, 0, 255);
                DebugPivot.Width = 4;
                DebugPivot.Height = 4;
            }

            foreach(Element element in Elements)
            {
                element.Billboard.Material = element.Type == BillboardType.Corner ? cornerMaterial.Value : flatMaterial.Value;
                element.Billboard.Options = HudAPIv2.Options.Pixel;
                element.Billboard.Blend = MyBillboard.BlendTypeEnum.PostPP;
                element.Billboard.Visible = false;
            }

            SetColor(color.Value);
        }

        public void Dispose()
        {
            foreach(Element element in Elements)
            {
                element.Billboard.DeleteMessage();
            }
        }

        public void SetColor(Color color)
        {
            int c = 0;

            foreach(Element element in Elements)
            {
                if(element.Type == BillboardType.Debug)
                    continue;

                if(DebugMode)
                {
                    if(element.Type == BillboardType.Corner)
                        element.Billboard.BillBoardColor = Color.Red * (color.A / 255f);
                    else
                        element.Billboard.BillBoardColor = Utils.GetIndexColor(c++, 5) * (color.A / 255f);
                }
                else
                {
                    element.Billboard.BillBoardColor = color;
                }
            }
        }

        public void SetVisible(bool visible)
        {
            foreach(Element element in Elements)
            {
                element.Billboard.Visible = visible;
            }
        }

        /// <summary>
        /// Manual draw for one frame regardless of visible state
        /// </summary>
        public void Draw()
        {
            foreach(Element element in Elements)
            {
                element.Billboard.Draw();
            }
        }

        /// <summary>
        /// Everything is in pixels!
        /// </summary>
        public void SetProperties(Vector2D pos, Vector2 boxSize, CornerSize cornerSize)
        {
            // FIXME: flaw if 3+ corners are different size https://cdn.discordapp.com/attachments/126460115204308993/1116251964993450074/image.png

            // TODO posPx needs to specify a pivot corner too

            if(DebugMode)
            {
                DebugPivot.Origin = pos;
                DebugPivot.Offset = Vector2D.Zero;
                DebugSize.Offset = Vector2D.Zero;
                DebugSize.Width = boxSize.X;
                DebugSize.Height = boxSize.Y;
            }

            foreach(Element element in Elements)
            {
                element.Billboard.Origin = pos;
            }

            UpdateCorner(CornerTopLeft, ref cornerSize, CornerFlag.TopLeft, new Vector2(0, 0));
            UpdateCorner(CornerTopRight, ref cornerSize, CornerFlag.TopRight, new Vector2(boxSize.X, 0));
            UpdateCorner(CornerBottomLeft, ref cornerSize, CornerFlag.BottomLeft, new Vector2(0, boxSize.Y));
            UpdateCorner(CornerBottomRight, ref cornerSize, CornerFlag.BottomRight, new Vector2(boxSize.X, boxSize.Y));

            if(CenterLeft != null)
            {
                CenterLeft.Width = Math.Max(cornerSize.TopLeft.X, cornerSize.BottomLeft.X);
                CenterLeft.Height = boxSize.Y - cornerSize.TopLeft.Y - cornerSize.BottomLeft.Y;
                CenterLeft.Offset = new Vector2D(0, cornerSize.TopLeft.Y);
            }

            if(CenterRight != null)
            {
                CenterRight.Width = Math.Max(cornerSize.TopRight.X, cornerSize.BottomRight.X);
                CenterRight.Height = boxSize.Y - cornerSize.TopRight.Y - cornerSize.BottomRight.Y;
                CenterRight.Offset = new Vector2D(boxSize.X - CenterRight.Width, cornerSize.TopRight.Y);
            }

            if(CenterTop != null)
            {
                CenterTop.Width = boxSize.X - cornerSize.TopLeft.X - cornerSize.TopRight.X;
                CenterTop.Height = Math.Min(cornerSize.TopLeft.Y, cornerSize.TopRight.Y);
                CenterTop.Offset = new Vector2D(cornerSize.TopLeft.X, 0);
            }

            if(CenterBottom != null)
            {
                CenterBottom.Width = boxSize.X - cornerSize.BottomLeft.X - cornerSize.BottomRight.X;
                CenterBottom.Height = Math.Min(cornerSize.BottomLeft.Y, cornerSize.BottomRight.Y);
                CenterBottom.Offset = new Vector2D(cornerSize.BottomLeft.X, boxSize.Y - Math.Min(cornerSize.BottomLeft.Y, cornerSize.BottomRight.Y));
            }

            if(CenterMiddle != null)
            {
                CenterMiddle.Width = boxSize.X - (CenterLeft?.Width ?? 0) - (CenterRight?.Width ?? 0);
                CenterMiddle.Height = boxSize.Y - (CenterTop?.Height ?? 0) - (CenterBottom?.Height ?? 0);
                CenterMiddle.Offset = new Vector2D(Math.Max(cornerSize.TopLeft.X, cornerSize.BottomLeft.X), CenterTop?.Height ?? 0);
            }
        }

        void UpdateCorner(HudAPIv2.BillBoardHUDMessage billboard, ref CornerSize cornerSizePx, CornerFlag corner, Vector2 cornerOuterPos)
        {
            if(billboard == null)
            {
                cornerSizePx.SetCorners(corner, Vector2.Zero);
                return;
            }

            Vector2 sizePx = cornerSizePx.GetCorner(corner);
            billboard.Width = sizePx.X;
            billboard.Height = sizePx.Y;

            if(corner == CornerFlag.BottomLeft)
                cornerOuterPos.Y -= sizePx.Y;
            else if(corner == CornerFlag.TopRight)
                cornerOuterPos.X -= sizePx.X;
            else if(corner == CornerFlag.BottomRight)
                cornerOuterPos -= sizePx;

            billboard.Offset = cornerOuterPos;
        }
    }
}