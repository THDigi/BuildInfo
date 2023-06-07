using System;
using System.Collections.Generic;
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

        public static string GetNameAndId(this MyObjectBuilder_Checkpoint.ModItem modItem)
        {
            if(modItem.PublishedFileId == 0)
                return $"{modItem.Name} (local)";
            else
                return $"{modItem.FriendlyName} ({modItem.PublishedServiceName}:{modItem.PublishedFileId})";
        }

        public static bool ContainsIgnoreCase(this string str, string find)
        {
            return str.IndexOf(find, StringComparison.InvariantCultureIgnoreCase) > -1;
        }

        /// <summary>
        /// Get value at key if it exists, otherwise create an instance of TValue, add it to dictionary and return it.
        /// </summary>
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> instancer = null) where TValue : class, new()
        {
            TValue value;
            if(dictionary.TryGetValue(key, out value))
                return value;

            if(instancer != null)
                value = instancer.Invoke();
            else
                value = new TValue();

            dictionary.Add(key, value);
            return value;
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
