using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Utils
{
    public class Caches
    {
        public readonly Dictionary<string, IMyModelDummy> Dummies = new Dictionary<string, IMyModelDummy>();
        public readonly HashSet<Vector3I> Vector3ISet = new HashSet<Vector3I>(Vector3I.Comparer);
    }
}
