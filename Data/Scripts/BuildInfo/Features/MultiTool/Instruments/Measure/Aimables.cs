using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
using static VRageRender.MyBillboard;

namespace Digi.BuildInfo.Features.MultiTool.Instruments.Measure
{
    struct AimedAt
    {
        public Vector3D WorldPosition;
        public Vector3? WorldNormal;
        public IMyEntity AnchorEntity;
        public IMeasureVertex AnchorVertex;
        public MeasurementLine AnchorLine;
    }

    class Aimables
    {
        Color ColorDotCenter = Color.Blue;
        Color ColorDotCorner = Color.SkyBlue;
        Color ColorDotFace = Color.Orange;

        readonly Measure Measure;

        int GetAimablesCooldown = 0;

        public Aimables(Measure host)
        {
            Measure = host;
        }

        readonly List<IHitInfo> TempPhysHits = new List<IHitInfo>(16);

        internal bool GetPos(out AimedAt data)
        {
            data = default(AimedAt);

            bool snapGrid = Measure.MultiTool.ControlSecondary.IsPressed();

            MatrixD camWM = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D camPos = camWM.Translation;
            LineD line = new LineD(camWM.Translation, camWM.Translation + camWM.Forward * Measure.MaxRange, Measure.MaxRange);
            RayD ray = new RayD(camWM.Translation, camWM.Forward);

            // other measurements
            {
                IMeasureVertex closestVertex = null;
                MeasurementLine closestLine = null;
                Vector3D closestLinePos = default(Vector3D);
                double closestDist = double.MaxValue;

                foreach(MeasurementBase m in Measure.Measurements)
                {
                    {
                        var ml = m as MeasurementLine;
                        if(ml != null)
                        {
                            bool aimingAtEnds = false;

                            foreach(IMeasureVertex measurement in ml.GetPoints())
                            {
                                Vector3D worldPoint = measurement.GetWorldPosition();

                                BoundingSphereD sphere = new BoundingSphereD(worldPoint, Measure.AimableRadius);
                                double? lineLen = sphere.Intersects(ray);

                                if(lineLen != null && lineLen.Value <= Measure.MaxRange)
                                {
                                    aimingAtEnds = true;

                                    const double Offset = Measure.AimableRadius * Measure.AimableRadius; // make these seem closer to get higher priority over lines and other stuff
                                    double distSq = Vector3D.DistanceSquared(worldPoint, camPos) - Offset;
                                    if(distSq < closestDist)
                                    {
                                        closestDist = distSq;
                                        closestVertex = measurement;
                                        closestLine = null;
                                    }
                                }

                                Measure.DrawVertex(worldPoint, ml.Color);
                            }

                            if(!aimingAtEnds)
                            {
                                Vector3D a = ml.A.GetWorldPosition();
                                Vector3D b = ml.B.GetWorldPosition();
                                Vector3D dirN = (a - b);
                                double len = dirN.Normalize();

                                Vector3D point, ignore;
                                if(Utils.ClosestPointsOnLines(out point, out ignore, a, dirN, ray.Position, ray.Direction))
                                {
                                    // check if point is on the line
                                    double distanceAlongLine = Vector3D.Dot(a - point, dirN);
                                    if(distanceAlongLine > 0 && distanceAlongLine < len)
                                    {
                                        // check if aimed line is close enough to the other line
                                        if(MyUtils.GetPointLineDistance(ref line.From, ref line.To, ref point) <= Measure.AimableRadius)
                                        {
                                            if(snapGrid)
                                            {
                                                double snapLen = Math.Round(distanceAlongLine / Measure.RulerSizeMeters) * Measure.RulerSizeMeters;
                                                point = a - dirN * snapLen;
                                            }

                                            Measure.DrawVertex(point, ml.Color);

                                            double distSq = Vector3D.DistanceSquared(point, camPos);
                                            if(distSq < closestDist)
                                            {
                                                closestDist = distSq;
                                                closestVertex = null;
                                                closestLine = ml;
                                                closestLinePos = point;
                                            }
                                        }
                                    }
                                }
                            }
                            continue;
                        }
                    }
                }

                if(closestLine != null)
                {
                    data.WorldPosition = closestLinePos;
                    data.AnchorLine = closestLine;
                    return true;
                }

                if(closestVertex != null)
                {
                    data.WorldPosition = closestVertex.GetWorldPosition();
                    data.AnchorVertex = closestVertex;
                    return true;
                }
            }

            if(!Measure.MultiTool.ControlSecondary.IsPressed())
            {
                try
                {
                    GetAimablesCooldown = 0;

                    IMyCharacter self = MyAPIGateway.Session.ControlledObject as IMyCharacter;

                    MyAPIGateway.Physics.CastRay(line.From, line.To, TempPhysHits, Measure.RaycastLayer);

                    if(TempPhysHits.Count == 0)
                        return false;

                    IHitInfo hit = null;

                    foreach(var h in TempPhysHits)
                    {
                        if(h.HitEntity == self)
                            continue;

                        hit = h;
                        break;
                    }

                    if(hit == null)
                        return false;

                    data.AnchorEntity = hit.HitEntity;
                    data.WorldPosition = hit.Position;
                    data.WorldNormal = hit.Normal;

                    const float DotSize = 0.1f;
                    const float DotOffset = 0.01f;
                    const float LineLength = 0.3f;
                    const float LineThick = 0.08f;
                    MatrixD matrix = MatrixD.CreateFromDir(hit.Normal);
                    MyTransparentGeometry.AddBillboardOriented(Constants.Mat_LaserDot, Color.SkyBlue, data.WorldPosition + hit.Normal * DotOffset, matrix.Left, matrix.Up,
                        DotSize, DotSize, Vector2.Zero, BlendTypeEnum.PostPP);
                    MyTransparentGeometry.AddLineBillboard(Constants.Mat_LaserGradient, Color.SkyBlue, data.WorldPosition, hit.Normal,
                        LineLength, LineThick, BlendTypeEnum.PostPP);

                    return true;
                }
                finally
                {
                    TempPhysHits.Clear();
                }
            }
            else
            {
                if(--GetAimablesCooldown < 0)
                {
                    GetAimablesCooldown = 6;
                    FindAimableEntities(line);
                }

                if(Objects.Count > 0)
                {
                    bool selected = false;
                    Aimable closestAimable = default(Aimable);
                    double closestDist = double.MaxValue;

                    foreach(Aimable aimable in Objects)
                    {
                        Vector3D worldPoint = aimable.Parent == null ? aimable.Point : Vector3D.Transform(aimable.Point, aimable.Parent.PositionComp.WorldMatrixRef);

                        BoundingSphereD sphere = new BoundingSphereD(worldPoint, Measure.AimableRadius);
                        double? lineLen = sphere.Intersects(ray);

                        if(lineLen != null && lineLen.Value <= Measure.MaxRange)
                        {
                            double distSq = Vector3D.DistanceSquared(worldPoint, camPos);
                            if(distSq < closestDist)
                            {
                                closestDist = distSq;
                                closestAimable = aimable;
                                selected = true;
                            }
                        }

                        Measure.DrawVertex(worldPoint, aimable.Color);
                    }

                    if(selected)
                    {
                        data.AnchorEntity = closestAimable.Parent;
                        data.WorldPosition = closestAimable.Point;
                        data.WorldNormal = closestAimable.Normal;

                        if(closestAimable.Parent != null)
                        {
                            data.WorldPosition = Vector3D.Transform(closestAimable.Point, closestAimable.Parent.PositionComp.WorldMatrixRef);

                            if(closestAimable.Normal.HasValue)
                                data.WorldNormal = Vector3D.TransformNormal(closestAimable.Normal.Value, closestAimable.Parent.PositionComp.WorldMatrixRef);
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        struct Aimable
        {
            public IMyEntity Parent;

            /// <summary>
            /// Local if parent is not null
            /// </summary>
            public Vector3D Point;

            /// <summary>
            /// Local if parent is not null
            /// </summary>
            public Vector3? Normal;

            public Color Color;
        }

        List<Aimable> Objects = new List<Aimable>(64);
        List<MyLineSegmentOverlapResult<MyEntity>> TempHits = new List<MyLineSegmentOverlapResult<MyEntity>>(32);
        List<Vector3I> TempCells = new List<Vector3I>(64);
        Vector3[] TempCorners = new Vector3[8];

        void FindAimableEntities(LineD line)
        {
            Objects.Clear();

            IMyCharacter self = MyAPIGateway.Session.ControlledObject as IMyCharacter;

            try
            {
                TempHits.Clear();
                MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref line, TempHits, MyEntityQueryType.Both);

                foreach(MyLineSegmentOverlapResult<MyEntity> hit in TempHits)
                {
                    MyEntity ent = hit.Element;
                    if(ent == self)
                        continue;

                    IMyCharacter character = ent as IMyCharacter;
                    if(character != null)
                    {
                        Objects.Add(new Aimable()
                        {
                            Color = ColorDotCenter,
                            Parent = character,
                            Point = Vector3D.Transform(character.WorldVolume.Center, character.WorldMatrixInvScaled),
                        });

                        MatrixD head = character.GetHeadMatrix(false, false);

                        Objects.Add(new Aimable()
                        {
                            Color = ColorDotFace,
                            Parent = character,
                            Point = Vector3D.Transform(head.Translation, character.WorldMatrixInvScaled),
                        });

                        continue;
                    }

                    MyCubeGrid grid = ent as MyCubeGrid;
                    if(grid != null)
                    {
                        TempCells.Clear();
                        grid.RayCastCells(line.From, line.To, TempCells);

                        //Vector3D localFrom = Vector3D.Transform(line.From, grid.PositionComp.WorldMatrixInvScaled);
                        //Vector3D localTo = Vector3D.Transform(line.To, grid.PositionComp.WorldMatrixInvScaled);
                        //Vector3D localDir = Vector3D.TransformNormal(line.Direction, grid.PositionComp.WorldMatrixInvScaled);

                        foreach(Vector3I cell in TempCells)
                        {
                            if(!grid.CubeExists(cell))
                                continue;

                            Vector3 cellCenter = cell * grid.GridSize;
                            var bb = new BoundingBox(cellCenter - grid.GridSizeHalfVector, cellCenter + grid.GridSizeHalfVector);

                            Objects.Add(new Aimable()
                            {
                                Color = ColorDotCenter,
                                Parent = grid,
                                Point = cellCenter,
                            });

                            bb.GetCorners(TempCorners);

                            for(int i = 0; i < TempCorners.Length; i++)
                            {
                                Vector3 corner = TempCorners[i];

                                Objects.Add(new Aimable()
                                {
                                    Color = ColorDotCorner,
                                    Parent = grid,
                                    Point = corner,
                                    Normal = Vector3.Normalize(corner - cellCenter),
                                });
                            }

                            foreach(Vector3 dir in Base6Directions.Directions)
                            {
                                Vector3 face = cellCenter + dir * grid.GridSizeHalf;

                                //if(Vector3.Dot(localDir, dir) > 0)
                                //    continue; // skip directions facing the other way

                                Objects.Add(new Aimable()
                                {
                                    Color = ColorDotFace,
                                    Parent = grid,
                                    Point = face,
                                });
                            }

                            break;
                        }

                        continue;
                    }

                    // TODO voxelmaps?
                }
            }
            finally
            {
                TempHits.Clear();
            }
        }
    }
}
