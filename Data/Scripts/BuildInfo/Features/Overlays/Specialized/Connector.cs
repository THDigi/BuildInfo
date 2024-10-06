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
            var connectorDef = def as MyShipConnectorDefinition;
            if(connectorDef == null)
                return;

            BData_Connector data = Main.LiveDataHandler.Get<BData_Connector>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            Vector3D connectPos = Vector3D.Transform(data.ConnectPosition, blockWorldMatrix);

            // HACK: from MyShipConnector.TryAttach(), flipped because the math is weird, and normalized because it also technically does on the angle check.
            Vector3D connectDir = (connectorDef.ConnectDirection.X * blockWorldMatrix.Left
                                 + connectorDef.ConnectDirection.Y * blockWorldMatrix.Up
                                 + connectorDef.ConnectDirection.Z * blockWorldMatrix.Forward);
            connectDir = -Vector3D.Normalize(connectDir);

            MatrixD coneMatrix = MatrixD.CreateFromDir(connectDir, blockWorldMatrix.Up);
            coneMatrix.Translation = connectPos;

            float baseRadius = (float)Math.Tan(Hardcoded.Connector_ConnectAngleOffAxis * Hardcoded.Connector_ConnectMaxDistance);
            float coneHeight = Hardcoded.Connector_ConnectMaxDistance;

            Utils.DrawTransparentCone(ref coneMatrix, baseRadius, coneHeight, ref ColorShape, MySimpleObjectRasterizer.Solid, RoundedQualityMed, MaterialSquare, blendType: BlendType);
            MyTransparentGeometry.AddPointBillboard(MaterialDot, Color, coneMatrix.Translation, 0.05f, 0, blendType: BlendType);

            if(drawInstance.LabelRender.CanDrawLabel())
            {
                Vector3D labelDir = coneMatrix.Right;
                Vector3D labelLineStart = coneMatrix.Translation + coneMatrix.Forward * coneHeight + labelDir * baseRadius;
                drawInstance.LabelRender.DrawLineLabel(LabelType.ConnectorLimits, labelLineStart, labelDir, Color, "Connector angle & distance limits");
            }
        }
    }
}
