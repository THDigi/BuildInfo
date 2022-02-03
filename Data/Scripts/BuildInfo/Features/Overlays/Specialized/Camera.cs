using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class Camera : SpecializedOverlayBase
    {
        static Color Color = new Color(20, 255, 155);
        static Vector4 ColorPyramid = (Color * SolidOverlayAlpha).ToVector4();

        const float LaserWidth = 0.01f;

        public Camera(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_CameraBlock));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            MyCameraBlockDefinition camDef = def as MyCameraBlockDefinition;
            if(camDef == null)
                return;

            //IMyCameraBlock camBlock = block?.FatBlock as IMyCameraBlock;
            //if(camBlock != null && camBlock.EnableRaycast)
            //{
            //   // MyCameraBlock.m_lastRay not exposed so nothing to really do here
            //} 

            float length = 5;
            if(camDef.RaycastDistanceLimit > 0 && length > camDef.RaycastDistanceLimit)
                length = (float)camDef.RaycastDistanceLimit;

            // NOTE: not a cone but a pyramid!
            float angle = MathHelper.ToRadians(camDef.RaycastConeLimit);

            // my drawMatrix is always centered to block, ModelOffset affects where the position camera raycasts from.
            Vector3D startPos = Vector3D.Transform(def.ModelOffset, drawMatrix);

            Vector3D dirTop = Vector3D.Transform(drawMatrix.Forward, Quaternion.CreateFromAxisAngle((Vector3)drawMatrix.Right, angle));
            Vector3D dirBottom = Vector3D.Transform(drawMatrix.Forward, Quaternion.CreateFromAxisAngle((Vector3)drawMatrix.Right, -angle));
            Vector3D dirLeft = Vector3D.Transform(drawMatrix.Forward, Quaternion.CreateFromAxisAngle((Vector3)drawMatrix.Up, angle));
            Vector3D dirRight = Vector3D.Transform(drawMatrix.Forward, Quaternion.CreateFromAxisAngle((Vector3)drawMatrix.Up, -angle));

            Vector3D topToRight = (startPos + dirRight * length) - (startPos + dirTop * length);
            double halfDist = Vector3D.Dot(topToRight, drawMatrix.Right);

            Vector3D n = Vector3D.Forward;

            Vector2 uv0 = new Vector2(0.0f, 0.5f);
            Vector2 uv1 = new Vector2(1.0f, 0.0f);
            Vector2 uv2 = new Vector2(1.0f, 1.0f);

            {
                Vector3D dir = dirTop * length;
                Vector3D dirCross = drawMatrix.Right * halfDist;
                Vector3D p0 = startPos;
                Vector3D p1 = startPos + dir + dirCross;
                Vector3D p2 = startPos + dir - dirCross;
                MyTransparentGeometry.AddTriangleBillboard(p0, p1, p2, n, n, n, uv0, uv1, uv2, MaterialGradient, uint.MaxValue, p0, ColorPyramid, BlendType);
            }
            {
                Vector3D dir = dirBottom * length;
                Vector3D dirCross = drawMatrix.Right * halfDist;
                Vector3D p0 = startPos;
                Vector3D p1 = startPos + dir + dirCross;
                Vector3D p2 = startPos + dir - dirCross;
                MyTransparentGeometry.AddTriangleBillboard(p0, p1, p2, n, n, n, uv0, uv1, uv2, MaterialGradient, uint.MaxValue, p0, ColorPyramid, BlendType);
            }
            {
                Vector3D dir = dirLeft * length;
                Vector3D dirCross = drawMatrix.Up * halfDist;
                Vector3D p0 = startPos;
                Vector3D p1 = startPos + dir + dirCross;
                Vector3D p2 = startPos + dir - dirCross;
                MyTransparentGeometry.AddTriangleBillboard(p0, p1, p2, n, n, n, uv0, uv1, uv2, MaterialGradient, uint.MaxValue, p0, ColorPyramid, BlendType);
            }
            {
                Vector3D dir = dirRight * length;
                Vector3D dirCross = drawMatrix.Up * halfDist;
                Vector3D p0 = startPos;
                Vector3D p1 = startPos + dir + dirCross;
                Vector3D p2 = startPos + dir - dirCross;
                MyTransparentGeometry.AddTriangleBillboard(p0, p1, p2, n, n, n, uv0, uv1, uv2, MaterialGradient, uint.MaxValue, p0, ColorPyramid, BlendType);
            }

            if(drawInstance.LabelRender.CanDrawLabel())
            {
                Vector3D labelDir = drawMatrix.Down;
                Vector3D labelLineStart = startPos;
                drawInstance.LabelRender.DrawLineLabel(LabelType.RaycastLimits, labelLineStart, labelDir, Color, "Raycast Limits");
            }
        }
    }
}
