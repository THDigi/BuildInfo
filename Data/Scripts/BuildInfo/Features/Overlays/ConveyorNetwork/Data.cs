using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.ConveyorNetwork
{
    class GridRender
    {
        public readonly IMyCubeGrid Grid;
        public readonly List<RenderLine> Lines = new List<RenderLine>();
        public readonly List<RenderDot> Dots = new List<RenderDot>();
        public readonly List<RenderDirectional> DirectionalLines = new List<RenderDirectional>();
        public readonly List<RenderBox> Boxes = new List<RenderBox>();

        public GridRender(IMyCubeGrid grid)
        {
            Grid = grid;
        }
    }

    [Flags]
    enum RenderFlags : byte
    {
        None = 0,
        Small = (1 << 0),
        Pulse = (1 << 1),
    }

    struct RenderDot
    {
        public Vector3 LocalPos;
        public Color Color; // 32bits
        public RenderFlags Flags;

        // not using readonly+constructor because mod profiler
    }

    struct RenderLine
    {
        public Vector3 LocalFrom;
        public Vector3 LocalTo;
        public float Length;
        public Color Color;
        public RenderFlags Flags;
    }

    struct RenderDirectional
    {
        public Vector3 LocalPos;
        public Base6Directions.Direction Dir;
        public Color Color;
        public RenderFlags Flags;
    }

    struct RenderLink
    {
        public IMyCubeBlock BlockA;
        public IMyCubeBlock BlockB;
        public BData_Base DataA;
        public BData_Base DataB;
        public float Length;
        public Color Color;
        public RenderFlags Flags;
    }

    struct RenderBox
    {
        public Vector3 LocalPos;
        public Color Color;
        public RenderFlags Flags;
    }
}