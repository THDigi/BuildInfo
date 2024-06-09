using System;
using System.Collections.Generic;
using System.Text;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo.Utilities
{
    public class Caches : ModComponent
    {
        // caches/lookups
        public readonly List<MyPhysicalItemDefinition> ItemDefs = new List<MyPhysicalItemDefinition>(128); // vanilla has ~107
        public readonly List<MyCubeBlockDefinition> BlockDefs = new List<MyCubeBlockDefinition>(1024); // vanilla has ~637
        public readonly Dictionary<string, MyTargetingGroupDefinition> TargetGroups = new Dictionary<string, MyTargetingGroupDefinition>(4); // vanilla has 3
        public readonly List<MyTargetingGroupDefinition> OrderedTargetGroups = new List<MyTargetingGroupDefinition>(4);
        public readonly Dictionary<int, List<Vector3>> GeneratedSphereData = new Dictionary<int, List<Vector3>>();

        // re-usables
        public readonly Dictionary<string, IMyModelDummy> Dummies = new Dictionary<string, IMyModelDummy>();
        public readonly HashSet<Vector3I> Vector3ISet = new HashSet<Vector3I>(Vector3I.Comparer);
        public readonly StringBuilder WordWrapTempSB = new StringBuilder(512);
        public readonly StringBuilder StatusTempSB = new StringBuilder(512);
        public readonly MyObjectBuilder_Toolbar EmptyToolbarOB = new MyObjectBuilder_Toolbar();
        public readonly List<Vector3D> Vertices = new List<Vector3D>();
        public readonly Dictionary<string, int> NamedSums = new Dictionary<string, int>();
        public readonly List<IMyPlayer> Players = new List<IMyPlayer>();

        //public readonly Dictionary<MyCubeBlockDefinition, Dictionary<int, List<string>>> MountRestrictions = new Dictionary<MyCubeBlockDefinition, Dictionary<int, List<string>>>();

        private readonly HashSet<MyObjectBuilderType> OBTypeSet = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer);
        private readonly HashSet<MyDefinitionId> DefIdSet = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
        private readonly List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();

        public int LightningMinDamage { get; private set; }
        public int LightningMaxDamage { get; private set; }

        public Caches(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;

            FillDefs();
            CacheTargetGroups();
            //ComputeMountpointProperties();
            CacheLightningStats();
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        void FillDefs()
        {
            ItemDefs.Clear();
            BlockDefs.Clear();

            foreach(MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                {
                    var physDef = def as MyPhysicalItemDefinition;
                    if(physDef != null)
                    {
                        ItemDefs.Add(physDef);
                        continue;
                    }
                }
                {
                    var blockDef = def as MyCubeBlockDefinition;
                    if(blockDef != null)
                    {
                        BlockDefs.Add(blockDef);
                        continue;
                    }
                }
            }
        }

        void CacheTargetGroups()
        {
            MyDefinitionManager.Static.GetTargetingGroupDefinitions(OrderedTargetGroups);

            foreach(MyTargetingGroupDefinition def in OrderedTargetGroups)
            {
                TargetGroups[def.Id.SubtypeName] = def;
            }
        }

#if false
        void ComputeMountpointProperties()
        {
            for(int b1idx = 0; b1idx < BlockDefs.Count; b1idx++)
            {
                MyCubeBlockDefinition b1 = BlockDefs[b1idx];

                if(b1.MountPoints == null || b1.MountPoints.Length == 0)
                    continue;

                var b1dict = MountRestrictions.GetValueOrNew(b1);

                for(int b2idx = 0; b2idx < BlockDefs.Count; b2idx++)
                {
                    MyCubeBlockDefinition b2 = BlockDefs[b2idx];
                    if(b1 == b2)
                        continue; // blocks can't exclude from mounting themselves

                    if(b1.CubeSize != b2.CubeSize)
                        continue;

                    if(b2.MountPoints == null || b2.MountPoints.Length == 0)
                        continue;

                    for(int b1mIndex = 0; b1mIndex < b1.MountPoints.Length; b1mIndex++)
                    {
                        MyCubeBlockDefinition.MountPoint b1m = b1.MountPoints[b1mIndex];
                        if(!b1m.Enabled || (b1m.ExclusionMask == 0 && b1m.PropertiesMask == 0))
                            continue;

                        foreach(var b2m in b2.MountPoints)
                        {
                            if(!b2m.Enabled || (b2m.ExclusionMask == 0 && b2m.PropertiesMask == 0))
                                continue;

                            if(!((b1m.ExclusionMask & b2m.PropertiesMask) == 0 && (b1m.PropertiesMask & b2m.ExclusionMask) == 0))
                            {
                                string name = b2.DisplayNameText;
                                var list = b1dict.GetValueOrNew(b1mIndex);
                                if(!list.Contains(name))
                                    list.Add(name);
                            }
                        }
                    }
                }

                if(b1dict.Count > 0)
                {
                    foreach(var list in b1dict.Values)
                    {
                        list.Sort(StringComparer.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    MountRestrictions.Remove(b1);
                }
            }
        }
#endif

        void CacheLightningStats()
        {
            if(!MyAPIGateway.Session.SessionSettings.WeatherLightingDamage)
            {
                LightningMinDamage = 0;
                LightningMaxDamage = 0;
                return;
            }

            LightningMinDamage = int.MaxValue;
            LightningMaxDamage = int.MinValue;

            foreach(var weatherDef in MyDefinitionManager.Static.GetWeatherDefinitions())
            {
                if(weatherDef.Lightning != null && weatherDef.LightningGridHitIntervalMax > 0 || weatherDef.LightningCharacterHitIntervalMax > 0 || weatherDef.LightningIntervalMax > 0)
                {
                    LightningMinDamage = Math.Min(LightningMinDamage, weatherDef.Lightning.Damage);
                    LightningMaxDamage = Math.Max(LightningMaxDamage, weatherDef.Lightning.Damage);
                }
            }
        }

        public static HashSet<MyObjectBuilderType> GetObTypeSet()
        {
            HashSet<MyObjectBuilderType> set = BuildInfoMod.Instance.Caches.OBTypeSet;
            set.Clear();
            return set;
        }

        public static HashSet<MyDefinitionId> GetDefIdSet()
        {
            HashSet<MyDefinitionId> set = BuildInfoMod.Instance.Caches.DefIdSet;
            set.Clear();
            return set;
        }

        /// <summary>
        /// Re-uses an internal list.
        /// WARNING: do not use multiple times at once, it returns the same list!
        /// You should clear the given list after done using it, otherwise this'll throw errors when called again.
        /// </summary>
        public static List<IMyCubeGrid> GetGrids(IMyCubeGrid mainGrid, GridLinkTypeEnum type)
        {
            List<IMyCubeGrid> grids = BuildInfoMod.Instance.Caches.Grids;
            if(grids.Count > 0)
                Log.Error("WARNING: Potential stacking of Caches.GetGrids()");

            grids.Clear();
            MyAPIGateway.GridGroups.GetGroup(mainGrid, type, grids);
            return grids;
        }

        #region Per-grid gravity checking
        Dictionary<long, float> GravityLengthAtGridCache = new Dictionary<long, float>();

        public override void UpdateAfterSim(int tick)
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
