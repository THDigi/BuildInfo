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
    public class Thruster : SpecializedOverlayBase
    {
        static Color Color = Color.Red;
        static Color ColorLines = Color * LaserOverlayAlpha;

        const int LineEveryDeg = RoundedQualityLow;
        const float LineThickness = 0.02f;

        public Thruster(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_Thrust));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BData_Thrust data = Main.LiveDataHandler.Get<BData_Thrust>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            MatrixD capsuleMatrix = MatrixD.CreateWorld(Vector3D.Zero, drawMatrix.Up, drawMatrix.Backward); // capsule is rotated weirdly (pointing up), needs adjusting
            bool drawLabel = drawInstance.LabelRender.CanDrawLabel();

            foreach(BData_Thrust.FlameInfo flame in data.Flames)
            {
                Vector3D start = Vector3D.Transform(flame.LocalFrom, drawMatrix);
                capsuleMatrix.Translation = start + (drawMatrix.Forward * (flame.CapsuleLength * 0.5)); // capsule's position is in the center

                float paddedRadius = flame.CapsuleRadius + Hardcoded.Thrust_DamageCapsuleRadiusAdd;
                Utils.DrawTransparentCapsule(ref capsuleMatrix, paddedRadius, flame.CapsuleLength, ref ColorLines, MySimpleObjectRasterizer.Wireframe, (360 / LineEveryDeg), lineThickness: LineThickness, material: MaterialLaser, blendType: BlendType);

                if(drawLabel)
                {
                    drawLabel = false; // label only on the first flame
                    Vector3D labelDir = drawMatrix.Down;
                    Vector3D labelLineStart = Vector3D.Transform(flame.LocalTo, drawMatrix) + labelDir * paddedRadius;
                    drawInstance.LabelRender.DrawLineLabel(LabelType.ThrustDamage, labelLineStart, labelDir, Color, "Thrust damage");
                }
            }
        }
    }
}
