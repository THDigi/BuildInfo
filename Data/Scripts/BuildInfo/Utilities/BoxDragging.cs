using System;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Utilities
{
    /// <summary>
    /// Box dragging handler for on-screen elements.
    /// Must call <see cref="Update()"/> every tick, preferably in an input reading context.
    /// </summary>
    public class BoxDragging
    {
        public Vector2D Position { get; set; }

        public BoundingBox2D DragHitbox { get; set; }

        /// <summary>
        /// Button used for dragging.
        /// </summary>
        public MyMouseButtonsEnum MouseButton { get; set; } = MyMouseButtonsEnum.Left;

        /// <summary>
        /// Called once when box is first hovered.
        /// </summary>
        public event Action BoxSelected;

        /// <summary>
        /// Called once when box is no longer hovered.
        /// </summary>
        public event Action BoxDeselected;

        /// <summary>
        /// Called continuously as the box is being dragged.
        /// </summary>
        public event Action<Vector2D> Dragging;

        /// <summary>
        /// Called once mouse button is released while it was dragging.
        /// NOTE: It would have the same position value as the last <see cref="Dragging"/> callback.
        /// </summary>
        public event Action<Vector2D> FinishedDragging;

        public bool DrawDragHitbox = false;
        public Color DrawColor = new Color(255, 0, 255) * 0.25f;
        public Color DrawColorSelected = new Color(155, 0, 255) * 0.3f;
        public MyStringId DrawMaterial = MyStringId.GetOrCompute("Square");

        int Rounding;
        bool Hovered;
        Vector2D? ClickOffset;

        public BoxDragging(MyMouseButtonsEnum mouseButton = MyMouseButtonsEnum.Left, int rounding = 6)
        {
            MouseButton = mouseButton;
            Rounding = rounding;
        }

        public void Update()
        {
            if(ClickOffset.HasValue && MyAPIGateway.Input.IsMouseReleased(MouseButton))
            {
                FinishedDragging?.Invoke(Position);
                ClickOffset = null;
                return;
            }

            Vector2 mousePos = MyAPIGateway.Input.GetMousePosition() / MyAPIGateway.Input.GetMouseAreaSize(); // pixels to scalar
            Vector2D mouseOnScreen = new Vector2D(mousePos.X * 2 - 1, 1 - 2 * mousePos.Y); // turn from 0~1 to -1~1

            if(DragHitbox.Contains(mouseOnScreen) == ContainmentType.Contains)
            {
                if(!Hovered)
                {
                    Hovered = true;
                    BoxSelected?.Invoke();
                }

                if(MyAPIGateway.Input.IsNewMousePressed(MouseButton))
                {
                    ClickOffset = (Position - mouseOnScreen);
                }
            }
            else
            {
                if(Hovered)
                {
                    Hovered = false;
                    BoxDeselected?.Invoke();
                }
            }

            if(ClickOffset.HasValue && MyAPIGateway.Input.IsMousePressed(MouseButton))
            {
                Vector2D newPos = mouseOnScreen + ClickOffset.Value;
                newPos = new Vector2D(Math.Round(newPos.X, Rounding), Math.Round(newPos.Y, Rounding));
                newPos = Vector2D.Clamp(newPos, -Vector2D.One, Vector2D.One);

                Position = newPos;
                Dragging?.Invoke(newPos);
            }

            if(DrawDragHitbox)
            {
                DrawUtils drawUtils = BuildInfoMod.Instance.DrawUtils;
                MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;

                Vector3D pos = drawUtils.TextAPIHUDtoWorld(DragHitbox.Center);

                Vector2 viewport = MyAPIGateway.Session.Camera.ViewportSize;
                float aspectRatio = viewport.X / viewport.Y;

                const float Magic = 0.025f; // needed to match textAPI box...
                float width = (float)DragHitbox.Width * Magic * aspectRatio * drawUtils.ScaleFOV;
                float height = (float)DragHitbox.Height * Magic * drawUtils.ScaleFOV;

                MyTransparentGeometry.AddBillboardOriented(DrawMaterial, (Hovered ? DrawColorSelected : DrawColor), pos, camMatrix.Left, camMatrix.Up, width, height, Vector2.Zero, blendType: BlendTypeEnum.PostPP);
            }

            // for debugging
            //{
            //    DrawUtils drawUtils = BuildInfoMod.Instance.DrawUtils;
            //    MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            //    float w = (0.0005f * drawUtils.ScaleFOV);
            //    float h = w;

            //    Vector3D worldPos = drawUtils.TextAPIHUDtoWorld(DragHitbox.Min);
            //    MyTransparentGeometry.AddBillboardOriented(MyStringId.GetOrCompute("WhiteDot"), Color.Lime, worldPos, camMatrix.Left, camMatrix.Up, w, h, Vector2.Zero, blendType: BlendTypeEnum.PostPP);

            //    worldPos = drawUtils.TextAPIHUDtoWorld(DragHitbox.Max);
            //    MyTransparentGeometry.AddBillboardOriented(MyStringId.GetOrCompute("WhiteDot"), Color.Red, worldPos, camMatrix.Left, camMatrix.Up, w, h, Vector2.Zero, blendType: BlendTypeEnum.PostPP);

            //    worldPos = drawUtils.TextAPIHUDtoWorld(Position);
            //    MyTransparentGeometry.AddBillboardOriented(MyStringId.GetOrCompute("WhiteDot"), new Color(255, 0, 255), worldPos, camMatrix.Left, camMatrix.Up, w, h, Vector2.Zero, blendType: BlendTypeEnum.PostPP);
            //}
        }
    }
}
