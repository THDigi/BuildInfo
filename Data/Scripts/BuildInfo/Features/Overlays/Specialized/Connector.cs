using System;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class Connector : SpecializedOverlayBase
    {
        Color Color = new Color(255, 255, 0);
        Color ColorShape = new Color(255, 255, 0) * LaserOverlayAlpha;

        public Connector(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_ShipConnector));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BData_Connector data = Main.LiveDataHandler.Get<BData_Connector>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            Vector3D connectPos = Vector3D.Transform(data.ConnectPosition, blockWorldMatrix);

            MatrixD coneMatrix = blockWorldMatrix;
            coneMatrix.Translation = connectPos;

            float baseRadius = (float)Math.Tan(Hardcoded.Connector_ConnectAngleMinMax * Hardcoded.Connector_ConnectMaxDistance);

            Utils.DrawTransparentCone(ref coneMatrix, baseRadius, Hardcoded.Connector_ConnectMaxDistance, ref ColorShape, MySimpleObjectRasterizer.Solid, RoundedQualityMed, MaterialSquare, blendType: BlendType);

            if(drawInstance.LabelRender.CanDrawLabel())
            {
                Vector3D labelDir = coneMatrix.Down;
                Vector3D labelLineStart = coneMatrix.Translation;
                drawInstance.LabelRender.DrawLineLabel(LabelType.ConnectorLimits, labelLineStart, labelDir, Color, "Connector angle & distance limits");
            }
        }
    }
}
