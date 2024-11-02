using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using VRageMath;
using static Digi.BuildInfo.Systems.TextAPI;

namespace Digi.BuildInfo.Features.MultiTool.Instruments.Measure
{
    abstract class MeasurementBase : IDisposable
    {
        public event Action OnDisposed;

        public Color Color { get; protected set; }

        protected readonly Measure Host;

        protected TextPackage Text;

        protected static int ColorIndex;

        protected MeasurementBase(Measure host)
        {
            Host = host;
            Color = Utils.GetTerminalColorHSV(ColorIndex++).HSVtoColor();

            if(Host.Main.TextAPI.WasDetected)
                CreateUI();
        }

        public virtual void Dispose()
        {
            Text?.Dispose();
            OnDisposed?.Invoke();
        }

        public virtual void CreateUI()
        {
            Text = new TextPackage(64, false, Constants.MatUI_Square);
            Text.Scale = Measure.TextScale;
        }

        public virtual void Hide()
        {
            if(Text != null)
            {
                Text.Visible = false;
            }
        }

        public abstract void Draw();
        public abstract void DrawHUD();
    }

    //class MeasurementCircle : MeasurementBase
    //{
    //    public readonly IMeasureVertex A;
    //    public readonly IMeasureVertex B;

    //    public MeasurementCircle(Measure host, IMeasureVertex a, IMeasureVertex b) : base(host)
    //    {
    //    }

    //    public override void Draw()
    //    {
    //    }

    //    public override void DrawHUD()
    //    {
    //    }
    //}

    //class MeasurementSphere : MeasurementBase
    //{
    //    public MeasurementSphere(Measure host, IMeasureVertex center, float radius) : base(host)
    //    {
    //    }

    //    public override void Draw()
    //    {
    //    }

    //    public override void DrawHUD()
    //    {
    //    }
    //}

    class MeasurementAngle : MeasurementBase
    {
        internal readonly IMeasureVertex Common;
        internal readonly IMeasureVertex PointA;
        internal readonly IMeasureVertex PointB;

        public MeasurementAngle(Measure host, IMeasureVertex common, IMeasureVertex pointA, IMeasureVertex pointB) : base(host)
        {
            Common = common;
            PointA = pointA;
            PointB = pointB;

            var otherLine = (common.HostLine == pointA.HostLine ? pointB.HostLine : pointA.HostLine);
            Color = Color.Lerp(common.HostLine.Color, otherLine.Color, 0.5f);
        }

        public override void Draw()
        {
            Host.DrawMeasurementAngle(Common.GetWorldPosition(), PointA.GetWorldPosition(), PointB.GetWorldPosition(), Color, false, Text);
        }

        public override void DrawHUD()
        {
            if(Text != null && Text.TextStringBuilder.Length > 0)
            {
                Text.Draw();
            }
        }
    }

    class MeasurementLine : MeasurementBase
    {
        public readonly IMeasureVertex A;
        public readonly IMeasureVertex B;

        public MeasurementLine(Measure host, IMeasureVertex a, IMeasureVertex b) : base(host)
        {
            A = a;
            B = b;

            A.HostLine = this;
            B.HostLine = this;
        }

        public override void Dispose()
        {
            A.HostLine = null;
            B.HostLine = null;

            A.Dispose();
            B.Dispose();

            base.Dispose();
        }

        public override void Draw()
        {
            Host.DrawMeasurementLine(A.GetWorldPosition(), B.GetWorldPosition(), Color, false, Text);
        }

        public override void DrawHUD()
        {
            if(Text != null && Text.TextStringBuilder.Length > 0)
            {
                Text.Draw();
            }
        }

        public IMeasureVertex GetOther(IMeasureVertex m)
        {
            return (m == A ? B : A);
        }

        public IEnumerable<IMeasureVertex> GetPoints()
        {
            yield return A;
            yield return B;
        }
    }
}
