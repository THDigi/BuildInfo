using System.Collections.Generic;
using VRageMath;

namespace Digi.BuildInfo.Features.LeakInfo
{
    /// <summary>
    /// Used by the pathfinding algorithm to sort crumbs by cost... and stuff.
    /// </summary>
    public class BreadcrumbHeap
    {
        public class Crumb
        {
            public Vector3I Position;
            public int Cost;
            public int PathCost;
            public Crumb Next;
            public Crumb NextListElem;
        }

        public Crumb ListHead;

        // since crumbs need to be classes and hard to tell when they're gotten rid of, keep a non-remove pool of them.
        int PoolIndex = 0;
        readonly List<Crumb> LinearPool = new List<Crumb>();

        public void Add(Vector3I position, int cost = 0, int pathCost = 0, Crumb next = null)
        {
            Crumb item = null;
            if(LinearPool.Count <= PoolIndex)
            {
                item = new Crumb();
                LinearPool.Add(item);
            }
            else
            {
                item = LinearPool[PoolIndex];
            }
            PoolIndex++;

            item.Position = position;
            item.Cost = cost;
            item.PathCost = pathCost;
            item.Next = next;
            item.NextListElem = null;

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
                Crumb pointer = ListHead;

                while(pointer.NextListElem != null && pointer.NextListElem.Cost < item.Cost)
                {
                    pointer = pointer.NextListElem;
                }

                item.NextListElem = pointer.NextListElem;
                pointer.NextListElem = item;
            }
        }

        //public Crumb GetFirst()
        //{
        //    Crumb result = ListHead;
        //    ListHead = ListHead.NextListElem;
        //    return result;
        //}

        public void Clear()
        {
            ListHead = null;
            PoolIndex = 0;
        }

        public void ClearPool()
        {
            Clear();
            LinearPool.Clear();
            LinearPool.Capacity = 0;
        }
    }
}
