using Digi.BuildInfo.Features.LiveData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class Camera : SpecializedOverlayBase
    {
        static Color ColorRaycast = new Color(20, 255, 155);
        static Vector4 ColorRaycastPyramid = (ColorRaycast * SolidOverlayAlpha).ToVector4();

        static Color ColorCamera = new Color(55, 155, 255);

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

            BData_Camera data = Main.LiveDataHandler.Get<BData_Camera>(def);

            bool canDrawLabel = drawInstance.LabelRender.CanDrawLabel();

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            #region Raycast limits
            {
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

                Vector3D dirTop = Vector3D.Transform(blockWorldMatrix.Forward, Quaternion.CreateFromAxisAngle((Vector3)blockWorldMatrix.Right, angle));
                Vector3D dirBottom = Vector3D.Transform(blockWorldMatrix.Forward, Quaternion.CreateFromAxisAngle((Vector3)blockWorldMatrix.Right, -angle));
                Vector3D dirLeft = Vector3D.Transform(blockWorldMatrix.Forward, Quaternion.CreateFromAxisAngle((Vector3)blockWorldMatrix.Up, angle));
                Vector3D dirRight = Vector3D.Transform(blockWorldMatrix.Forward, Quaternion.CreateFromAxisAngle((Vector3)blockWorldMatrix.Up, -angle));

                Vector3D pos = blockWorldMatrix.Translation;
                Vector3D topToRight = (pos + dirRight * length) - (pos + dirTop * length);
                double halfDist = Vector3D.Dot(topToRight, blockWorldMatrix.Right);

                Vector3D n = Vector3D.Forward;

                Vector2 uv0 = new Vector2(0.0f, 0.5f);
                Vector2 uv1 = new Vector2(1.0f, 0.0f);
                Vector2 uv2 = new Vector2(1.0f, 1.0f);

                {
                    Vector3D dir = dirTop * length;
                    Vector3D dirCross = blockWorldMatrix.Right * halfDist;
                    Vector3D p0 = pos;
                    Vector3D p1 = pos + dir + dirCross;
                    Vector3D p2 = pos + dir - dirCross;
                    MyTransparentGeometry.AddTriangleBillboard(p0, p1, p2, n, n, n, uv0, uv1, uv2, MaterialGradient, uint.MaxValue, p0, ColorRaycastPyramid, BlendType);
                }
                {
                    Vector3D dir = dirBottom * length;
                    Vector3D dirCross = blockWorldMatrix.Right * halfDist;
                    Vector3D p0 = pos;
                    Vector3D p1 = pos + dir + dirCross;
                    Vector3D p2 = pos + dir - dirCross;
                    MyTransparentGeometry.AddTriangleBillboard(p0, p1, p2, n, n, n, uv0, uv1, uv2, MaterialGradient, uint.MaxValue, p0, ColorRaycastPyramid, BlendType);
                }
                {
                    Vector3D dir = dirLeft * length;
                    Vector3D dirCross = blockWorldMatrix.Up * halfDist;
                    Vector3D p0 = pos;
                    Vector3D p1 = pos + dir + dirCross;
                    Vector3D p2 = pos + dir - dirCross;
                    MyTransparentGeometry.AddTriangleBillboard(p0, p1, p2, n, n, n, uv0, uv1, uv2, MaterialGradient, uint.MaxValue, p0, ColorRaycastPyramid, BlendType);
                }
                {
                    Vector3D dir = dirRight * length;
                    Vector3D dirCross = blockWorldMatrix.Up * halfDist;
                    Vector3D p0 = pos;
                    Vector3D p1 = pos + dir + dirCross;
                    Vector3D p2 = pos + dir - dirCross;
                    MyTransparentGeometry.AddTriangleBillboard(p0, p1, p2, n, n, n, uv0, uv1, uv2, MaterialGradient, uint.MaxValue, p0, ColorRaycastPyramid, BlendType);
                }

                if(canDrawLabel)
                {
                    Vector3D labelDir = blockWorldMatrix.Backward;
                    Vector3D labelLineStart = pos;
                    drawInstance.LabelRender.DrawLineLabel(LabelType.RaycastLimits, labelLineStart, labelDir, ColorRaycast, "Raycast Limits");
                }
            }
            #endregion

            #region Camera viewpoint
            if(data != null)
            {
                // from MyCameraBlock.GetViewMatrix()
                MatrixD view = blockWorldMatrix;
                view.Translation += blockWorldMatrix.Forward * 0.20000000298023224;
                view.Translation += Vector3.TransformNormal(data.DummyLocalAdditive, view);

                MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorCamera, view.Translation, (Vector3)view.Forward, 3, 0.025f, BlendType);
                MyTransparentGeometry.AddPointBillboard(MaterialDot, ColorCamera, view.Translation, 0.04f, 0, blendType: BlendType);

                if(canDrawLabel)
                {
                    Vector3D labelDir = view.Backward;
                    Vector3D labelLineStart = view.Translation;

                    drawInstance.LabelRender.DrawLineLabel(LabelType.Camera, labelLineStart, labelDir, ColorCamera, "Camera", scale: 0.75f, alwaysOnTop: true);
                }
            }
            #endregion
        }
    }
}
