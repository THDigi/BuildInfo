using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
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

                const float thick = 0.025f;

                {
                    DrawDirectionalFace? faces = new DrawDirectionalFace()
                    {
                        Material = MaterialGradient,
                        Color = ColorCameraPyramid,
                        Blend = BlendType,
                    };

                    DrawDirectionalLine? parallel = new DrawDirectionalLine()
                    {
                        Material = MaterialGradient,
                        Color = ColorCameraLine,
                        Thick = thick,
                        Blend = BlendType,
                    };

                    DrawLine? start = null;

                    DrawLine? end = null;

                    Utils.DrawFrustum(ref frustum, faces, parallel, start, end);
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

                {
                    DrawDirectionalFace? faces = new DrawDirectionalFace()
                    {
                        Material = MaterialGradient,
                        Color = ColorRaycastPyramid,
                        Blend = BlendType,
                    };

                    DrawDirectionalLine? parallel = new DrawDirectionalLine()
                    {
                        Material = MaterialGradient,
                        Color = ColorRaycastLine,
                        Thick = thick,
                        Blend = BlendType,
                    };

                    DrawLine? end = null;

                    Utils.DrawPyramid(ref blockWorldMatrix, angle, length, faces, parallel, end);
                }

                if(canDrawLabel)
                {
                    Vector3D labelDir = blockWorldMatrix.Up;
                    Vector3D labelLineStart = blockWorldMatrix.Translation;
                    drawInstance.LabelRender.DrawLineLabel(LabelType.RaycastLimits, labelLineStart, labelDir, ColorRaycastText, "Raycast Limits");
                }
            }
            #endregion
        }
    }
}
