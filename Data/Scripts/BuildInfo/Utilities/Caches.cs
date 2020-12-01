using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo.Utilities
{
    public class Caches : ModComponent
    {
        public readonly Dictionary<string, IMyModelDummy> Dummies = new Dictionary<string, IMyModelDummy>();
        public readonly HashSet<Vector3I> Vector3ISet = new HashSet<Vector3I>(Vector3I.Comparer);
        public readonly HashSet<MyObjectBuilderType> OBTypeSet = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer);
        public readonly HashSet<MyDefinitionId> DefIdSet = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
        public readonly StringBuilder SB = new StringBuilder(128);
        public readonly MyObjectBuilder_Toolbar EmptyToolbarOB = new MyObjectBuilder_Toolbar();
        public readonly List<Vector3D> Vertices = new List<Vector3D>();
        public readonly Dictionary<int, List<Vector3D>> GeneratedSphereData = new Dictionary<int, List<Vector3D>>();

        public Caches(BuildInfoMod main) : base(main)
        {
        }

        protected override void RegisterComponent()
        {
        }

        protected override void UnregisterComponent()
        {
        }

        public static HashSet<MyObjectBuilderType> GetObTypeSet()
        {
            var set = BuildInfoMod.Instance.Caches.OBTypeSet;
            set.Clear();
            return set;
        }

        public static HashSet<MyDefinitionId> GetDefIdSet()
        {
            var set = BuildInfoMod.Instance.Caches.DefIdSet;
            set.Clear();
            return set;
        }
    }
}
