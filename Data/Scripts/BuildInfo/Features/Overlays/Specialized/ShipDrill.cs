using System;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
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

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            float mineRadius = Hardcoded.ShipDrill_VoxelVisualAdd + drill.CutOutRadius;

            #region Mining
            {
                MatrixD mineMatrix = blockWorldMatrix;
                mineMatrix.Translation += mineMatrix.Forward * drill.CutOutOffset;

                Utils.DrawSphere(ref mineMatrix, mineRadius, LineEveryDeg,
                   wireColor: ColorMineLines, wireMaterial: MaterialLaser, wireThickness: LineThickness, wireBlend: BlendType);

                if(drawLabel)
                {
                    Vector3D labelDir = mineMatrix.Up;
                    Vector3D sphereEdge = mineMatrix.Translation + (labelDir * mineRadius);
                    drawInstance.LabelRender.DrawLineLabel(LabelType.MineRadius, sphereEdge, labelDir, ColorMineText, "Mining radius");
                }
            }
            #endregion

            #region Carving
            {
                MatrixD carveMatrix = blockWorldMatrix;
                carveMatrix.Translation += carveMatrix.Forward * drill.CutOutOffset;

                float carveRadius = Hardcoded.ShipDrill_VoxelVisualAdd + (drill.CutOutRadius * drill.DiscardingMultiplier);

                Utils.DrawSphere(ref carveMatrix, carveRadius, LineEveryDeg,
                   wireColor: ColorCarveLines, wireMaterial: MaterialLaser, wireThickness: LineThickness, wireBlend: BlendType);

                if(drawLabel)
                {
                    Vector3D labelDir = carveMatrix.Up;
                    Vector3D sphereEdge = carveMatrix.Translation + (labelDir * carveRadius);
                    drawInstance.LabelRender.DrawLineLabel(LabelType.CarveRadius, sphereEdge, labelDir, ColorCarveText, "Carving radius");
                }
            }
            #endregion

            #region Sensor
            {
                MatrixD sensorMatrix = blockWorldMatrix;
                sensorMatrix.Translation += sensorMatrix.Forward * drill.SensorOffset;

                float sensorRadius = drill.SensorRadius;

                // only draw it if it's a different size or offset
                if(Math.Abs(mineRadius - sensorRadius) > 0.001f || Math.Abs(drill.CutOutOffset - drill.SensorOffset) > 0.001f)
                {
                    Utils.DrawSphere(ref sensorMatrix, sensorRadius, LineEveryDeg,
                       wireColor: ColorSensorLines, wireMaterial: MaterialLaser, wireThickness: LineThickness, wireBlend: BlendType);
                }

                if(drawLabel)
                {
                    Vector3D labelDir = drawMatrix.Left;
                    Vector3D sphereEdge = sensorMatrix.Translation + (labelDir * sensorRadius);
                    drawInstance.LabelRender.DrawLineLabel(LabelType.SensorRadius, sphereEdge, labelDir, ColorSensorText, "Entity detection radius");
                }
            }
            #endregion
        }
    }
}
