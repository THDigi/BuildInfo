using System.Text;
using Draygo.API;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Utilities
{
    /// <summary>
    /// Extremely inefficient methods, for debugging purposes only!
    /// </summary>
    internal class DebugDraw
    {
        public static void Draw3DText(StringBuilder text, Vector3D pos, double scale = 0.05, bool alwaysOnTop = false, bool constantSize = false, int liveTime = 0)
        {
            if(!BuildInfoMod.Instance.TextAPI.WasDetected)
                return; // no textAPI

            IMyCamera cam = MyAPIGateway.Session.Camera;
            if(Vector3D.Dot(pos - cam.Position, cam.WorldMatrix.Forward) <= 0)
                return; // behind camera, ignore

            MatrixD cm = cam.WorldMatrix;

            if(alwaysOnTop)
            {
                double DepthRatio = 0.01;

                Vector3D dirN = Vector3D.Normalize(pos - cm.Translation);

                pos = cm.Translation + (dirN * DepthRatio);

                if(!constantSize)
                    scale *= DepthRatio;
            }
            else if(constantSize)
            {
                scale *= Vector3D.Distance(pos, cm.Translation) * BuildInfoMod.Instance.DrawUtils.ScaleFOV;
            }

            new HudAPIv2.SpaceMessage(text, pos, cm.Up, cm.Left, scale, TimeToLive: 2 + liveTime, Blend: BlendTypeEnum.PostPP);
        }

        public static void DrawHudText3DPos(StringBuilder text, Vector3D pos, double scale = 0.5)
        {
            if(!BuildInfoMod.Instance.TextAPI.WasDetected)
                return; // no textAPI

            IMyCamera cam = MyAPIGateway.Session.Camera;

            if(Vector3D.Dot(pos - cam.Position, cam.WorldMatrix.Forward) <= 0)
                return; // behind camera, ignore

            Vector3D transformed = cam.WorldToScreen(ref pos);

            new HudAPIv2.HUDMessage(text, new Vector2D(transformed.X, transformed.Y), Scale: scale, HideHud: false, TimeToLive: 2, Blend: BlendTypeEnum.PostPP);
        }

        public static void DrawOBB(MyOrientedBoundingBoxD obb, Color color, MySimpleObjectRasterizer draw = MySimpleObjectRasterizer.SolidAndWireframe, BlendTypeEnum blend = BlendTypeEnum.PostPP, bool extraAdditive = true)
        {
            MatrixD wm = MatrixD.CreateFromQuaternion(obb.Orientation);
            wm.Translation = obb.Center;

            BoundingBoxD localBB = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);

            MyStringId mat = MyStringId.GetOrCompute("Square");

            MySimpleObjectDraw.DrawTransparentBox(ref wm, ref localBB, ref color, MySimpleObjectRasterizer.SolidAndWireframe, 1, faceMaterial: mat, lineMaterial: mat, blendType: blend);

            if(extraAdditive)
                DrawOBB(obb, color, draw, BlendTypeEnum.AdditiveTop, extraAdditive: false);
        }

        public static void DrawSphere(BoundingSphereD sphere, Color color, MySimpleObjectRasterizer draw = MySimpleObjectRasterizer.SolidAndWireframe, BlendTypeEnum blend = BlendTypeEnum.PostPP)
        {
            MyStringId mat = MyStringId.GetOrCompute("Square");
            MatrixD wm = MatrixD.CreateTranslation(sphere.Center);
            MySimpleObjectDraw.DrawTransparentSphere(ref wm, (float)sphere.Radius, ref color, draw, 24, mat, mat, 0.01f, blendType: blend);
        }

        public static void DrawFrustum(BoundingFrustumD frustum, float scale = 1f, MySimpleObjectRasterizer draw = MySimpleObjectRasterizer.SolidAndWireframe, BlendTypeEnum blend = BlendTypeEnum.PostPP)
        {
            MyStringId mat = MyStringId.GetOrCompute("Square");

            Vector3D[] corners = frustum.GetCorners();

            if(draw == MySimpleObjectRasterizer.SolidAndWireframe || draw == MySimpleObjectRasterizer.Wireframe)
            {
                float thick;
                Color color;

                // close square
                //thick = 0.001f * scale;
                //color = Color.Lime;
                //DrawLine(corners[0], corners[1], color, thick, blend);
                //DrawLine(corners[1], corners[2], color, thick, blend);
                //DrawLine(corners[2], corners[3], color, thick, blend);
                //DrawLine(corners[3], corners[0], color, thick, blend);

                // connecting lines
                thick = 0.01f * scale;
                color = Color.Yellow;
                DrawLine(corners[0], corners[4], color, thick, blend);
                DrawLine(corners[1], corners[5], color, thick, blend);
                DrawLine(corners[2], corners[6], color, thick, blend);
                DrawLine(corners[3], corners[7], color, thick, blend);

                // far square
                thick = 0.1f * scale;
                color = Color.Red;
                DrawLine(corners[4], corners[5], color, thick, blend);
                DrawLine(corners[5], corners[6], color, thick, blend);
                DrawLine(corners[6], corners[7], color, thick, blend);
                DrawLine(corners[7], corners[4], color, thick, blend);
            }

            if(draw == MySimpleObjectRasterizer.SolidAndWireframe || draw == MySimpleObjectRasterizer.Solid)
            {
                //for(int i = 0; i < corners.Length; i++)
                //{
                //    Vector3D corner = corners[i];
                //    DrawHudText3DPos(new StringBuilder($"c{i}"), corner, 1);
                //}

                Color color = Color.Wheat * 0.25f;
                MyQuadD quad = default(MyQuadD);

                // top
                {
                    quad.Point0 = corners[0];
                    quad.Point1 = corners[4];
                    quad.Point2 = corners[5];
                    quad.Point3 = corners[1];
                    Vector3D center = (quad.Point0 + quad.Point1 + quad.Point2 + quad.Point3) / 4;
                    MyTransparentGeometry.AddQuad(mat, ref quad, color, ref center, blendType: blend);
                }

                // right
                {
                    quad.Point0 = corners[1];
                    quad.Point1 = corners[5];
                    quad.Point2 = corners[6];
                    quad.Point3 = corners[2];
                    Vector3D center = (quad.Point0 + quad.Point1 + quad.Point2 + quad.Point3) / 4;
                    MyTransparentGeometry.AddQuad(mat, ref quad, color, ref center, blendType: blend);
                }

                // left
                {
                    quad.Point0 = corners[0];
                    quad.Point1 = corners[3];
                    quad.Point2 = corners[7];
                    quad.Point3 = corners[4];
                    Vector3D center = (quad.Point0 + quad.Point1 + quad.Point2 + quad.Point3) / 4;
                    MyTransparentGeometry.AddQuad(mat, ref quad, color, ref center, blendType: blend);
                }

                // bottom
                {
                    quad.Point0 = corners[3];
                    quad.Point1 = corners[2];
                    quad.Point2 = corners[6];
                    quad.Point3 = corners[7];
                    Vector3D center = (quad.Point0 + quad.Point1 + quad.Point2 + quad.Point3) / 4;
                    MyTransparentGeometry.AddQuad(mat, ref quad, color, ref center, blendType: blend);
                }
            }
        }

        public static void DrawLine(Vector3D from, Vector3D to, Color color, float thick = 0.005f, BlendTypeEnum blend = BlendTypeEnum.PostPP)
        {
            MyStringId mat = MyStringId.GetOrCompute("Square");
            MyTransparentGeometry.AddLineBillboard(mat, color, from, (to - from), 1f, thick, blend);
        }

        public static void DrawPlane(PlaneD plane, Color color, float size = 1000, BlendTypeEnum blend = BlendTypeEnum.PostPP)
        {
            MyStringId mat = MyStringId.GetOrCompute("Square");

            Vector3D camPos = MyAPIGateway.Session.Camera.Position;

            //Vector3D center = plane.Normal * plane.D;
            Vector3D center = plane.ProjectPoint(ref camPos); // closest point on plane

            MatrixD matrix = MatrixD.CreateFromDir(plane.Normal);

            MyTransparentGeometry.AddBillboardOriented(mat, color, center, matrix.Left, matrix.Up, size, blend);
        }
    }
}
