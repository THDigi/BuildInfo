using System;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.MultiTool.Instruments.Measure
{
    interface IMeasureVertex : IDisposable
    {
        MeasurementLine HostLine { get; set; }
        bool IsStatic { get; }

        Vector3D GetWorldPosition();

        event Action OnDisposed;
    }

    class VertexWorld : IMeasureVertex
    {
        public MeasurementLine HostLine { get; set; }
        public bool IsStatic { get; } = true;
        public event Action OnDisposed;

        public readonly Vector3D WorldPosition;
        public readonly Vector3? WorldNormal;

        public VertexWorld(Vector3D worldPosition, Vector3? worldNormal)
        {
            WorldPosition = worldPosition;
            WorldNormal = worldNormal;
        }

        void IDisposable.Dispose()
        {
            OnDisposed?.Invoke();
        }

        public Vector3D GetWorldPosition() => WorldPosition;
    }

    class VertexAnchoredVertex : IMeasureVertex
    {
        public MeasurementLine HostLine { get; set; }
        public bool IsStatic { get; set; } = false;
        public event Action OnDisposed;

        public IMeasureVertex AnchoredVertex { get; private set; }
        Vector3D WorldPosition;

        public VertexAnchoredVertex(IMeasureVertex vertex)
        {
            AnchoredVertex = vertex;
            AnchoredVertex.OnDisposed += AnchorDisposed;
            GetWorldPosition();
        }

        void IDisposable.Dispose()
        {
            OnDisposed?.Invoke();
        }

        void AnchorDisposed()
        {
            if(AnchoredVertex != null)
            {
                AnchoredVertex.OnDisposed -= AnchorDisposed;
                AnchoredVertex = null;
            }

            IsStatic = true;
        }

        public Vector3D GetWorldPosition()
        {
            if(AnchoredVertex != null)
                WorldPosition = AnchoredVertex.GetWorldPosition();

            return WorldPosition;
        }
    }

    class VertexAnchoredLine : IMeasureVertex
    {
        public MeasurementLine HostLine { get; set; }
        public bool IsStatic { get; set; } = false;
        public event Action OnDisposed;

        public MeasurementLine AnchoredLine { get; private set; }
        public float PointAtLength { get; private set; }
        Vector3D WorldPosition;

        public VertexAnchoredLine(MeasurementLine line, float pointAtLength)
        {
            AnchoredLine = line;
            PointAtLength = pointAtLength;

            AnchoredLine.OnDisposed += AnchorDisposed;
            GetWorldPosition();
        }

        void IDisposable.Dispose()
        {
            AnchorDisposed();
            OnDisposed?.Invoke();
        }

        void AnchorDisposed()
        {
            if(AnchoredLine != null)
            {
                AnchoredLine.OnDisposed -= AnchorDisposed;
                AnchoredLine = null;
            }

            IsStatic = true;
        }

        public Vector3D GetWorldPosition()
        {
            if(AnchoredLine != null)
            {
                WorldPosition = AnchoredLine.A.GetWorldPosition();
                if(PointAtLength > 0)
                {
                    Vector3D dir = Vector3D.Normalize(AnchoredLine.B.GetWorldPosition() - WorldPosition);
                    WorldPosition += dir * PointAtLength;
                }
            }

            return WorldPosition;
        }
    }

    // TODO would this be cheaty? used to track someone long range?
    class VertexAnchoredEntity : IMeasureVertex
    {
        public MeasurementLine HostLine { get; set; }
        public bool IsStatic { get; set; } = false;
        public event Action OnDisposed;

        public IMyEntity Entity { get; private set; }

        Vector3D WorldPosition;
        Vector3D LocalPosition;

        public VertexAnchoredEntity(IMyEntity anchor, Vector3D worldPosition)
        {
            Entity = anchor;
            Entity.OnMarkForClose += AnchorDisposed;

            WorldPosition = worldPosition;
            LocalPosition = Vector3D.Transform(worldPosition, Entity.WorldMatrixInvScaled);
        }

        void IDisposable.Dispose()
        {
            AnchorDisposed(null);
            OnDisposed?.Invoke();
        }

        void AnchorDisposed(IMyEntity _)
        {
            if(Entity != null)
            {
                GetWorldPosition(); // cache current position if valid (which will happen with the tool holstered and such)

                Entity.OnMarkForClose -= AnchorDisposed;
                Entity = null;
            }

            IsStatic = true;
        }

        public Vector3D GetWorldPosition()
        {
            if(Entity != null)
            {
                Vector3D pos = Vector3D.Transform(LocalPosition, Entity.PositionComp.WorldMatrixRef);
                if(!Vector3D.IsZero(pos))
                    WorldPosition = pos;
            }

            // maintain last seen entity position
            return WorldPosition;
        }
    }
}
