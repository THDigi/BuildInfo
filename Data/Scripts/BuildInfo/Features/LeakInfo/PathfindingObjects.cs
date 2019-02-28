using System.Collections.Generic;
using VRageMath;

namespace Digi.BuildInfo.Features.LeakInfo
{
    /// <summary>
    /// Used by the pathfinding algorithm to... I'm not sure xD
    /// </summary>
    public class MinHeap
    {
        private Breadcrumb ListHead;

        public bool HasNext()
        {
            return ListHead != null;
        }

        public void Add(Breadcrumb item)
        {
            if(ListHead == null)
            {
                ListHead = item;
            }
            else if(ListHead.Next == null && item.Cost <= ListHead.Cost)
            {
                item.NextListElem = ListHead;
                ListHead = item;
            }
            else
            {
                var pointer = ListHead;

                while(pointer.NextListElem != null && pointer.NextListElem.Cost < item.Cost)
                {
                    pointer = pointer.NextListElem;
                }

                item.NextListElem = pointer.NextListElem;
                pointer.NextListElem = item;
            }
        }

        public Breadcrumb GetFirst()
        {
            var result = ListHead;
            ListHead = ListHead.NextListElem;
            return result;
        }

        public void Clear()
        {
            ListHead = null;
        }
    }

    /// <summary>
    /// Used by the pathfinding algorithm to keep track of checked path cost and route.
    /// </summary>
    public class Breadcrumb
    {
        public Vector3I Position;
        public int Cost;
        public int PathCost;
        public Breadcrumb Next;
        public Breadcrumb NextListElem;

        public Breadcrumb(Vector3I position)
        {
            Position = position;
        }

        public Breadcrumb(Vector3I position, int cost, int pathCost, Breadcrumb next)
        {
            Position = position;
            Cost = cost;
            PathCost = pathCost;
            Next = next;
        }
    }

    /// <summary>
    /// Identifier for block position combined with direction.
    /// <para>Used for pathfinding to mark a certain move (from position towards direction) as already explored or not due to how the pressurization system works.</para>
    /// </summary>
    public struct MoveId
    {
        public readonly Vector3I Position;
        public readonly Vector3I Direction;
        public readonly int HashCode;

        public MoveId(ref Vector3I position, ref Vector3I direction)
        {
            Position = position;
            Direction = direction;

            unchecked
            {
                HashCode = 17;
                HashCode = HashCode * 31 + position.GetHashCode();
                HashCode = HashCode * 31 + direction.GetHashCode();
            }
        }
    }

    public class MoveIdEqualityComparer : IEqualityComparer<MoveId>
    {
        public bool Equals(MoveId x, MoveId y)
        {
            return x.Position.Equals(y.Position) && x.Direction.Equals(y.Direction);
        }

        public int GetHashCode(MoveId x)
        {
            return x.HashCode;
        }
    }

    public struct LineI
    {
        public readonly Vector3I Start;
        public readonly Vector3I End;

        public LineI(Vector3I start, Vector3I end)
        {
            Start = start;
            End = end;
        }
    }
}
