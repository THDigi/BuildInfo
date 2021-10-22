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
        static Color ColorSensorFace = ColorSensorText * OverlayAlpha;

        static Color ColorMineText = Color.Lime;
        static Color ColorMineFace = ColorMineText * OverlayAlpha;

        static Color ColorCarveText = Color.Red;
        static Color ColorCarveFace = ColorCarveText * OverlayAlpha;

        public ShipDrill(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_Drill));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            MyShipDrillDefinition drill = def as MyShipDrillDefinition;
            if(drill == null)
                return;

            const int wireDivRatio = 20;
            float lineThickness = 0.03f;
            bool drawLabel = drawInstance.LabelRender.CanDrawLabel();

            #region Mining
            MatrixD mineMatrix = drawMatrix;
            mineMatrix.Translation += mineMatrix.Forward * drill.CutOutOffset;
            float mineRadius = Hardcoded.ShipDrill_VoxelVisualAdd + drill.CutOutRadius;
            Utils.DrawTransparentSphere(ref mineMatrix, mineRadius, ref ColorMineFace, MySimpleObjectRasterizer.Wireframe, wireDivRatio, lineThickness: lineThickness, material: MaterialLaser, blendType: BlendType);

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
            Utils.DrawTransparentSphere(ref carveMatrix, carveRadius, ref ColorCarveFace, MySimpleObjectRasterizer.Wireframe, wireDivRatio, lineThickness: lineThickness, material: MaterialLaser, blendType: BlendType);

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
                Utils.DrawTransparentSphere(ref sensorMatrix, sensorRadius, ref ColorSensorFace, MySimpleObjectRasterizer.Wireframe, wireDivRatio, lineThickness: lineThickness, material: MaterialLaser, blendType: BlendType);
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
