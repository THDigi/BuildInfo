using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;

namespace Digi.BuildInfo.Utils
{
    public static class Extensions
    {
        /// <summary>
        /// Gets the workshop id from the mod context by iterating the mods to find it.
        /// Returns 0 if not found.
        /// </summary>
        public static ulong GetWorkshopID(this MyModContext modContext)
        {
            // HACK workaround for MyModContext not having the actual workshop ID number.
            foreach(var mod in MyAPIGateway.Session.Mods)
            {
                if(mod.Name == modContext.ModId)
                    return mod.PublishedFileId;
            }

            return 0;
        }

        public static bool ContainsCaseInsensitive(this string str, string find)
        {
            return str.IndexOf(find, 0, StringComparison.InvariantCultureIgnoreCase) > -1;
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
    }
}
