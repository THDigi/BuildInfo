using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using VRage.Collections;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Utilities
{
    public static class Extensions
    {
        public static bool IsLocal(this MyModContext modContext)
        {
            return modContext != null && !modContext.IsBaseGame && modContext.ModItem.PublishedFileId == 0;
        }

        public static string GetNameAndId(this IMyModContext modContext)
        {
            if(modContext == null)
                return "<Unknown>";

            if(modContext.IsBaseGame)
                return "<BaseGame>";

            return modContext.ModItem.GetNameAndId();
        }

        public static string GetNameAndId(this MyObjectBuilder_Checkpoint.ModItem modItem)
        {
            bool isPublished = modItem.PublishedFileId != 0;
            if(isPublished)
                return $"{modItem.GetName()} ({modItem.PublishedServiceName}:{modItem.PublishedFileId})";
            else
                return $"{modItem.GetName()} (local)";
        }

        public static string GetName(this IMyModContext modContext)
        {
            if(modContext == null)
                return "<Unknown>";

            if(modContext.IsBaseGame)
                return "<BaseGame>";

            string name = modContext.ModName;
            if(string.IsNullOrEmpty(name))
                name = "<Unknown; from PluginLoader?>";

            return name;
        }

        public static string GetName(this MyObjectBuilder_Checkpoint.ModItem modItem)
        {
            bool isPublished = modItem.PublishedFileId != 0;
            string name = (isPublished ? modItem.FriendlyName : modItem.Name);
            if(string.IsNullOrEmpty(name))
                name = "<Unnamed; from PluginLoader?>";

            return name;
        }

        /// <summary>
        /// Primarily used to tell if block has mass, because if this returns false it will contribute no mass to the grid (definition's Mass is non-0 but ignored)
        /// </summary>
        public static bool HasCollider(this MyCubeBlockDefinition def)
        {
            return def.HasPhysics && def.PhysicsOption != MyPhysicsOption.None;
        }

        public static bool ContainsIgnoreCase(this string str, string find)
        {
            return str.IndexOf(find, StringComparison.InvariantCultureIgnoreCase) > -1;
        }

        public static void AddSetReader<T>(this HashSet<T> set, HashSetReader<T> read)
        {
            foreach(T item in read)
            {
                set.Add(item);
            }
        }

        public static void AddArray<T>(this HashSet<T> set, T[] read)
        {
            for(int i = 0; i < read.Length; ++i)
            {
                set.Add(read[i]);
            }
        }

        public static bool ArrayContains<T>(this T[] array, T contains) where T : IEquatable<T>
        {
            for(int i = 0; i < array.Length; ++i)
            {
                if(array[i].Equals(contains))
                    return true;
            }
            return false;
        }

        public static int GetDigitCount(this int value, bool includeSign = true)
        {
            if(value == int.MinValue)
                return (includeSign ? 11 : 10);

            if(value == int.MaxValue)
                return 10;

            int sign = 0;
            if(value < 0)
            {
                value = -value;
                if(includeSign)
                    sign = 1;
            }

            if(value <= 9) return sign + 1;
            if(value <= 99) return sign + 2;
            if(value <= 999) return sign + 3;
            if(value <= 9999) return sign + 4;
            if(value <= 99999) return sign + 5;
            if(value <= 999999) return sign + 6;
            if(value <= 9999999) return sign + 7;
            if(value <= 99999999) return sign + 8;
            if(value <= 999999999) return sign + 9;
            return sign + 10;
        }

        /// <summary>
        /// Hopefully boxless getter, must be fed MyPhysics.HitInfo.
        /// </summary>
        public static IMyEntity GetHitEnt<T>(this T val) where T : IHitInfo => val.HitEntity;

        /// <summary>
        /// Hopefully boxless getter, must be fed MyPhysics.HitInfo.
        /// </summary>
        public static Vector3D GetHitPos<T>(this T val) where T : IHitInfo => val.Position;

        /// <summary>
        /// WARNING: uses one reusable dictionary, do not stack.
        /// </summary>
        public static Dictionary<string, IMyModelDummy> GetDummies(this IMyModel model)
        {
            Dictionary<string, IMyModelDummy> dict = BuildInfoMod.Instance.Caches.Dummies;
            dict.Clear();
            model.GetDummies(dict);
            return dict;
        }
    }
}
