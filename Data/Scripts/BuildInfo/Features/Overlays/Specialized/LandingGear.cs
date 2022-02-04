using Digi.BuildInfo.Features.LiveData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class LandingGear : SpecializedOverlayBase
    {
        static Color Color = new Color(20, 255, 155);
        static Color ColorLines = Color * LaserOverlayAlpha;

        const int SubdivideBox = 1;
        const float LineWidth = 0.03f;

        public LandingGear(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_LandingGear));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BData_LandingGear data = Main.LiveDataHandler.Get<BData_LandingGear>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            bool drawLabel = drawInstance.LabelRender.CanDrawLabel();

            foreach(MyOrientedBoundingBoxD obb in data.Magents)
            {
                BoundingBoxD localBB = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);
                MatrixD m = MatrixD.CreateFromQuaternion(obb.Orientation);
                m.Translation = obb.Center;
                m *= drawMatrix;

                MySimpleObjectDraw.DrawTransparentBox(ref m, ref localBB, ref ColorLines, MySimpleObjectRasterizer.Wireframe, (1 + SubdivideBox), LineWidth, MaterialSquare, MaterialLaser, blendType: BlendType);

                if(drawLabel)
                {
                    drawLabel = false; // only label the first one
                    Vector3D labelDir = drawMatrix.Down;
                    Vector3D labelLineStart = m.Translation + (m.Down * localBB.HalfExtents.Y) + (m.Backward * localBB.HalfExtents.Z) + (m.Left * localBB.HalfExtents.X);
                    drawInstance.LabelRender.DrawLineLabel(LabelType.MagnetizedArea, labelLineStart, labelDir, Color, "Magnetized Area");
                }
            }
        }
    }
}
