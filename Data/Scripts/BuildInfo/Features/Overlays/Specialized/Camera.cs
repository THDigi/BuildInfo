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
        static Color ColorRaycastText = new Color(200, 40, 40);
        static Color ColorRaycastOverlay = new Color(100, 25, 25);
        static Vector4 ColorRaycastPyramid = (ColorRaycastOverlay * SolidOverlayAlpha).ToVector4();
        static Vector4 ColorRaycastLine = ColorRaycastOverlay.ToVector4();

        static Color ColorCamera = new Color(100, 200, 255);
        static Vector4 ColorCameraPyramid = (ColorCamera * SolidOverlayAlpha).ToVector4();
        static Vector4 ColorCameraLine = ColorCamera.ToVector4();

        static Vector3D[] Corners = new Vector3D[8];

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

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            bool canDrawLabel = drawInstance.LabelRender.CanDrawLabel();

            #region Camera viewpoint
            if(data != null)
            {
                // from MyCameraBlock.GetViewMatrix()
                MatrixD viewWorld = blockWorldMatrix;
                viewWorld.Translation += blockWorldMatrix.Forward * 0.20000000298023224;
                viewWorld.Translation += Vector3.TransformNormal(data.DummyLocalAdditive, viewWorld);

                float angle = camDef.MaxFov;
                const float length = 15;

                // HACK: near plane and math from MyCamera
                MatrixD projection = MatrixD.CreatePerspectiveFieldOfView(angle, Main.GameConfig.AspectRatio, 0.05f, length);
                MatrixD viewInverted = MatrixD.Invert(viewWorld);
                BoundingFrustumD frustum = new BoundingFrustumD(viewInverted * projection);
                frustum.GetCorners(Corners);

                // wireframe connecting lines
                for(int i = 0; i <= 3; i++)
                {
                    const float thick = 0.025f;

                    Vector3D start = Corners[i];
                    Vector3D end = Corners[i + 4];
                    MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorCameraLine, start, (end - start), 1f, thick, BlendType);
                }

                // solid
                {
                    MyQuadD quad = default(MyQuadD);

                    // top
                    {
                        quad.Point0 = Corners[0];
                        quad.Point1 = Corners[4];
                        quad.Point2 = Corners[5];
                        quad.Point3 = Corners[1];
                        Vector3D center = (quad.Point0 + quad.Point1 + quad.Point2 + quad.Point3) * 0.25;
                        MyTransparentGeometry.AddQuad(MaterialGradient, ref quad, ColorCameraPyramid, ref center, blendType: BlendType);
                    }

                    // bottom
                    {
                        quad.Point0 = Corners[2];
                        quad.Point1 = Corners[6];
                        quad.Point2 = Corners[7];
                        quad.Point3 = Corners[3];
                        Vector3D center = (quad.Point0 + quad.Point1 + quad.Point2 + quad.Point3) * 0.25;
                        MyTransparentGeometry.AddQuad(MaterialGradient, ref quad, ColorCameraPyramid, ref center, blendType: BlendType);
                    }

                    // left
                    {
                        quad.Point0 = Corners[3];
                        quad.Point1 = Corners[7];
                        quad.Point2 = Corners[4];
                        quad.Point3 = Corners[0];
                        Vector3D center = (quad.Point0 + quad.Point1 + quad.Point2 + quad.Point3) * 0.25;
                        MyTransparentGeometry.AddQuad(MaterialGradient, ref quad, ColorCameraPyramid, ref center, blendType: BlendType);
                    }

                    // right
                    {
                        quad.Point0 = Corners[1];
                        quad.Point1 = Corners[5];
                        quad.Point2 = Corners[6];
                        quad.Point3 = Corners[2];
                        Vector3D center = (quad.Point0 + quad.Point1 + quad.Point2 + quad.Point3) * 0.25;
                        MyTransparentGeometry.AddQuad(MaterialGradient, ref quad, ColorCameraPyramid, ref center, blendType: BlendType);
                    }
                }

                if(canDrawLabel)
                {
                    Vector3D labelDir = blockWorldMatrix.Forward;
                    Vector3D labelLineStart = viewWorld.Translation;

                    drawInstance.LabelRender.DrawLineLabel(LabelType.Camera, labelLineStart, labelDir, ColorCamera, "Camera\n(With your screen aspect-ratio)", scale: 0.75f, alwaysOnTop: true);
                }
            }
            #endregion

            // render raycast over camera viewpoint as this one is way smaller
            #region Raycast limits
            {
                //IMyCameraBlock camBlock = block?.FatBlock as IMyCameraBlock;
                //if(camBlock != null && camBlock.EnableRaycast)
                //{
                //   // MyCameraBlock.m_lastRay not exposed so nothing to really do here
                //} 

                const float thick = 0.01f;

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

                // solid & wireframe border
                {
                    Vector3D dir = dirTop * length;
                    Vector3D dirCross = blockWorldMatrix.Right * halfDist;
                    Vector3D p1 = pos + dir + dirCross;
                    Vector3D p2 = pos + dir - dirCross;
                    MyTransparentGeometry.AddTriangleBillboard(pos, p1, p2, n, n, n, uv0, uv1, uv2, MaterialGradient, uint.MaxValue, pos, ColorRaycastPyramid, BlendType);

                    MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorRaycastLine, pos, (p1 - pos), 1f, thick, BlendType);
                }
                {
                    Vector3D dir = dirBottom * length;
                    Vector3D dirCross = blockWorldMatrix.Right * halfDist;
                    Vector3D p1 = pos + dir + dirCross;
                    Vector3D p2 = pos + dir - dirCross;
                    MyTransparentGeometry.AddTriangleBillboard(pos, p1, p2, n, n, n, uv0, uv1, uv2, MaterialGradient, uint.MaxValue, pos, ColorRaycastPyramid, BlendType);

                    MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorRaycastLine, pos, (p2 - pos), 1f, thick, BlendType);
                }
                {
                    Vector3D dir = dirLeft * length;
                    Vector3D dirCross = blockWorldMatrix.Up * halfDist;
                    Vector3D p1 = pos + dir + dirCross;
                    Vector3D p2 = pos + dir - dirCross;
                    MyTransparentGeometry.AddTriangleBillboard(pos, p1, p2, n, n, n, uv0, uv1, uv2, MaterialGradient, uint.MaxValue, pos, ColorRaycastPyramid, BlendType);

                    MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorRaycastLine, pos, (p1 - pos), 1f, thick, BlendType);
                }
                {
                    Vector3D dir = dirRight * length;
                    Vector3D dirCross = blockWorldMatrix.Up * halfDist;
                    Vector3D p1 = pos + dir + dirCross;
                    Vector3D p2 = pos + dir - dirCross;
                    MyTransparentGeometry.AddTriangleBillboard(pos, p1, p2, n, n, n, uv0, uv1, uv2, MaterialGradient, uint.MaxValue, pos, ColorRaycastPyramid, BlendType);

                    MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorRaycastLine, pos, (p2 - pos), 1f, thick, BlendType);
                }

                if(canDrawLabel)
                {
                    Vector3D labelDir = blockWorldMatrix.Up;
                    Vector3D labelLineStart = pos;
                    drawInstance.LabelRender.DrawLineLabel(LabelType.RaycastLimits, labelLineStart, labelDir, ColorRaycastText, "Raycast Limits");
                }
            }
            #endregion
        }
    }
}
