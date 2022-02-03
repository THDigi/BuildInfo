using Digi.BuildInfo.Features.LiveData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class Suspension : SpecializedOverlayBase
    {
        static Color Color = new Color(255, 200, 0);
        static Vector4 ColorLine = (Color * LaserOverlayAlpha).ToVector4();

        const float LineWidth = 0.05f;

        public Suspension(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_MotorSuspension));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BData_Suspension data = Main.LiveDataHandler.Get<BData_Suspension>(def, drawInstance.BDataCache);
            if(data == null || data.StatorDef == null || data.TopDef == null)
                return;

            MyMotorSuspensionDefinition suspensionDef = def as MyMotorSuspensionDefinition;
            if(suspensionDef == null)
                return;

            MyCubeBlockDefinitionGroup blockPair = MyDefinitionManager.Static.TryGetDefinitionGroup(suspensionDef.TopPart);
            if(blockPair == null)
                return;

            MyCubeBlockDefinition topDef = blockPair[suspensionDef.CubeSize];
            if(topDef == null)
                return;

            BData_Wheel wheelData = Main.LiveDataHandler.Get<BData_Wheel>(topDef);
            if(wheelData == null)
                return;

            Matrix localMatrix = Matrix.Identity;

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            MatrixD gridWorldMatrix = blockWorldMatrix;

            float height = 0; // MathHelper.Clamp(-9999, suspensionDef.MinHeight, suspensionDef.MaxHeight);
            MatrixD topMatrix = data.GetWheelMatrix(localMatrix, blockWorldMatrix, gridWorldMatrix, height);

            // MyMotorSuspension.CanPlaceRotor() does it negative too
            //topMatrix.Translation -= Vector3D.TransformNormal(wheelData.WheelDummy, topMatrix);

            //float axisLengthHalf = (def.Size.Z * MyDefinitionManager.Static.GetCubeSize(def.CubeSize)) * 1.5f;

            float totalTravel = (suspensionDef.MaxHeight - suspensionDef.MinHeight);
            MyTransparentGeometry.AddLineBillboard(MaterialLaser, ColorLine, topMatrix.Translation + blockWorldMatrix.Forward * suspensionDef.MinHeight, blockWorldMatrix.Forward, totalTravel, LineWidth, BlendType);

            if(drawInstance.LabelRender.CanDrawLabel())
            {
                Vector3D labelDir = blockWorldMatrix.Forward;
                Vector3D labelLineStart = topMatrix.Translation + labelDir * suspensionDef.MinHeight;
                drawInstance.LabelRender.DrawLineLabel(LabelType.SteeringAxis, labelLineStart, labelDir, Color, "Steering axis & suspension travel", lineHeight: 0);
            }
        }
    }
}
