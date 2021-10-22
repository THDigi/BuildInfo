using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class ShipTool : SpecializedOverlayBase
    {
        static Color Color = Color.Lime;
        static Color ColorFace = Color * OverlayAlpha;

        public ShipTool(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_ShipWelder));
            Add(typeof(MyObjectBuilder_ShipGrinder));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BData_ShipTool data = Main.LiveDataHandler.Get<BData_ShipTool>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            const int wireDivRatio = 20;
            const float lineThickness = 0.03f;

            MyShipToolDefinition toolDef = (MyShipToolDefinition)def;
            Vector3 sensorCenter = data.DummyMatrix.Translation + data.DummyMatrix.Forward * toolDef.SensorOffset;
            drawMatrix.Translation = Vector3D.Transform(sensorCenter, drawMatrix);
            float radius = toolDef.SensorRadius;

            Utils.DrawTransparentSphere(ref drawMatrix, radius, ref ColorFace, MySimpleObjectRasterizer.Wireframe, wireDivRatio, lineThickness: lineThickness, material: MaterialLaser, blendType: BlendType);

            if(drawInstance.LabelRender.CanDrawLabel())
            {
                bool isWelder = def is MyShipWelderDefinition;
                Vector3D labelDir = drawMatrix.Down;
                Vector3D sphereEdge = drawMatrix.Translation + (labelDir * radius);

                if(isWelder)
                    drawInstance.LabelRender.DrawLineLabel(LabelType.WeldingRadius, sphereEdge, labelDir, Color, "Welding radius");
                else
                    drawInstance.LabelRender.DrawLineLabel(LabelType.GrindingRadius, sphereEdge, labelDir, Color, "Grinding radius");
            }
        }
    }
}
