using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Utilities
{
    public static class Extensions
    {
        /// <summary>
        /// Gets the workshop id from the mod context by iterating the mods to find it.
        /// Returns 0 if not found.
        /// NOTE: workaround for MyModContext not having the actual workshop ID number.
        /// </summary>
        public static ulong GetWorkshopID(this MyModContext modContext)
        {
            foreach(var mod in MyAPIGateway.Session.Mods)
            {
                if(mod.Name == modContext.ModId)
                    return mod.PublishedFileId;
            }

            return 0;
        }

        public static bool ContainsIgnoreCase(this string str, string find)
        {
            return str.IndexOf(find, StringComparison.InvariantCultureIgnoreCase) > -1;
        }

        public static void AddSetReader<T>(this HashSet<T> set, HashSetReader<T> read)
        {
            foreach(var item in read)
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
    }
}
