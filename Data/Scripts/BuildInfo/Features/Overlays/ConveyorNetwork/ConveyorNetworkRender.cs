using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using VRageRender;
using static VRageRender.MyBillboard;

namespace Digi.BuildInfo.Features.Overlays.ConveyorNetwork
{
    public class ConveyorNetworkRender
    {
        public const double RangeOutsideShipVolume = 100;

        // TODO: configurable colors, first need some kind of list config setting...
        public static Vector4[] NetworkColors = new Vector4[]
        {
            Color.Yellow.ToVector4().ToLinearRGB(),
            Color.Cyan.ToVector4().ToLinearRGB(),
            new Color(255,190,20).ToVector4().ToLinearRGB(),
            Color.Blue.ToVector4().ToLinearRGB(),
            Color.MediumSpringGreen.ToVector4().ToLinearRGB(),
            Color.SkyBlue.ToVector4().ToLinearRGB(),
            Color.LimeGreen.ToVector4().ToLinearRGB(),
        };

        public static readonly Vector4 ConnectableColor = new Color(255, 0, 255).ToVector4().ToLinearRGB();
        public static readonly Vector4 UnusedPortColor = (new Color(80, 80, 80) * 0.5f).ToVector4().ToLinearRGB();
        public static readonly Vector4 IsolatedColor = new Color(255, 0, 0).ToVector4().ToLinearRGB();
        public static readonly Vector4 BrokenColor = IsolatedColor; // new Color(255, 50, 10).ToVector4().ToLinearRGB();
        public static readonly Vector4 ShadowColor = Color.Black.ToVector4().ToLinearRGB();

        public const float InventoryBoxOpacity = 0.25f; // note that it's on linear space now
        public const float BoxSizeSG = 0.18f;
        public const float BoxSizeLG = 0.6f;
        public const float BoxSpacing = 0.05f;

        readonly MyStringId MaterialLine = MyStringId.GetOrCompute("BuildInfo_Square");
        readonly MyStringId MaterialLineShadow = MyStringId.GetOrCompute("BuildInfo_ShadowedLine");
        readonly MyStringId MaterialDot = MyStringId.GetOrCompute("BuildInfo_Dot");
        readonly MyStringId MaterialDotShadow = MyStringId.GetOrCompute("BuildInfo_ShadowedDot");
        readonly MyStringId MaterialArrow = MyStringId.GetOrCompute("BuildInfo_Arrow");
        readonly MyStringId MaterialArrowShadow = MyStringId.GetOrCompute("BuildInfo_ShadowedArrow");
        readonly MyStringId MaterialSquare = MyStringId.GetOrCompute("BuildInfo_Square");

        const BlendTypeEnum Blend = BlendTypeEnum.PostPP;

        const float DepthRatio = 0.01f;

        // port size not grid size
        const float BaseThickLarge = 0.4f;
        const float BaseThickSmall = 0.15f;

        // these are tweaked based on the textures
        const float LineThickMul = 0.16f;
        const float LineShadowThickMul = 1f;
        const float DotThickMul = 0.75f;
        const float DotShadowThickMul = 2.5f;
        const float ArrowThickMul = LineShadowThickMul * 1.5f;
        const float ArrowShadowThickMul = ArrowThickMul;

        internal readonly Dictionary<IMyCubeGrid, GridRender> RenderGrids = new Dictionary<IMyCubeGrid, GridRender>();
        GridRender[] SortedRenderGrids = null;

        internal readonly List<RenderLink> GridLinks = new List<RenderLink>();
        RenderLink[] SortedGridLinks = null;

        HashSet<IMyCubeGrid> TempClosedGrids = new HashSet<IMyCubeGrid>();
        float[] TempSortKeys = new float[128];
        int SortTask = 0;

        bool FirstDraw = true;

        BillboardHandler BH = new BillboardHandler();

        ConveyorNetworkView Handler;

        float Pulse;

        BoundingFrustumD CameraFrustum = new BoundingFrustumD(MatrixD.Identity);

        public ConveyorNetworkRender(ConveyorNetworkView handler)
        {
            Handler = handler;
        }

        public void Init()
        {
        }

        public void Reset()
        {
            BH.Reset();

            RenderGrids.Clear();
            SortedRenderGrids = null;

            GridLinks.Clear();
            SortedGridLinks = null;

            FirstDraw = true;
        }

        public bool IsValid()
        {
            BoundingSphereD sphere = BoundingSphereD.CreateInvalid();

            try
            {
                TempClosedGrids.Clear();

                foreach(GridRender gridRender in RenderGrids.Values)
                {
                    if(gridRender.Grid.Closed || gridRender.Grid.MarkedForClose)
                    {
                        TempClosedGrids.Add(gridRender.Grid);
                    }
                    else
                    {
                        sphere.Include(gridRender.Grid.WorldVolume);
                    }
                }

                foreach(IMyCubeGrid key in TempClosedGrids)
                {
                    RenderGrids.Remove(key);
                }
            }
            finally
            {
                TempClosedGrids.Clear();
            }

            if(RenderGrids.Count == 0)
                return false;

            Vector3D camPos = MyAPIGateway.Session.Camera.Position;
            double distSq = (sphere.Radius * sphere.Radius) + (RangeOutsideShipVolume * RangeOutsideShipVolume);
            if(Vector3D.DistanceSquared(camPos, sphere.Center) > distSq)
                return false;

            return true;
        }

        public void Draw()
        {
            Pulse = Utils.Pulse(0.5f, 1.5f, freq: 1.2f);

            IMyCamera camera = MyAPIGateway.Session.Camera;

            // for debugging
            //var ProjectionMatrix = MatrixD.CreatePerspectiveFieldOfView(MathHelper.ToRadians(40), camera.ViewportSize.X / camera.ViewportSize.Y, camera.NearPlaneDistance, camera.FarPlaneDistance);
            //CameraFrustum.Matrix = MatrixD.Invert(MyAPIGateway.Session.Player.Character.GetHeadMatrix(false, false)) * ProjectionMatrix;
            //DebugDraw.DrawFrustum(CameraFrustum);

            CameraFrustum.Matrix = camera.ViewMatrix * camera.ProjectionMatrix;

            bool paused = BuildInfoMod.Instance.IsPaused;

            if(FirstDraw)
            {
                FirstDraw = false;
                FirstFrame();
            }

            if(!paused && Handler.Main.Tick % 15 == 0)
            {
                //using(new DevProfiler($"sort objects {SortTask}", 250))
                {
                    SortObjects();
                }
            }

            //using(new DevProfiler("draw layers", 16))
            {
                DrawLayer(isShadow: true);
                DrawLayer(drawLines: true);
                DrawBoxes();
                DrawLayer(drawDots: true);
            }

            //using(new DevProfiler("send to render", 16))
            {
                BH.SendToRender();
            }

            //if(!MyParticlesManager.Paused)
            //{
            //    MyAPIGateway.Utilities.ShowNotification($"buffers: {BH.Billboards.Length}; {BH.BillboardIndex}", 16);
            //
            //    int lines = 0;
            //
            //    foreach(var gr in RenderGrids.Values)
            //        lines += gr.Lines.Count;
            //
            //    MyAPIGateway.Utilities.ShowNotification($"draw lines: {lines} (*2 for shadow)", 16);
            //}
        }

        void FirstFrame()
        {
            int capacity = 0;

            foreach(GridRender gridRender in RenderGrids.Values)
            {
                capacity += gridRender.Lines.Count * 2; // foreground + shadow
                capacity += gridRender.Dots.Count * 2;
                capacity += gridRender.DirectionalLines.Count * 2;
                capacity += gridRender.Boxes.Count * 8; // box faces, no shadow
            }

            capacity += GridLinks.Count * 2; // line + shadow

            if(BH.Billboards.Length < capacity)
            {
                BH.IncreaseBuffersCapacity(capacity);
            }

            for(int i = 0; i < 4; i++)
            {
                SortObjects();
            }
        }

        void SortObjects()
        {
            int largestList = GridLinks.Count;

            foreach(GridRender gridRender in RenderGrids.Values)
            {
                largestList = Math.Max(gridRender.Lines.Count,
                              Math.Max(gridRender.Dots.Count,
                              Math.Max(gridRender.DirectionalLines.Count,
                              Math.Max(gridRender.Boxes.Count, largestList))));
            }

            if(TempSortKeys.Length < largestList)
            {
                TempSortKeys = new float[MathHelper.GetNearestBiggerPowerOfTwo(largestList)];
            }

            Vector3D camPos = MyAPIGateway.Session.Camera.Position;

            foreach(GridRender gridRender in RenderGrids.Values)
            {
                if(CameraFrustum.Contains(gridRender.Grid.WorldAABB) == ContainmentType.Disjoint)
                    continue;

                MatrixD transform = gridRender.Grid.PositionComp.WorldMatrixRef;

                switch(SortTask)
                {
                    case 0:
                    {
                        EnsureArraySameSize(gridRender.Lines, ref gridRender.SortedLines);

                        for(int i = 0; i < gridRender.Lines.Count; i++)
                        {
                            RenderLine line = gridRender.Lines[i];
                            Vector3D from = Vector3D.Transform(line.LocalFrom, transform);
                            Vector3D to = Vector3D.Transform(line.LocalTo, transform);
                            Vector3D midpoint = from + (to - from) * 0.5f;

                            float distToCamSq = (float)Vector3D.DistanceSquared(camPos, midpoint);

                            TempSortKeys[i] = distToCamSq;
                            gridRender.SortedLines[i] = line;
                        }

                        Array.Sort(TempSortKeys, gridRender.SortedLines, 0, gridRender.SortedLines.Length);
                        break;
                    }

                    case 1:
                    {
                        EnsureArraySameSize(gridRender.Dots, ref gridRender.SortedDots);

                        for(int i = 0; i < gridRender.Dots.Count; i++)
                        {
                            RenderDot dot = gridRender.Dots[i];
                            Vector3D pos = Vector3D.Transform(dot.LocalPos, transform);

                            float distToCamSq = (float)Vector3D.DistanceSquared(camPos, pos);

                            TempSortKeys[i] = distToCamSq;
                            gridRender.SortedDots[i] = dot;
                        }

                        Array.Sort(TempSortKeys, gridRender.SortedDots, 0, gridRender.SortedDots.Length);
                        break;
                    }

                    case 2:
                    {
                        EnsureArraySameSize(gridRender.DirectionalLines, ref gridRender.SortedDirLines);

                        for(int i = 0; i < gridRender.DirectionalLines.Count; i++)
                        {
                            RenderDirectional dirLine = gridRender.DirectionalLines[i];
                            Vector3D pos = Vector3D.Transform(dirLine.LocalPos, transform);

                            float distToCamSq = (float)Vector3D.DistanceSquared(camPos, pos);

                            TempSortKeys[i] = distToCamSq;
                            gridRender.SortedDirLines[i] = dirLine;
                        }

                        Array.Sort(TempSortKeys, gridRender.SortedDirLines, 0, gridRender.SortedDirLines.Length);
                        break;
                    }

                    case 3:
                    {
                        EnsureArraySameSize(gridRender.Boxes, ref gridRender.SortedBoxes);

                        for(int i = 0; i < gridRender.Boxes.Count; i++)
                        {
                            RenderBox box = gridRender.Boxes[i];
                            Vector3D pos = Vector3D.Transform(box.LocalPos, transform);

                            float distToCamSq = (float)Vector3D.DistanceSquared(camPos, pos);

                            TempSortKeys[i] = distToCamSq;
                            gridRender.SortedBoxes[i] = box;
                        }

                        Array.Sort(TempSortKeys, gridRender.SortedBoxes, 0, gridRender.SortedBoxes.Length);
                        break;
                    }
                }
            }

            if(SortTask == 2)
            {
                for(int i = GridLinks.Count - 1; i >= 0; i--)
                {
                    RenderLink link = GridLinks[i];
                    if(link.BlockA.MarkedForClose || link.BlockB.MarkedForClose)
                    {
                        GridLinks.RemoveAtFast(i);
                        continue;
                    }
                }

                EnsureArraySameSize(GridLinks, ref SortedGridLinks);

                for(int i = 0; i < GridLinks.Count; i++)
                {
                    RenderLink link = GridLinks[i];

                    Vector3D from = Vector3D.Transform(link.DataA.ConveyorVisCenter, link.BlockA.WorldMatrix);
                    Vector3D to = Vector3D.Transform(link.DataB.ConveyorVisCenter, link.BlockB.WorldMatrix);
                    Vector3D midpoint = from + (to - from) * 0.5f;

                    float distToCamSq = (float)Vector3D.DistanceSquared(camPos, midpoint);

                    TempSortKeys[i] = distToCamSq;
                    SortedGridLinks[i] = link;
                }

                Array.Sort(TempSortKeys, SortedGridLinks, 0, SortedGridLinks.Length);
            }

            if(SortTask == 2)
            {
                EnsureArraySameSize(RenderGrids.Values, ref SortedRenderGrids);

                int idx = 0;
                foreach(GridRender rg in RenderGrids.Values)
                {
                    float distToCamSq = (float)Vector3D.DistanceSquared(camPos, rg.Grid.WorldVolume.Center);

                    TempSortKeys[idx] = distToCamSq;
                    SortedRenderGrids[idx] = rg;
                    idx++;
                }

                Array.Sort(TempSortKeys, SortedRenderGrids, 0, SortedRenderGrids.Length);
            }

            SortTask = (SortTask + 1) % 4;
        }

        static void EnsureArraySameSize<T>(ICollection<T> list, ref T[] array)
        {
            if(array == null || array.Length != list.Count)
                array = new T[list.Count];
        }

        void DrawLayer(bool isShadow = false, bool drawLines = false, bool drawDots = false)
        {
            const double ContainsRadius = BaseThickLarge * LineShadowThickMul;

            Vector3D camPos = MyAPIGateway.Session.Camera.Position;

            if(isShadow)
            {
                drawLines = true;
                drawDots = true;
            }

            // grid-local render objects
            foreach(GridRender gridRender in SortedRenderGrids)
            {
                if(CameraFrustum.Contains(gridRender.Grid.WorldAABB) == ContainmentType.Disjoint)
                    continue;

                MatrixD transform = gridRender.Grid.PositionComp.WorldMatrixRef;

                // reminder that a lot of things are inlined because of mod profiler
                // I would very much like to use methods too :(

                if(drawLines && gridRender.SortedLines != null)
                {
                    MyStringId material = (isShadow ? MaterialLineShadow : MaterialLine);

                    for(int i = gridRender.SortedLines.Length - 1; i >= 0; i--)
                    {
                        RenderLine line = gridRender.SortedLines[i];
                        Vector3D from = Vector3D.Transform(line.LocalFrom, transform);
                        Vector3D to = Vector3D.Transform(line.LocalTo, transform);
                        Vector3D midpoint = from + (to - from) * 0.5f;

                        if(CameraFrustum.Contains(new BoundingSphereD(midpoint, line.Length + ContainsRadius * 2)) == ContainmentType.Disjoint)
                            continue;

                        bool isSmallPort = (line.Flags & RenderFlags.Small) != 0;

                        float thickness = (isSmallPort ? BaseThickSmall : BaseThickLarge)
                                        * (isShadow ? LineShadowThickMul : LineThickMul)
                                        * DepthRatio;

                        if((line.Flags & RenderFlags.Pulse) != 0)
                            thickness *= Pulse;

                        Vector4 color = (isShadow ? ShadowColor : line.Color);

                        Vector3D fromClose = camPos + ((from - camPos) * DepthRatio);
                        Vector3D toClose = camPos + ((to - camPos) * DepthRatio);

                        Vector3D direction = (toClose - fromClose);

                        //DrawLine(material, ref color, ref fromClose, ref direction, 1f, thickness);

                        #region INLINED DrawLine()
                        Vector3D origin = fromClose;
                        const float length = 1f;

                        #region billboard pool
                        if(BH.BillboardIndex >= BH.Billboards.Length)
                        {
                            BH.IncreaseBuffersCapacity(BH.BillboardIndex + 10);
                        }

                        MyBillboard billboard = BH.Billboards[BH.BillboardIndex];
                        if(billboard == null)
                        {
                            billboard = new MyBillboard();
                            BH.Billboards[BH.BillboardIndex] = billboard;
                        }

                        BH.BillboardIndex++;
                        #endregion

                        billboard.Material = material;
                        billboard.UVOffset = Vector2.Zero;
                        billboard.UVSize = Vector2.One;
                        billboard.BlendType = Blend;
                        billboard.Color = color;
                        billboard.ColorIntensity = 1f;

                        //billboard.LocalType = LocalTypeEnum.Line;
                        //billboard.Position0 = origin;
                        //billboard.Position1 = direction;
                        //billboard.Position2 = new Vector3D(length, thickness, 0);

                        MyQuadD quad;
                        MyPolyLineD polyLine;
                        polyLine.LineDirectionNormalized = direction;
                        polyLine.Point0 = origin;
                        polyLine.Point1 = origin + direction * length;
                        polyLine.Thickness = thickness;
                        // TODO: find a faster way without sqrt?
                        MyUtils.GetPolyLineQuad(out quad, ref polyLine, MyAPIGateway.Session.Camera.Position);
                        billboard.LocalType = LocalTypeEnum.Custom;
                        billboard.Position0 = quad.Point0;
                        billboard.Position1 = quad.Point1;
                        billboard.Position2 = quad.Point2;
                        billboard.Position3 = quad.Point3;

                        //Vector3D endPoint = origin + direction * length;
                        //Vector3D dirToCam = Vector3D.Normalize(MyAPIGateway.Session.Camera.Position - origin);
                        //Vector3D offset = MyUtils.GetVector3Scaled(Vector3D.Cross(direction, dirToCam), thickness);
                        //billboard.Position0 = origin - offset;
                        //billboard.Position1 = endPoint - offset;
                        //billboard.Position2 = endPoint + offset;
                        //billboard.Position3 = origin + offset;

                        billboard.ParentID = uint.MaxValue;
                        billboard.CustomViewProjection = -1;

                        billboard.Reflectivity = 0f;
                        billboard.SoftParticleDistanceScale = 0f;
                        //billboard.AlphaCutout = 0f;
                        //billboard.DistanceSquared = 0; // does not seem used by the game
                        #endregion
                    }
                }

                if(drawDots && gridRender.SortedDots != null)
                {
                    MyStringId material = (isShadow ? MaterialDotShadow : MaterialDot);

                    for(int i = gridRender.SortedDots.Length - 1; i >= 0; i--)
                    {
                        RenderDot dot = gridRender.SortedDots[i];
                        Vector3D pos = Vector3D.Transform(dot.LocalPos, transform);

                        if(CameraFrustum.Contains(new BoundingSphereD(pos, ContainsRadius)) == ContainmentType.Disjoint)
                            continue;

                        bool isSmallPort = (dot.Flags & RenderFlags.Small) != 0;

                        float thickness = (isSmallPort ? BaseThickSmall : BaseThickLarge)
                                        * (isShadow ? DotShadowThickMul : DotThickMul)
                                        * DepthRatio;

                        thickness *= DotThickMul;

                        Vector4 color = (isShadow ? ShadowColor : dot.Color);

                        if((dot.Flags & RenderFlags.Pulse) != 0)
                            thickness *= Pulse;

                        Vector3D posClose = camPos + ((pos - camPos) * DepthRatio);

                        #region INLINED DrawDot()
                        #region billboard pool
                        if(BH.BillboardIndex >= BH.Billboards.Length)
                        {
                            BH.IncreaseBuffersCapacity(BH.BillboardIndex + 10);
                        }

                        MyBillboard billboard = BH.Billboards[BH.BillboardIndex];
                        if(billboard == null)
                        {
                            billboard = new MyBillboard();
                            BH.Billboards[BH.BillboardIndex] = billboard;
                        }

                        BH.BillboardIndex++;
                        #endregion

                        billboard.Material = material;
                        billboard.UVOffset = Vector2.Zero;
                        billboard.UVSize = Vector2.One;
                        billboard.BlendType = Blend;
                        billboard.Color = color;
                        billboard.ColorIntensity = 1f;

                        // TODO some better way?
                        billboard.LocalType = LocalTypeEnum.Point;
                        billboard.Position0 = posClose;
                        billboard.Position2 = new Vector3D(thickness, 0, 0);

                        billboard.ParentID = uint.MaxValue;
                        billboard.CustomViewProjection = -1;

                        billboard.Reflectivity = 0f;
                        billboard.SoftParticleDistanceScale = 0f;
                        //billboard.AlphaCutout = 0f;
                        //billboard.DistanceSquared = 0; // does not seem used by the game
                        #endregion
                    }
                }

                if(drawLines && gridRender.SortedDirLines != null)
                {
                    MyStringId material = (isShadow ? MaterialArrowShadow : MaterialArrow);

                    for(int i = gridRender.SortedDirLines.Length - 1; i >= 0; i--)
                    {
                        RenderDirectional directionalLine = gridRender.SortedDirLines[i];
                        Vector3D pos = Vector3D.Transform(directionalLine.LocalPos, transform);

                        if(CameraFrustum.Contains(new BoundingSphereD(pos, ContainsRadius)) == ContainmentType.Disjoint)
                            continue;

                        bool isSmallPort = (directionalLine.Flags & RenderFlags.Small) != 0;

                        float thickness = (isSmallPort ? BaseThickSmall : BaseThickLarge)
                                        * (isShadow ? ArrowShadowThickMul : ArrowThickMul);

                        Vector4 color = (isShadow ? ShadowColor : directionalLine.Color);

                        if((directionalLine.Flags & RenderFlags.Pulse) != 0)
                            thickness *= Pulse;

                        Vector3D direction = transform.GetDirectionVector(directionalLine.Dir);
                        pos -= direction * thickness; // since this is a line, offset by half length so the input position is centered
                        thickness *= DepthRatio;

                        Vector3D posClose = camPos + ((pos - camPos) * DepthRatio);

                        float length = thickness * 2;

                        #region INLINED DrawLine()
                        Vector3D origin = posClose;

                        #region billboard pool
                        if(BH.BillboardIndex >= BH.Billboards.Length)
                        {
                            BH.IncreaseBuffersCapacity(BH.BillboardIndex + 10);
                        }

                        MyBillboard billboard = BH.Billboards[BH.BillboardIndex];
                        if(billboard == null)
                        {
                            billboard = new MyBillboard();
                            BH.Billboards[BH.BillboardIndex] = billboard;
                        }

                        BH.BillboardIndex++;
                        #endregion

                        billboard.Material = material;
                        billboard.UVOffset = Vector2.Zero;
                        billboard.UVSize = Vector2.One;
                        billboard.BlendType = Blend;
                        billboard.Color = color;
                        billboard.ColorIntensity = 1f;

                        MyQuadD quad;
                        MyPolyLineD polyLine;
                        polyLine.LineDirectionNormalized = direction;
                        polyLine.Point0 = origin;
                        polyLine.Point1 = origin + direction * length;
                        polyLine.Thickness = thickness;
                        // TODO: find a faster way without sqrt?
                        MyUtils.GetPolyLineQuad(out quad, ref polyLine, MyAPIGateway.Session.Camera.Position);
                        billboard.LocalType = LocalTypeEnum.Custom;
                        billboard.Position0 = quad.Point0;
                        billboard.Position1 = quad.Point1;
                        billboard.Position2 = quad.Point2;
                        billboard.Position3 = quad.Point3;

                        billboard.ParentID = uint.MaxValue;
                        billboard.CustomViewProjection = -1;

                        billboard.Reflectivity = 0f;
                        billboard.SoftParticleDistanceScale = 0f;
                        //billboard.AlphaCutout = 0f;
                        //billboard.DistanceSquared = 0; // does not seem used by the game
                        #endregion
                    }
                }
            }

            // mechanical blocks connecting grids
            if(drawLines && SortedGridLinks != null)
            {
                MyStringId material = (isShadow ? MaterialLineShadow : MaterialLine);

                for(int i = SortedGridLinks.Length - 1; i >= 0; i--)
                {
                    RenderLink link = SortedGridLinks[i];

                    if(link.BlockA.MarkedForClose || link.BlockB.MarkedForClose)
                        continue;

                    Vector3D from = Vector3D.Transform(link.DataA.ConveyorVisCenter, link.BlockA.WorldMatrix);
                    Vector3D to = Vector3D.Transform(link.DataB.ConveyorVisCenter, link.BlockB.WorldMatrix);
                    Vector3D midpoint = from + (to - from) * 0.5f;

                    if(CameraFrustum.Contains(new BoundingSphereD(midpoint, link.Length + ContainsRadius * 2)) == ContainmentType.Disjoint)
                        continue;

                    Vector3D fromClose = camPos + ((from - camPos) * DepthRatio);
                    Vector3D toClose = camPos + ((to - camPos) * DepthRatio);

                    bool isSmallPort = (link.Flags & RenderFlags.Small) != 0;

                    Vector4 color = (isShadow ? ShadowColor : link.Color);

                    float thickness = (isSmallPort ? BaseThickSmall : BaseThickLarge)
                                    * (isShadow ? LineShadowThickMul : LineThickMul)
                                    * DepthRatio;

                    if((link.Flags & RenderFlags.Pulse) != 0)
                        thickness *= Pulse;

                    Vector3D direction = (toClose - fromClose);

                    //DrawLine(material, ref color, ref fromClose, ref direction, 1f, thickness);

                    #region INLINED DrawLine()
                    Vector3D origin = fromClose;
                    const float length = 1f;

                    #region billboard pool
                    if(BH.BillboardIndex >= BH.Billboards.Length)
                    {
                        BH.IncreaseBuffersCapacity(BH.BillboardIndex + 10);
                    }

                    MyBillboard billboard = BH.Billboards[BH.BillboardIndex];
                    if(billboard == null)
                    {
                        billboard = new MyBillboard();
                        BH.Billboards[BH.BillboardIndex] = billboard;
                    }

                    BH.BillboardIndex++;
                    #endregion

                    billboard.Material = material;
                    billboard.UVOffset = Vector2.Zero;
                    billboard.UVSize = Vector2.One;
                    billboard.BlendType = Blend;
                    billboard.Color = color;
                    billboard.ColorIntensity = 1f;

                    //billboard.LocalType = LocalTypeEnum.Line;
                    //billboard.Position0 = origin;
                    //billboard.Position1 = direction;
                    //billboard.Position2 = new Vector3D(length, thickness, 0);

                    MyQuadD quad;
                    MyPolyLineD polyLine;
                    polyLine.LineDirectionNormalized = direction;
                    polyLine.Point0 = origin;
                    polyLine.Point1 = origin + direction * length;
                    polyLine.Thickness = thickness;
                    // TODO: find a faster way without sqrt?
                    MyUtils.GetPolyLineQuad(out quad, ref polyLine, MyAPIGateway.Session.Camera.Position);
                    billboard.LocalType = LocalTypeEnum.Custom;
                    billboard.Position0 = quad.Point0;
                    billboard.Position1 = quad.Point1;
                    billboard.Position2 = quad.Point2;
                    billboard.Position3 = quad.Point3;

                    //Vector3D endPoint = origin + direction * length;
                    //Vector3D dirToCam = Vector3D.Normalize(MyAPIGateway.Session.Camera.Position - origin);
                    //Vector3D offset = MyUtils.GetVector3Scaled(Vector3D.Cross(direction, dirToCam), thickness);
                    //billboard.Position0 = origin - offset;
                    //billboard.Position1 = endPoint - offset;
                    //billboard.Position2 = endPoint + offset;
                    //billboard.Position3 = origin + offset;

                    billboard.ParentID = uint.MaxValue;
                    billboard.CustomViewProjection = -1;

                    billboard.Reflectivity = 0f;
                    billboard.SoftParticleDistanceScale = 0f;
                    //billboard.AlphaCutout = 0f;
                    //billboard.DistanceSquared = 0; // does not seem used by the game
                    #endregion
                }
            }
        }

        MyQuadD[] TempQuads = new MyQuadD[6];

        void DrawBoxes()
        {
            foreach(GridRender gridRender in SortedRenderGrids)
            {
                if(gridRender.SortedBoxes == null)
                    continue;

                if(CameraFrustum.Contains(gridRender.Grid.WorldAABB) == ContainmentType.Disjoint)
                    continue;

                MatrixD transform = gridRender.Grid.PositionComp.WorldMatrixRef;

                float boxSize = (gridRender.Grid.GridSizeEnum == MyCubeSize.Large ? BoxSizeLG : BoxSizeSG);
                float boxSizeHalf = boxSize * 0.5f;

                MyStringId material = MaterialSquare;

                for(int i = gridRender.SortedBoxes.Length - 1; i >= 0; i--)
                {
                    RenderBox box = gridRender.SortedBoxes[i];
                    MatrixD boxWM = transform;
                    boxWM.Translation = Vector3D.Transform(box.LocalPos, transform);

                    if(CameraFrustum.Contains(new BoundingSphereD(boxWM.Translation, BoxSizeLG)) == ContainmentType.Disjoint)
                        continue;

                    float depthRatio = OverlayDrawInstance.ConvertToAlwaysOnTop(ref boxWM);

                    {
                        MyQuadD quad;
                        int idx = 0;
                        {
                            Vector3D faceCenter = boxWM.Translation + boxWM.Up * boxSizeHalf;
                            Vector3D a = boxWM.Left * boxSizeHalf;
                            Vector3D b = boxWM.Forward * boxSizeHalf;
                            quad.Point0 = faceCenter + b + a;
                            quad.Point1 = faceCenter + b - a;
                            quad.Point2 = faceCenter - b - a;
                            quad.Point3 = faceCenter - b + a;
                            TempQuads[idx++] = quad;

                            Vector3D mirrorOffset = boxWM.Down * boxSizeHalf * 2;
                            quad.Point0 += mirrorOffset;
                            quad.Point1 += mirrorOffset;
                            quad.Point2 += mirrorOffset;
                            quad.Point3 += mirrorOffset;
                            TempQuads[idx++] = quad;
                        }
                        {
                            Vector3D faceCenter = boxWM.Translation + boxWM.Left * boxSizeHalf;
                            Vector3D a = boxWM.Up * boxSizeHalf;
                            Vector3D b = boxWM.Forward * boxSizeHalf;
                            quad.Point0 = faceCenter + b + a;
                            quad.Point1 = faceCenter + b - a;
                            quad.Point2 = faceCenter - b - a;
                            quad.Point3 = faceCenter - b + a;
                            TempQuads[idx++] = quad;

                            Vector3D mirrorOffset = boxWM.Right * boxSizeHalf * 2;
                            quad.Point0 += mirrorOffset;
                            quad.Point1 += mirrorOffset;
                            quad.Point2 += mirrorOffset;
                            quad.Point3 += mirrorOffset;
                            TempQuads[idx++] = quad;
                        }
                        {
                            Vector3D faceCenter = boxWM.Translation + boxWM.Forward * boxSizeHalf;
                            Vector3D a = boxWM.Up * boxSizeHalf;
                            Vector3D b = boxWM.Left * boxSizeHalf;
                            quad.Point0 = faceCenter + b + a;
                            quad.Point1 = faceCenter + b - a;
                            quad.Point2 = faceCenter - b - a;
                            quad.Point3 = faceCenter - b + a;
                            TempQuads[idx++] = quad;

                            Vector3D mirrorOffset = boxWM.Backward * boxSizeHalf * 2;
                            quad.Point0 += mirrorOffset;
                            quad.Point1 += mirrorOffset;
                            quad.Point2 += mirrorOffset;
                            quad.Point3 += mirrorOffset;
                            TempQuads[idx++] = quad;
                        }
                    }

                    for(int qi = 0; qi < TempQuads.Length; qi++)
                    {
                        MyQuadD quad = TempQuads[qi];

                        #region INLINED DrawQuad()
                        #region billboard pool
                        if(BH.BillboardIndex >= BH.Billboards.Length)
                        {
                            BH.IncreaseBuffersCapacity(BH.BillboardIndex + 10);
                        }

                        MyBillboard billboard = BH.Billboards[BH.BillboardIndex];
                        if(billboard == null)
                        {
                            billboard = new MyBillboard();
                            BH.Billboards[BH.BillboardIndex] = billboard;
                        }

                        BH.BillboardIndex++;
                        #endregion

                        billboard.Material = material;
                        billboard.UVOffset = Vector2.Zero;
                        billboard.UVSize = Vector2.One;
                        billboard.BlendType = Blend;
                        billboard.Color = box.Color;
                        billboard.ColorIntensity = 1f;

                        billboard.LocalType = LocalTypeEnum.Custom;
                        billboard.Position0 = quad.Point0;
                        billboard.Position1 = quad.Point1;
                        billboard.Position2 = quad.Point2;
                        billboard.Position3 = quad.Point3;

                        billboard.ParentID = uint.MaxValue;
                        billboard.CustomViewProjection = -1;

                        billboard.Reflectivity = 0f;
                        billboard.SoftParticleDistanceScale = 0f;
                        //billboard.AlphaCutout = 0f;
                        //billboard.DistanceSquared = 0; // does not seem used by the game
                        #endregion
                    }
                }
            }
        }

        class BillboardHandler
        {
            const int InitialSize = 128;

            public int BillboardIndex = 0;
            public MyBillboard[] Billboards;

            int BufferIndex;
            MyBillboard[][] Buffers = new MyBillboard[3][]; // HACK: tripple-buffered pool to avoid shenanigans with billboards being used for longer than expected
            List<MyBillboard> TempSendBillboards = new List<MyBillboard>(InitialSize);

            public BillboardHandler()
            {
                for(int i = 0; i < Buffers.Length; i++)
                    Buffers[i] = new MyBillboard[InitialSize];

                BufferIndex = 0;
                Billboards = Buffers[BufferIndex];
            }

            public void Reset()
            {
                // nothing to really reset here
            }

            public void IncreaseBuffersCapacity(int newCapacity)
            {
                for(int i = 0; i < Buffers.Length; i++)
                {
                    Utils.EnlargeArray(ref Buffers[i], newCapacity);
                }

                Billboards = Buffers[BufferIndex];
            }

            public void SendToRender()
            {
                if(BillboardIndex <= 0)
                    return;

                try
                {
                    TempSendBillboards.Clear();
                    TempSendBillboards.EnsureCapacity(BillboardIndex);

                    for(int i = 0; i < BillboardIndex; i++)
                    {
                        TempSendBillboards.Add(Billboards[i]);
                    }

                    // while I can just feed Billboards here, it will also add the unused ones.
                    MyTransparentGeometry.AddBillboards(TempSendBillboards, isPersistent: false);
                }
                finally
                {
                    TempSendBillboards.Clear();
                    BillboardIndex = 0;

                    // use next buffer
                    BufferIndex = (BufferIndex + 1) % Buffers.Length;
                    Billboards = Buffers[BufferIndex];
                }
            }
        }
    }
}
