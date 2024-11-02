using System;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using VRageRender;
using static VRageRender.MyBillboard;

namespace Digi.BuildInfo.Features.MultiTool.Instruments
{
    public class PhysicsSnapshot : InstrumentBase
    {
        struct Hit
        {
            /// <summary>
            /// Add shot position to it to get world position
            /// </summary>
            public Vector3 DirScaled;

            public Vector3 Normal;
        }

        readonly Vector2I Resolution = new Vector2I(100, 100);

        Hit[] Hits;
        int HitsCount = 0;
        float[] Distances;

        enum State { Idle, Thread, Draw }
        State CastState = State.Idle;

        int HitLayer = 15;
        bool FirstCast = true;

        readonly MatrixD Projection;
        readonly BoundingFrustumD PreviewFrustum = new BoundingFrustumD();

        readonly BoundingFrustumD SnapFrustum = new BoundingFrustumD();
        MatrixD SnapCamera;
        MatrixD SnapCameraInv;
        MatrixD ProjectionViewInv;
        int SnapLayer;

        int DrawTick = 0;

        static readonly float FOV = MathHelper.ToRadians(50);
        const float NearPlane = 0.5f;
        const float FarPlane = 20f;
        const float AspectRatio = 1f;

        public PhysicsSnapshot() : base("Short Range LIDAR (Beta)", Constants.MatUI_IconWeaponModeSingle)
        {
            DisplayNameHUD = "Short Range LIDAR\n(Beta)";

            Projection = MatrixD.CreatePerspectiveFieldOfView(FOV, AspectRatio, NearPlane, FarPlane);

            Main.GUIMonitor.OptionsMenuClosed += RefreshDescription;
            RefreshDescription();
        }

        public override void Dispose()
        {
            Main.GUIMonitor.OptionsMenuClosed -= RefreshDescription;
        }

        public override void Selected()
        {
            RefreshDescription();
        }

        public override void Deselected()
        {
        }

        void RefreshDescription()
        {
            var sb = Description.Builder.Clear();

            sb.Clear();

            sb.AppendLine("Shows colliders that interact\nwith selected layer.");

            MultiTool.ControlPrimary.GetBind(sb);
            sb.Append(" to snapshot");
            sb.AppendLine();

            MultiTool.ControlSecondary.GetBind(sb);
            sb.Append(" to clear");
            sb.AppendLine();

            sb.Append("Shift+Scroll cycle layer");
            sb.AppendLine();

            sb.Append("Layer: ").Append(Hardcoded.PhysicsLayers[HitLayer].Name).Append(" (").Append(HitLayer).Append(")");
            sb.AppendLine();

            if(CastState == State.Draw)
            {
                sb.AppendLine();

                sb.Append("Snap: ").Append(Hardcoded.PhysicsLayers[SnapLayer].Name).Append(" (").Append(SnapLayer).Append(")");
                sb.AppendLine();

                sb.Append("Pixels: ").Append(HitsCount);
                sb.AppendLine();
            }

            Description.UpdateFromBuilder();
        }

        public override void Update(bool inputReadable)
        {
            if(inputReadable)
            {
                if(MultiTool.ControlPrimary.IsJustPressed())
                {
                    CastRays();
                    RefreshDescription();
                }

                if(MultiTool.ControlSecondary.IsJustPressed())
                {
                    CastState = State.Idle;
                    HitsCount = 0;
                    RefreshDescription();
                }

                if(MyAPIGateway.Input.IsAnyShiftKeyPressed())
                {
                    int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                    if(scroll != 0)
                    {
                        if(Hardcoded.PhysicsLayers.Count == 0)
                            throw new Exception("Unexpected for Hardcoded.PhysicsLayers to be empty, would infinitely loop here.");

                        PhysicsLayerInfo pli;
                        do
                        {
                            if(scroll < 0)
                            {
                                HitLayer++;
                                if(HitLayer > Hardcoded.PhysicsLayerMaxIndex)
                                    HitLayer = 0;
                            }
                            else
                            {
                                HitLayer--;
                                if(HitLayer < 0)
                                    HitLayer = Hardcoded.PhysicsLayerMaxIndex;
                            }
                        }
                        while(!Hardcoded.PhysicsLayers.TryGetValue(HitLayer, out pli)); // skip inexistent layers

                        RefreshDescription();
                    }
                }
            }

            if(Main.Tick % 30 == 0)
            {
                RefreshDescription();
            }
        }

        void CastRays()
        {
            if(CastState == State.Thread)
            {
                MyAPIGateway.Utilities.ShowNotification("Finishing up the previous cast...", 1000, FontsHandler.RedSh);
                return;
            }

            IMyCamera camera = MyAPIGateway.Session.Camera;

            SnapCamera = camera.WorldMatrix;
            SnapCameraInv = camera.ViewMatrix;
            MatrixD viewProjection = SnapCameraInv * Projection;
            SnapFrustum.Matrix = viewProjection;
            ProjectionViewInv = MatrixD.Invert(viewProjection);
            SnapLayer = HitLayer;

            if(Hits == null)
            {
                Hits = new Hit[Resolution.X * Resolution.Y];
                Distances = new float[Hits.Length];
            }

            if(FirstCast)
            {
                FirstCast = false;
                MyLog.Default.WriteLine($"{BuildInfoMod.ModName}: Ignore the following \"error\" about physics from parallel threads. This mod does it properly by blocking main thread until they're done.");
            }

            CastState = State.Thread;
            HitsCount = 0;

            MyAPIGateway.Parallel.For(0, Hits.Length, ProcessPixel);

            MyAPIGateway.Parallel.Start(PostProcessThread);
        }

        void ProcessPixel(int index)
        {
            try
            {
                // vertical first
                int x = index / Resolution.Y;
                int y = index % Resolution.Y;

                // screen to world line
                Vector4D vec = new Vector4D(2f * x / (float)Resolution.X - 1f, 1f - 2f * y / (float)Resolution.Y, 0.0, 1.0);
                Vector4D from = Vector4D.Transform(vec, ProjectionViewInv);
                vec.Z = 1;
                Vector4D to = Vector4D.Transform(vec, ProjectionViewInv);
                from /= from.W;
                to /= to.W;

                Vector3D rayFrom = new Vector3D(from);
                Vector3D rayTo = new Vector3D(to);
                IHitInfo hit;
                if(MyAPIGateway.Physics.CastRay(rayFrom, rayTo, out hit, HitLayer))
                {
                    Hits[index] = new Hit()
                    {
                        DirScaled = hit.Position - SnapCamera.Translation,
                        Normal = hit.Normal,
                    };
                    Distances[index] = hit.Fraction; // not in meters but doesn't matter in this case
                }
                else
                {
                    Hits[index] = default(Hit);
                    Distances[index] = float.MaxValue; // sort last to "trim" later
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void PostProcessThread()
        {
            // sort by distance ascending, leaving the no-hits last
            Array.Sort(Distances, Hits, 0, Hits.Length);

            HitsCount = Hits.Length; // fallback in case all are hits

            for(int i = 0; i < Hits.Length; i++)
            {
                if(Distances[i] < float.MaxValue)
                    continue;

                HitsCount = i;
                break;
            }

            CastState = State.Draw;
        }

        public override void Draw()
        {
            DrawTick++;

            IMyCamera camera = MyAPIGateway.Session.Camera;
            MatrixD camMatrix = camera.WorldMatrix;
            Vector3D camPos = camMatrix.Translation;

            // framing always rendered
            {
                const BlendTypeEnum BlendType = BlendTypeEnum.PostPP;

                DrawDirectionalFace? faces = null;
                DrawDirectionalLine? parallel = null;
                DrawLine? start = new DrawLine()
                {
                    Material = Constants.Mat_Laser,
                    Color = Color.White,
                    Thick = 0.001f,
                    Blend = BlendType,
                };
                DrawLine? end = null;

                PreviewFrustum.Matrix = camera.ViewMatrix * Projection;
                Utils.DrawFrustum(PreviewFrustum, faces, parallel, start, end);
            }

            if(CastState != State.Draw)
                return;

            {
                Color color = HitsCount > 0 ? Color.Yellow : Color.Gray;

                const BlendTypeEnum BlendType = BlendTypeEnum.PostPP;

                DrawDirectionalFace? faces = new DrawDirectionalFace()
                {
                    Material = Constants.Mat_Gradient,
                    FlipUV = true,
                    Color = color * 0.5f,
                    Blend = BlendType,
                };

                DrawDirectionalLine? parallel = new DrawDirectionalLine()
                {
                    Material = Constants.Mat_LaserGradient,
                    FlipDirection = true,
                    Color = color,
                    Thick = 0.05f,
                    Blend = BlendType,
                };

                DrawLine? start = null;

                DrawLine? end = new DrawLine()
                {
                    Material = Constants.Mat_Laser,
                    Color = color,
                    Thick = 0.05f,
                    Blend = BlendType,
                };

                Utils.DrawFrustum(SnapFrustum, faces, parallel, start, end);
            }

            if(HitsCount == 0)
                return;

            Vector4 colorDark = new Color(15, 25, 40).ToVector4();
            Vector4 colorMid = new Color(15, 75, 25).ToVector4();
            Vector4 colorBright = Color.Red.ToVector4();

            const float DepthRatio = 0.01f;

            // for quad
            //const float Size = 0.03f;
            //const float Alpha = 0.4f;

            // for point
            const float Size = 0.009f;
            const float Alpha = 0.8f;

            float sizeMul = Size * (float)Math.Tan(FOV * 0.5) * DepthRatio;

            Vector3D snapOrigin = SnapCamera.Translation;

            if(DrawTick % 15 == 0) // no need to sort frequently
            {
                for(int i = 0; i < HitsCount; i++)
                {
                    Hit hit = Hits[i];
                    Vector3D pos = snapOrigin + hit.DirScaled;
                    Distances[i] = Vector3.DistanceSquared(camPos, pos);
                }

                Array.Sort(Distances, Hits, 0, HitsCount);
            }

            // sorting above is closest to furthest, iterate backwards to render closest last (and on top)
            for(int i = HitsCount - 1; i >= 0; i--)
            {
                Hit hit = Hits[i];

                Vector3 pos = snapOrigin + hit.DirScaled;
                Vector3 closePos = camPos + (pos - camPos) * DepthRatio;

                float size = hit.DirScaled.Length() * sizeMul;

                Vector3 normalSnapshotSpace = Vector3D.TransformNormal(hit.Normal, ref SnapCameraInv);

                #region Pixel color
                Vector4 color;
                color.X = (normalSnapshotSpace.X + 1) * 0.5f;
                color.Y = (normalSnapshotSpace.Y + 1) * 0.5f;
                color.Z = (normalSnapshotSpace.Z + 1) * 0.5f;

                // higher contrast
                //color.X *= color.X;
                //color.Y *= color.Y;
                //color.Z *= color.Z;

                color.W = Alpha;
                // premultiplied alpha
                color.X *= color.W;
                color.Y *= color.W;
                color.Z *= color.W;
                #endregion

                MyTransparentGeometry.AddPointBillboard(Constants.Mat_Dot2, color, closePos, size, 0, blendType: BlendTypeEnum.PostPP);

                //Vector3D faceDirection = hit.Normal;
                //Vector3D faceDirection = Vector3D.Normalize(camera.Position - pos); // TODO optimize?

                //Vector3D suggestedUp = (Math.Abs(faceDirection.Y) >= 0.99 ? Vector3D.Forward : Vector3D.Up);

                //// MatrixD.CreateFromDir() with less normalize
                //Vector3D up = Vector3D.Cross(Vector3D.Cross(faceDirection, suggestedUp), faceDirection);
                //Vector3D back = -faceDirection;
                //Vector3D right = Vector3D.Normalize(Vector3D.Cross(up, back));
                //up = Vector3D.Cross(back, right);

                //MyQuadD quad;
                //Vector3D rightScaled = right * size;
                //Vector3D upScaled = up * size;
                //quad.Point0 = closePos + upScaled - rightScaled;
                //quad.Point1 = closePos + upScaled + rightScaled;
                //quad.Point2 = closePos - upScaled + rightScaled;
                //quad.Point3 = closePos - upScaled - rightScaled;

                //MyTransparentGeometry.AddQuad(Constants.Mat_Dot2, ref quad, color, ref snapOrigin, blendType: BlendTypeEnum.PostPP);
            }
        }
    }
}
