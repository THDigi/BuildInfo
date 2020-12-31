using System.Collections.Generic;
using System.Text;
using Digi.ComponentLib;
using Sandbox.ModAPI;
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
        public readonly StringBuilder SB = new StringBuilder(128);
        public readonly MyObjectBuilder_Toolbar EmptyToolbarOB = new MyObjectBuilder_Toolbar();
        public readonly List<Vector3D> Vertices = new List<Vector3D>();
        public readonly Dictionary<int, List<Vector3D>> GeneratedSphereData = new Dictionary<int, List<Vector3D>>();

        private readonly HashSet<MyObjectBuilderType> OBTypeSet = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer);
        private readonly HashSet<MyDefinitionId> DefIdSet = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
        private readonly List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();

        public Caches(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;
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

        /// <summary>
        /// Re-uses an internal list.
        /// WARNING: do not use multiple times at once, it returns the same list!
        /// You should clear the given list after done using it.
        /// </summary>
        public static List<IMyCubeGrid> GetGrids(IMyCubeGrid mainGrid, GridLinkTypeEnum type)
        {
            var grids = BuildInfoMod.Instance.Caches.Grids;
            grids.Clear();
            MyAPIGateway.GridGroups.GetGroup(mainGrid, type, grids);
            return grids;
        }

        #region Per-grid gravity checking
        Dictionary<long, float> GravityLengthAtGridCache = new Dictionary<long, float>();

        protected override void UpdateAfterSim(int tick)
        {
            if(tick % 60 * 3 == 0)
                GravityLengthAtGridCache.Clear();
        }

        public float GetGravityLengthAtGrid(IMyCubeGrid grid)
        {
            float length;
            if(!GravityLengthAtGridCache.TryGetValue(grid.EntityId, out length))
            {
                float naturalInterference;
                Vector3 vec = MyAPIGateway.Physics.CalculateNaturalGravityAt(grid.WorldVolume.Center, out naturalInterference);
                length = vec.Length();

                GravityLengthAtGridCache[grid.EntityId] = length;
            }
            return length;
        }
        #endregion
    }
}
