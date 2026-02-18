using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
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

        public static string ToShortString(this MyDefinitionId defId)
        {
            return defId.ToString().Substring("MyObjectBuilder_".Length);
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

        /// <summary>
        /// HACK workaround for lack of editable blueprint names in production tab.
        /// Replace blueprint "<see cref="MyBlueprintDefinition.DisplayNameText"/>" with this to avoid storing the cache which makes DisplayNameString not function.
        /// NOTE: this does mean changing tooltips mid-game would fail to update blueprints...
        /// </summary>
        public static string GetDisplayName(this MyDefinitionBase def)
        {
            string name = (def.DisplayNameEnum.HasValue ? MyTexts.GetString(def.DisplayNameEnum.Value) : def.DisplayNameString) ?? string.Empty;

            var bp = def as MyBlueprintDefinition;
            if(bp != null)
            {
                MyConsumableItemDefinition consumableResult = null;
                foreach(var result in bp.Results)
                {
                    MyPhysicalItemDefinition itemDef;
                    if(MyDefinitionManager.Static.TryGetPhysicalItemDefinition(result.Id, out itemDef))
                    {
                        consumableResult = itemDef as MyConsumableItemDefinition;
                        if(consumableResult != null)
                            break;
                    }
                }

                object[] args = consumableResult?.StatValuesWithUnits;
                if(args != null && args.Length > 0 && !string.IsNullOrEmpty(name))
                {
                    name = SafeFormatWithStats(name, args);

                    //name = SafeFormatRegex.Replace(name, (m) =>
                    //{
                    //    int id;
                    //    if(!int.TryParse(m.Groups[2].Value, out id))
                    //        return m.Value;
                    //
                    //    string replace = "N/A";
                    //    if(args.Length > id)
                    //        replace = args[id].ToString();
                    //    return replace;
                    //});
                }
            }

            return name;
        }

        //static Regex SafeFormatRegex = new Regex(@"(\{([\d+])\})", RegexOptions.Compiled | RegexOptions.Multiline);

        static string SafeFormatWithStats(string format, object[] args)
        {
            //if(string.IsNullOrEmpty(format) || args == null || args.Length == 0)
            //    return format;

            int highestFormatIndex = GetHighestFormatIndex(format);
            if(highestFormatIndex < 0)
                return format;

            if(highestFormatIndex < args.Length)
                return string.Format(format, args);

            MyLog.Default.Warning("(BuildInfo cloned) SafeFormatWithStats: not enough args for format \"{0}\". HighestIndex={1}, argsLength={2}", format, highestFormatIndex, args.Length);

            object[] array = new object[highestFormatIndex + 1];

            for(int i = 0; i < array.Length; i++)
            {
                array[i] = "N/A";
            }

            for(int j = 0; j < args.Length; j++)
            {
                array[j] = (args[j] ?? "N/A");
            }

            return string.Format(format, array);
        }

        static int GetHighestFormatIndex(string format)
        {
            if(string.IsNullOrEmpty(format))
                return -1;

            int result = -1;
            int len = format.Length;

            for(int i = 0; i < len; i++)
            {
                switch(format[i])
                {
                    case '{':
                    {
                        if(i + 1 < len && format[i + 1] == '{')
                        {
                            i++;
                            break;
                        }
                        int num2 = 0;
                        bool flag = false;
                        for(int j = i + 1; j < len && char.IsDigit(format[j]); j++)
                        {
                            flag = true;
                            num2 = num2 * 10 + (format[j] - 48);
                        }
                        if(flag && num2 > result)
                            result = num2;
                        break;
                    }
                    case '}':
                        if(i + 1 < len && format[i + 1] == '}')
                            i++;
                        break;
                }
            }

            return result;
        }
    }
}
