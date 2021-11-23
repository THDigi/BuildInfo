using System;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class ShipDrill : SpecializedOverlayBase
    {
        static Color ColorSensorText = Color.Gray;
        static Color ColorSensorLines = ColorSensorText * LaserOverlayAlpha;

        static Color ColorMineText = Color.Lime;
        static Color ColorMineLines = ColorMineText * LaserOverlayAlpha;

        static Color ColorCarveText = Color.Red;
        static Color ColorCarveLines = ColorCarveText * LaserOverlayAlpha;

        const int LineEveryDeg = RoundedQualityLow;
        float LineThickness = 0.03f;

        public ShipDrill(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_Drill));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            MyShipDrillDefinition drill = def as MyShipDrillDefinition;
            if(drill == null)
                return;

            bool drawLabel = drawInstance.LabelRender.CanDrawLabel();

            #region Mining
            MatrixD mineMatrix = drawMatrix;
            mineMatrix.Translation += mineMatrix.Forward * drill.CutOutOffset;
            float mineRadius = Hardcoded.ShipDrill_VoxelVisualAdd + drill.CutOutRadius;
            Utils.DrawTransparentSphere(ref mineMatrix, mineRadius, ref ColorMineLines, MySimpleObjectRasterizer.Wireframe, (360 / LineEveryDeg), lineThickness: LineThickness, material: MaterialLaser, blendType: BlendType);

            if(drawLabel)
            {
                Vector3D labelDir = mineMatrix.Up;
                Vector3D sphereEdge = mineMatrix.Translation + (labelDir * mineRadius);
                drawInstance.LabelRender.DrawLineLabel(LabelType.MineRadius, sphereEdge, labelDir, ColorMineText, "Mining radius");
            }
            #endregion

            #region Carving
            MatrixD carveMatrix = mineMatrix;
            float carveRadius = Hardcoded.ShipDrill_VoxelVisualAdd + (drill.CutOutRadius * Hardcoded.Drill_MineVoelNoOreRadiusMul);
            Utils.DrawTransparentSphere(ref carveMatrix, carveRadius, ref ColorCarveLines, MySimpleObjectRasterizer.Wireframe, (360 / LineEveryDeg), lineThickness: LineThickness, material: MaterialLaser, blendType: BlendType);

            if(drawLabel)
            {
                Vector3D labelDir = carveMatrix.Up;
                Vector3D sphereEdge = carveMatrix.Translation + (labelDir * carveRadius);
                drawInstance.LabelRender.DrawLineLabel(LabelType.CarveRadius, sphereEdge, labelDir, ColorCarveText, "Carving radius");
            }
            #endregion

            #region Sensor
            MatrixD sensorMatrix = drawMatrix;
            sensorMatrix.Translation += sensorMatrix.Forward * drill.SensorOffset;
            float sensorRadius = drill.SensorRadius;

            if(Math.Abs(mineRadius - sensorRadius) > 0.001f || Math.Abs(drill.CutOutOffset - drill.SensorOffset) > 0.001f)
            {
                Utils.DrawTransparentSphere(ref sensorMatrix, sensorRadius, ref ColorSensorLines, MySimpleObjectRasterizer.Wireframe, (360 / LineEveryDeg), lineThickness: LineThickness, material: MaterialLaser, blendType: BlendType);
            }

            if(drawLabel)
            {
                Vector3D labelDir = drawMatrix.Left;
                Vector3D sphereEdge = sensorMatrix.Translation + (labelDir * sensorRadius);
                drawInstance.LabelRender.DrawLineLabel(LabelType.SensorRadius, sphereEdge, labelDir, ColorSensorText, "Entity detection radius");
            }
            #endregion
        }
    }
}
