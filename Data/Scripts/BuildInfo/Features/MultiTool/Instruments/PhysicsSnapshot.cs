using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Library.Utils;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using VRageRender;
using static VRageRender.MyBillboard;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace Digi.BuildInfo.Features.MultiTool.Instruments
{
    public class PhysicsSnapshot : InstrumentBase
    {
        // global billboard limit is 32768, and other things need them too like TextAPI and RichHUD to render their own things.
        readonly Vector2I Resolution = new Vector2I(140, 140);
        static readonly float FOV = MathHelper.ToRadians(50);
        const float NearPlane = 0.1f; // must not be 0
        const float FarPlane = 30f;
        const float AspectRatio = 1f;
        const float NearPlanePreview = 0.1f;

        const bool FastOverMemory = true;

        struct Hit
        {
            /// <summary>
            /// Add shot position to it to get world position
            /// </summary>
            public Vector3 DirScaled;

            /// <summary>
            /// Ratio of <see cref="FarPlane"/> this ray travelled (and default if no hit)
            /// </summary>
            public float LengthRatio;

            /// <summary>
            /// World-space surface normal
            /// </summary>
            public Vector3 Normal;
        }

        /// <summary>
        /// Used by sorting, in both the casting and the rendering!
        /// Also not any particular unit, can be either ratio and meters squared depending on the timing.
        /// </summary>
        float[] Distances;

        /// <summary>
        /// The hit data, gets sorted closest to camera in realtime.
        /// </summary>
        Hit[] Hits;

        /// <summary>
        /// How many entries from <see cref="Hits"/> are valid.
        /// </summary>
        int HitsCount = 0;

        Stopwatch CaptureMeasure = new Stopwatch();

        List<IHitInfo> CastHits = new List<IHitInfo>(32);

        enum Channel { Hit }
        NotificationBucket<Channel> Notify = new NotificationBucket<Channel>();

        //bool FirstCast = true;
        //float FarthestHitRatio;

        enum State { Idle, Compute, Draw }
        State CastState = State.Idle;

        const int HitLayerNotAdmin = CollisionLayers.DefaultCollisionLayer;
        int HitLayer = HitLayerNotAdmin;

        /// <summary>
        /// offset to be in the center of the pixel
        /// </summary>
        readonly Vector4D PixelOffset;

        /// <summary>
        /// to multiply instead of divide
        /// </summary>
        readonly Vector2D ResolutionR;

        readonly MatrixD PreviewProjection;
        readonly BoundingFrustumD PreviewFrustum = new BoundingFrustumD();

        readonly MatrixD SnapProjection;
        readonly BoundingFrustumD SnapFrustum = new BoundingFrustumD();
        MatrixD SnapCamera;
        MatrixD SnapCameraInv;
        MatrixD ProjectionViewInv;
        int SnapLayer;

        // HACK: multiple buffers required to fix persistence of custom billboards.
        // MyBillboard consumes roughly 1408 bits (+64 for its own reference in the list); (1408*140*140*3)/8 = 10,819,200 bytes
        const int BufferLayers = 3;
        const float BufferAliveforMinutes = 3.0f;
        int BufferIndex = 0;
        readonly TemporaryMemory.Container<MyBillboard[][]> Memory;
        readonly DynamicBillboardCollection BillboardSender;

        enum Colors { CameraNormal, WorldNormal, Depth }

        Colors ColorMode = Colors.Depth;

        int DrawTick = 0;

        int PixelAlphaPercent = 80;
        float PixelAlpha = 0.8f;

        const float PixelDepthRatio = 0.01f;
        const float PixelSize = 0.009f;
        readonly MyStringId PixelMaterial = Constants.Mat_Dot2;
        const BlendTypeEnum PixelBlendType = BlendTypeEnum.PostPP;

        readonly float PointSizeMul = PixelSize * (float)Math.Tan(FOV * 0.5) * PixelDepthRatio;

        /// <summary>
        /// Precomputed powers for faster rendering
        /// </summary>
        float[] DepthPowers;
        const int DepthPowersCache = 1000; // how granular the values are cached, too few and you get banding, too many and you're wasting memory
        const float DepthDistancePower = 0.45f; // distance ratio is calculated to this power, to make colors narrower

        readonly Vector3D[] DepthColors =
        {
            new Vector3D(1.0, 1.0, 1.0),
            new Vector3D(1.0, 0.0, 0.0),
            new Vector3D(1.0, 1.0, 0.0),
            new Vector3D(0.0, 1.0, 0.0),
            new Vector3D(0.0, 1.0, 0.5),
            new Vector3D(0.0, 1.0, 1.0),
            new Vector3D(0.0, 0.0, 1.0),
            new Vector3D(0.6, 0.0, 0.5),
            new Vector3D(0.25, 0.25, 0.25),
        };

        public PhysicsSnapshot() : base("Short Range LIDAR", Constants.MatUI_Icon_PhysicsSnapshot)
        {
            PreviewProjection = MatrixD.CreatePerspectiveFieldOfView(FOV, AspectRatio, NearPlanePreview, FarPlane);
            SnapProjection = MatrixD.CreatePerspectiveFieldOfView(FOV, AspectRatio, NearPlane, FarPlane);

            Main.GUIMonitor.OptionsMenuClosed += RefreshDescription;
            RefreshDescription();

            ResolutionR = new Vector2D(1d / Resolution.X, 1d / Resolution.Y);

            // not sure why half isn't correct here...
            PixelOffset = new Vector4D(ResolutionR.X, -ResolutionR.Y, 0, 0);

            if(FastOverMemory)
            {
                Memory = new TemporaryMemory.Container<MyBillboard[][]>(AllocateBillboards, BufferAliveforMinutes);
                BillboardSender = new DynamicBillboardCollection();
            }

            Notify.Configure(Channel.Hit, 2000, FontsHandler.RedSh);
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
            bool creativeTools = Utils.CreativeToolsEnabled;

            var sb = Description.Builder.Clear();

            sb.Clear();

            MultiTool.ControlPrimary.GetBind(sb);
            sb.Append(" snapshot\n(+Ctrl aimed entity only)");
            sb.AppendLine();

            MultiTool.ControlSecondary.GetBind(sb);
            sb.Append(" clear");
            sb.AppendLine();

            MultiTool.ControlSymmetry.GetBind(sb);
            sb.Append(" color mode: ").Append(MyEnum<Colors>.GetName(ColorMode));
            sb.AppendLine();

            sb.Append("Ctrl+Scroll opacity: ").ProportionToPercent(PixelAlpha);
            sb.AppendLine();

            if(creativeTools)
            {
                sb.Append("Shift+Scroll cycle layer\n(creative tools only)");
                sb.AppendLine();

                sb.Append("Layer: ").Append(Hardcoded.PhysicsLayers[HitLayer].Name).Append(" (").Append(HitLayer).Append(")");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine();
            }

            if(CastState == State.Draw)
            {
                if(creativeTools)
                {
                    sb.Append("Snap: ").Append(Hardcoded.PhysicsLayers[SnapLayer].Name).Append(" (").Append(SnapLayer).Append(")");
                    sb.AppendLine();
                }

                sb.Append("Pixels: ").Append(HitsCount).Append(" / Cost: ").Append(Math.Round(CaptureMeasure.Elapsed.TotalMilliseconds, 1)).Append("ms");
                sb.AppendLine();
            }

            Description.UpdateFromBuilder();
        }

        void RemoveRender()
        {
            HitsCount = 0;
            CastState = State.Idle;
        }

        public override void Update(bool inputReadable)
        {
            bool creativeTools = Utils.CreativeToolsEnabled;

            if(!creativeTools)
            {
                HitLayer = HitLayerNotAdmin;
            }

            if(inputReadable)
            {
                if(MultiTool.ControlSymmetry.IsJustPressed())
                {
                    ColorMode++;
                    if(ColorMode > MyEnum<Colors>.Range.Max)
                        ColorMode = 0;

                    RefreshDescription();
                }

                if(MultiTool.ControlPrimary.IsJustPressed())
                {
                    CaptureSnapshot(MyAPIGateway.Input.IsAnyCtrlKeyPressed());
                    RefreshDescription();
                }

                if(MultiTool.ControlSecondary.IsJustPressed())
                {
                    RemoveRender();
                    RefreshDescription();
                }

                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(scroll != 0)
                {
                    if(MyAPIGateway.Input.IsAnyCtrlKeyPressed())
                    {
                        if(scroll > 0)
                            PixelAlphaPercent += 10;
                        else
                            PixelAlphaPercent -= 10;

                        PixelAlphaPercent = MathHelper.Clamp(PixelAlphaPercent, 0, 100);
                        PixelAlpha = PixelAlphaPercent / 100f;

                        RefreshDescription();
                    }
                    else if(creativeTools && MyAPIGateway.Input.IsAnyShiftKeyPressed())
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

        void CaptureSnapshot(bool captureAimed)
        {
            CaptureMeasure.Restart();

            if(CastState == State.Compute)
            {
                MyAPIGateway.Utilities.ShowNotification("Finishing up the previous cast...", 1000, FontsHandler.RedSh);
                return;
            }

            RemoveRender();

            IMyCamera camera = MyAPIGateway.Session.Camera;

            SnapCamera = camera.WorldMatrix;
            SnapCameraInv = camera.ViewMatrix;
            MatrixD viewProjection = SnapCameraInv * SnapProjection;
            SnapFrustum.Matrix = viewProjection;
            ProjectionViewInv = MatrixD.Invert(viewProjection);
            SnapLayer = HitLayer;

            if(Hits == null)
            {
                Hits = new Hit[Resolution.X * Resolution.Y];
                Distances = new float[Hits.Length];
            }

            CastState = State.Compute;
            HitsCount = 0;

            //if(FirstCast)
            //{
            //    FirstCast = false;
            //    MyLog.Default.WriteLine($"{BuildInfoMod.ModName}: Ignore the following \"error\" about physics from parallel threads. This mod does it properly by blocking main thread until they're done.");
            //}

            // measured various parallel.For() ways, divided by 16, individual, etc; all were slower than just running on main thread.
            bool casted = ShootRays(captureAimed); /// mind the <see cref="CastHits"/> list if doing this in parallel again

            CastHits.Clear();

            //using(new DevProfiler(nameof(PostProcess), 2000))
            if(casted)
            {
                PostProcess();
                //MyAPIGateway.Parallel.Start(PostProcess);
            }
            else
            {
                RemoveRender();
            }

            CaptureMeasure.Stop();
        }

        bool ShootRays(bool captureAimed)
        {
            IMyEntity aimedEntity = null;

            if(captureAimed)
            {
                IMyCamera camera = MyAPIGateway.Session.Camera;
                CastHits.Clear();
                MyAPIGateway.Physics.CastRay(camera.Position, camera.Position + camera.WorldMatrix.Forward * FarPlane, CastHits, 0);

                foreach(IHitInfo hit in CastHits)
                {
                    if(hit.HitEntity == MyAPIGateway.Session.Player.Character)
                        continue;

                    aimedEntity = hit.HitEntity;
                    break;
                }

                if(aimedEntity != null)
                {
                    string name = aimedEntity.DisplayName;
                    if(string.IsNullOrEmpty(name))
                        name = aimedEntity.GetType().Name;

                    Notify.Show(Channel.Hit, $"Only hits on {name} (and connected) will be shown", 500, FontsHandler.WhiteSh);
                }
                else
                {
                    Notify.Show(Channel.Hit, "No aimed entity!");

                    captureAimed = false;
                    return false;
                }
            }

            IMyCubeGrid aimedGrid = aimedEntity as IMyCubeGrid;

            //int endIdx = startIdx + count;
            //for(int index = startIdx; index < endIdx; index++)
            //{
            //    // vertical first
            //    int x = index / Resolution.Y;
            //    int y = index % Resolution.Y;

            int index = 0;

            for(int y = 0; y < Resolution.Y; y++)
            {
                for(int x = 0; x < Resolution.X; x++)
                {
                    // convert to -1.0~1.0 scale
                    double altX = 2.0 * x * ResolutionR.X - 1.0;
                    double altY = 1.0 - 2.0 * y * ResolutionR.Y;

                    // screen to world line
#if false
                    Vector4D vec = new Vector4D(altX, altY, 0.0, 1.0) + PixelOffset;

                    Vector4D from, to;
                    Vector4D.Transform(ref vec, ref ProjectionViewInv, out from);
                    vec.Z = 1;
                    Vector4D.Transform(ref vec, ref ProjectionViewInv, out to);
                    from /= from.W;
                    to /= to.W;

                    Vector3D rayFrom = new Vector3D(from);
                    Vector3D rayTo = new Vector3D(to);
#else
                    // same math as above but with less operations where not needed

                    altX += PixelOffset.X;
                    altY += PixelOffset.Y;

                    // missing Z because it's 0; W is always 1 so skipping the multiplication
                    double screenX = altX * ProjectionViewInv.M11 + altY * ProjectionViewInv.M21 + ProjectionViewInv.M41;
                    double screenY = altX * ProjectionViewInv.M12 + altY * ProjectionViewInv.M22 + ProjectionViewInv.M42;
                    double screenZ = altX * ProjectionViewInv.M13 + altY * ProjectionViewInv.M23 + ProjectionViewInv.M43;
                    double screenW = altX * ProjectionViewInv.M14 + altY * ProjectionViewInv.M24 + ProjectionViewInv.M44;
                    double screenWR = 1d / screenW;

                    Vector3D rayFrom = new Vector3D(screenX * screenWR, screenY * screenWR, screenZ * screenWR);

                    // would be multiplied by Z here which is 1, no need to do that op
                    screenX += ProjectionViewInv.M31;
                    screenY += ProjectionViewInv.M32;
                    screenZ += ProjectionViewInv.M33;
                    screenW += ProjectionViewInv.M34;
                    screenWR = 1d / screenW;

                    Vector3D rayTo = new Vector3D(screenX * screenWR, screenY * screenWR, screenZ * screenWR);
#endif

                    Hit storeHit = new Hit()
                    {
                        LengthRatio = float.MaxValue, // sort last to "trim" later
                    };

                    if(captureAimed)
                    {
                        // a bit faster but not as accurate
                        //if(test)
                        //{
                        //    Vector3D dir = (rayTo - rayFrom) / FarPlane;
                        //
                        //    while(true)
                        //    {
                        //        IHitInfo hit;
                        //        if(!MyAPIGateway.Physics.CastRay(rayFrom, rayTo, out hit, HitLayer))
                        //            break;
                        //
                        //        IMyEntity hitEntity = hit.HitEntity;
                        //        if(hitEntity == null)
                        //            break;
                        //
                        //        IMyCubeGrid hitGrid = hitEntity as IMyCubeGrid;
                        //        if(hitEntity == aimedEntity
                        //        || hitEntity.GetTopMostParent() == aimedEntity.GetTopMostParent()
                        //        || (hitGrid != null && aimedGrid != null && (hitGrid == aimedGrid || MyAPIGateway.GridGroups.HasConnection(aimedGrid, hitGrid, GridLinkTypeEnum.Physical))))
                        //        {
                        //            storeHit.DirScaled = hit.Position - SnapCamera.Translation;
                        //            storeHit.LengthRatio = storeHit.DirScaled.Length() / FarPlane; // slow.......
                        //            storeHit.Normal = hit.Normal;
                        //            break;
                        //        }
                        //        else
                        //        {
                        //            rayFrom = hit.Position + dir * 0.001;
                        //
                        //            // rayFrom is now in front of rayTo, stop casting
                        //            if(Vector3.Dot(dir, (rayTo - rayFrom)) <= 0)
                        //                break;
                        //        }
                        //    }
                        //}
                        //else



                        //CastHits.Clear();
                        //MyAPIGateway.Physics.CastRay(rayFrom, rayTo, CastHits, HitLayer);
                        //
                        //foreach(IHitInfo hit in CastHits)
                        //{
                        //    IMyEntity hitEntity = hit.HitEntity;
                        //    if(hitEntity == null)
                        //        continue;
                        //
                        //    IMyCubeGrid hitGrid = hitEntity as IMyCubeGrid;
                        //    if(hitEntity == aimedEntity
                        //    || hitEntity.GetTopMostParent() == aimedEntity.GetTopMostParent()
                        //    || (hitGrid != null && aimedGrid != null && (hitGrid == aimedGrid || MyAPIGateway.GridGroups.HasConnection(aimedGrid, hitGrid, GridLinkTypeEnum.Physical))))
                        //    {
                        //        storeHit.DirScaled = hit.Position - SnapCamera.Translation;
                        //        storeHit.LengthRatio = MathHelper.Clamp(hit.Fraction, 0f, 1f);
                        //        storeHit.Normal = hit.Normal;
                        //        break;
                        //    }
                        //}



                        IHitInfo hit;
                        if(MyAPIGateway.Physics.CastRay(rayFrom, rayTo, out hit, HitLayer))
                        {
                            IMyEntity hitEntity = hit.HitEntity;
                            if(hitEntity != null)
                            {
                                IMyCubeGrid hitGrid = hitEntity as IMyCubeGrid;
                                if(hitEntity == aimedEntity
                                || hitEntity.GetTopMostParent() == aimedEntity.GetTopMostParent()
                                || (hitGrid != null && aimedGrid != null && (hitGrid == aimedGrid || MyAPIGateway.GridGroups.HasConnection(aimedGrid, hitGrid, GridLinkTypeEnum.Physical))))
                                {
                                    storeHit.DirScaled = hit.Position - SnapCamera.Translation;
                                    storeHit.LengthRatio = MathHelper.Clamp(hit.Fraction, 0f, 1f);
                                    storeHit.Normal = hit.Normal;
                                }
                            }
                        }
                    }
                    else
                    {
                        IHitInfo hit;
                        if(MyAPIGateway.Physics.CastRay(rayFrom, rayTo, out hit, HitLayer))
                        {
                            storeHit.DirScaled = hit.Position - SnapCamera.Translation;
                            storeHit.LengthRatio = MathHelper.Clamp(hit.Fraction, 0f, 1f);
                            storeHit.Normal = hit.Normal;
                        }
                    }

                    Hits[index] = storeHit;
                    Distances[index] = storeHit.LengthRatio; // not in meters but doesn't matter in this case

                    index++;
                }
            }

            return true;
        }

        void PostProcess()
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

            //if(HitsCount > 0)
            //    FarthestHitRatio = Hits[HitsCount - 1].LengthRatio;
            //else
            //    FarthestHitRatio = 0;

            CastState = State.Draw;
        }

        public override void Draw()
        {
            DrawTick++;

            //using(new DevProfiler(nameof(RenderSquare), 16))
            RenderSquare();

            if(CastState != State.Draw)
                return;

            //using(new DevProfiler(nameof(RenderSnapshotFrustum), 16))
            RenderSnapshotFrustum();

            if(HitsCount == 0)
                return;

            if(ColorMode == Colors.Depth & DepthPowers == null)
            {
                DepthPowers = new float[DepthPowersCache + 1];
                DepthPowers[0] = 0;

                for(int i = 1; i <= DepthPowersCache; i++)
                {
                    DepthPowers[i] = (float)Math.Pow((double)i / DepthPowersCache, DepthDistancePower);
                    //DepthPowers[i] = (float)Math.Sqrt((double)i / DepthPowersCache);
                }
            }

            //using(new DevProfiler(nameof(RenderPixels), 16))
            RenderPixels();
        }

        void RenderSquare()
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

            PreviewFrustum.Matrix = MyAPIGateway.Session.Camera.ViewMatrix * PreviewProjection;
            Utils.DrawFrustum(PreviewFrustum, faces, parallel, start, end);
        }

        void RenderSnapshotFrustum()
        {
            Color color = HitsCount > 0 ? Color.Yellow : Color.Gray;
            float thick = 0.05f;

            Color parallelLinesColor = color;
            float distSq = (float)Vector3D.DistanceSquared(MyAPIGateway.Session.Camera.Position, SnapCamera.Translation);
            if(distSq < 1)
            {
                float transition = 1f - distSq;

                // transition to lime color if in the perfect viewing spot
                color = Color.Lerp(color, Color.Lime, transition);
                thick = MathHelper.Lerp(thick, 0.2f, transition);

                // fade out the edge lines because they were distracting when near the snap point.
                parallelLinesColor = color * distSq;
            }

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
                Color = parallelLinesColor,
                Thick = thick,
                Blend = BlendType,
            };

            DrawLine? start = null;

            DrawLine? end = new DrawLine()
            {
                Material = Constants.Mat_Laser,
                Color = color,
                Thick = thick,
                Blend = BlendType,
            };

            Utils.DrawFrustum(SnapFrustum, faces, parallel, start, end);
        }

        MyBillboard[][] AllocateBillboards()
        {
            var data = new MyBillboard[BufferLayers][];

            for(int l = 0; l < BufferLayers; l++)
            {
                MyBillboard[] billboards = new MyBillboard[Hits.Length];
                data[l] = billboards;

                for(int b = 0; b < Hits.Length; b++)
                {
                    billboards[b] = new MyBillboard()
                    {
                        Material = PixelMaterial,
                        BlendType = PixelBlendType,
                        LocalType = LocalTypeEnum.Custom,
                        ParentID = uint.MaxValue,
                        CustomViewProjection = -1,
                        UVSize = Vector2.One,
                        UVOffset = Vector2.Zero,
                        Color = Vector4.One,
                        ColorIntensity = 1f,
                        AlphaCutout = 1f,
                        Reflectivity = 0f,
                        SoftParticleDistanceScale = 0.1f,
                        //DistanceSquared = 0f,
                    };
                }
            }

            return data;
        }

        void RenderPixels()
        {
            Vector3D camPos = MyAPIGateway.Session.Camera.Position;
            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;

            BufferIndex = (BufferIndex + 1) % BufferLayers;

            Vector4 colorDark = new Color(15, 25, 40).ToVector4();
            Vector4 colorMid = new Color(15, 75, 25).ToVector4();
            Vector4 colorBright = Color.Red.ToVector4();

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

            int depthColorsLastIndex = DepthColors.Length - 1;

            //Vector3 lightDir = MyVisualScriptLogicProvider.GetSunDirection();
            Vector3 lightDir = Vector3.Normalize(SnapCamera.Up + SnapCamera.Left * 0.5);

            Memory.AllocateOrExtend();
            int billboardIdx = 0;
            MyBillboard[] billboards = Memory.Data[BufferIndex];

            // sorting above is closest to furthest, iterate backwards to render closest last (and on top)
            for(int i = HitsCount - 1; i >= 0; i--)
            {
                Hit hit = Hits[i];

                Vector3D pos = snapOrigin + hit.DirScaled;

                Vector3D closePos = camPos + (pos - camPos) * PixelDepthRatio;
                float size = hit.LengthRatio * FarPlane * PointSizeMul;

                Vector3D sRGB;

                if(ColorMode == Colors.Depth)
                {
                    int powerIdx = (int)Math.Floor(hit.LengthRatio * (DepthPowers.Length - 1));
                    float distanceRatioCurve = DepthPowers[powerIdx];

                    float floatIndex = depthColorsLastIndex * distanceRatioCurve;
                    int depthIdx = (int)Math.Floor(floatIndex);
                    if(depthIdx < depthColorsLastIndex)
                        sRGB = Vector3D.Lerp(DepthColors[depthIdx], DepthColors[depthIdx + 1], floatIndex - depthIdx);
                    else
                        sRGB = DepthColors[depthColorsLastIndex];

                    // some shading to aid with differentiating faces
                    float lightDot = Vector3.Dot(lightDir, hit.Normal);
                    sRGB *= MathHelper.Lerp(0.75f, 1f, Math.Max(lightDot, 0));
                }
                else if(ColorMode == Colors.WorldNormal)
                {
                    Vector3D normal = hit.Normal;

                    sRGB = (normal + 1) * 0.5;
                }
                else //if(ColorMode == Colors.CameraNormal)
                {
                    Vector3D normalSnapshotSpace = Vector3D.TransformNormal(hit.Normal, ref SnapCameraInv);

                    sRGB = (normalSnapshotSpace + 1) * 0.5;
                }

                sRGB *= PixelAlpha; // premultiplied alpha


                //MyTransparentGeometry.AddPointBillboard(PixelMaterial, new Vector4(sRGB, PixelAlpha), closePos, size, 0, blendType: PixelBlendType);


                //Vector3D faceDirection = Vector3D.Normalize(camPos - pos); // TODO optimize?
                //Vector3D suggestedUp = (Math.Abs(faceDirection.Y) >= 0.99 ? Vector3D.Forward : Vector3D.Up);
                //
                //// MatrixD.CreateFromDir() with less normalize
                //Vector3D up = Vector3D.Cross(Vector3D.Cross(faceDirection, suggestedUp), faceDirection);
                //Vector3D back = -faceDirection;
                //Vector3D right = Vector3D.Normalize(Vector3D.Cross(up, back));
                //up = Vector3D.Cross(back, right);
                //Vector3D rightScaled = right * size;
                //Vector3D upScaled = up * size;

                // way faster and barely noticing the difference
                Vector3D rightScaled = camMatrix.Right * size;
                Vector3D upScaled = camMatrix.Up * size;

                if(FastOverMemory)
                {
                    MyBillboard billboard = billboards[billboardIdx];

                    billboard.Position0 = closePos + upScaled - rightScaled;
                    billboard.Position1 = closePos + upScaled + rightScaled;
                    billboard.Position2 = closePos - upScaled + rightScaled;
                    billboard.Position3 = closePos - upScaled - rightScaled;

                    // SRGB->Linear without Pow() from https://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html
                    Vector3 linear = sRGB * (sRGB * (sRGB * 0.305306011 + 0.682171111) + 0.012522878);
                    // MyTransparentGeometry methods already make it linear, the slow way...

                    billboard.Color = new Vector4(linear, PixelAlpha);

                    billboardIdx++; // must be ascending to maintain draw order
                }
                else
                {
                    MyQuadD quad;
                    quad.Point0 = closePos + upScaled - rightScaled;
                    quad.Point1 = closePos + upScaled + rightScaled;
                    quad.Point2 = closePos - upScaled + rightScaled;
                    quad.Point3 = closePos - upScaled - rightScaled;
                    MyTransparentGeometry.AddQuad(PixelMaterial, ref quad, new Vector4(sRGB, PixelAlpha), ref snapOrigin, blendType: PixelBlendType);
                }
            }

            if(FastOverMemory)
            {
                BillboardSender.Count = HitsCount;
                BillboardSender.SourceArray = billboards;
                MyTransparentGeometry.AddBillboards(BillboardSender, false);
                BillboardSender.SourceArray = null;
            }
        }

        /// <summary>
        /// HACK: exists solely because <see cref="MyTransparentGeometry.AddBillboards(IEnumerable{MyBillboard}, bool)"/> does not have a count override
        /// </summary>
        class DynamicBillboardCollection : ICollection<MyBillboard>
        {
            public int Count { get; set; }
            public MyBillboard[] SourceArray;

            public void CopyTo(MyBillboard[] targetArray, int targetStart)
            {
                Array.Copy(SourceArray, 0, targetArray, targetStart, Count);
            }

            #region Ignored stuff
            public bool IsReadOnly
            {
                get
                {
                    throw new Exception("This should not be called, something is wrong");
                }
            }

            public void Add(MyBillboard item)
            {
                throw new Exception("This should not be called, something is wrong");
            }

            public void Clear()
            {
                throw new Exception("This should not be called, something is wrong");
            }

            public bool Contains(MyBillboard item)
            {
                throw new Exception("This should not be called, something is wrong");
            }

            public IEnumerator<MyBillboard> GetEnumerator()
            {
                throw new Exception("This should not be called, something is wrong");
            }

            public bool Remove(MyBillboard item)
            {
                throw new Exception("This should not be called, something is wrong");
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new Exception("This should not be called, something is wrong");
            }
            #endregion
        }
    }
}
