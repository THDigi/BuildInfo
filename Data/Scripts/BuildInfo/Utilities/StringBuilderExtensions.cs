using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features;
using Digi.BuildInfo.Features.Config;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Input;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using static Digi.BuildInfo.Constants;

namespace Digi.BuildInfo.Utilities
{
    public static class StringBuilderExtensions
    {
        // copy of StringBuilderExtensions_2.TrimTrailingWhitespace() since it's not whitelisted in modAPI
        public static StringBuilder TrimEndWhitespace(this StringBuilder sb)
        {
            int idx = sb.Length - 1;

            while(true)
            {
                if(idx < 0)
                    break;

                char c = sb[idx];
                if(c != ' ' && c != '\n' && c != '\r')
                    break;

                idx--;
            }

            sb.Length = idx + 1;
            return sb;
        }

        public static int IndexOf(this StringBuilder sb, char findCharacter, int startIndex = 0, bool ignoreCase = false)
        {
            int length = sb.Length;
            if(length == 0)
                return -1;

            if(startIndex >= length)
                throw new Exception($"startIndex ({startIndex.ToString()}) is out of range ({length.ToString()})!");

            if(ignoreCase)
            {
                findCharacter = char.ToUpperInvariant(findCharacter);

                for(int i = startIndex; i < length; i++)
                {
                    if(char.ToUpperInvariant(sb[i]) == findCharacter)
                        return i;
                }
            }
            else
            {
                for(int i = startIndex; i < length; i++)
                {
                    if(sb[i] == findCharacter)
                        return i;
                }
            }

            return -1;
        }

        public static int IndexOf(this StringBuilder sb, string findString, int startIndex = 0, bool ignoreCase = false)
        {
            int length = sb.Length;
            if(length == 0)
                return -1;

            if(startIndex >= length)
                throw new Exception($"startIndex ({startIndex.ToString()}) is out of range ({length.ToString()})!");

            int findLength = findString.Length;
            int maxSearchLength = (length - findLength) + 1;

            if(ignoreCase)
            {
                char firstChar = char.ToUpperInvariant(findString[0]);

                for(int i = startIndex; i < maxSearchLength; i++)
                {
                    if(char.ToUpperInvariant(sb[i]) == firstChar)
                    {
                        int index = 1;

                        // can safely assume sb index lookup is within limits because of maxSearchLength
                        while((index < findLength) && (char.ToUpperInvariant(sb[i + index]) == char.ToUpperInvariant(findString[index])))
                        {
                            index++;
                        }

                        if(index == findLength)
                            return i;
                    }
                }
            }
            else
            {
                for(int i = startIndex; i < maxSearchLength; i++)
                {
                    if(sb[i] == findString[0])
                    {
                        int index = 1;
                        while((index < findLength) && (sb[i + index] == findString[index]))
                        {
                            index++;
                        }

                        if(index == findLength)
                            return i;
                    }
                }
            }

            return -1;
        }

        public static bool RemoveLineStartsWith(this StringBuilder sb, string prefix)
        {
            int prefixIndex = sb.IndexOf(prefix);
            if(prefixIndex == -1)
                return false;

            int endIndex = -1;
            if(prefixIndex + prefix.Length < sb.Length)
            {
                endIndex = sb.IndexOf('\n', prefixIndex + prefix.Length);
                // newlines are at the start of the line for prefixes so don't add the trailing newline too
            }

            if(endIndex == -1)
                endIndex = sb.Length;

            sb.Remove(prefixIndex, endIndex - prefixIndex);
            return true;
        }

        public static StringBuilder AppendWordWrapped(this StringBuilder sb, string text, int maxWidthChars)
        {
            int width = 0;
            int lastSpaceIdx = -1;

            for(int charIdx = 0; charIdx < text.Length; charIdx++)
            {
                char c = text[charIdx];

                switch(c)
                {
                    case '\n':
                        width = 0;
                        lastSpaceIdx = -1;
                        break;
                    case ' ':
                        lastSpaceIdx = charIdx;
                        break;
                }

                sb.Append(c);

                if(++width > maxWidthChars)
                {
                    if(lastSpaceIdx > -1)
                    {
                        // go back to last space, replace it with a newline and set loop to go from that point
                        sb.Length -= (charIdx - lastSpaceIdx);
                        sb[sb.Length - 1] = '\n';
                        charIdx = lastSpaceIdx;
                    }
                    else
                    {
                        // no space on this line, split word
                        sb.Append("-\n");
                    }

                    width = 0;
                    lastSpaceIdx = -1;
                }
            }

            return sb;
        }

        public static StringBuilder AppendRGBA(this StringBuilder sb, Color color)
        {
            return sb.Append(color.R).Append(", ").Append(color.G).Append(", ").Append(color.B).Append(", ").Append(color.A);
        }

        public static StringBuilder AppendMaxLength(this StringBuilder s, string text, int maxLength, bool addDots = true, bool noNewLines = true)
        {
            if(text == null)
                return s.Append("(NULL)");

            if(noNewLines)
            {
                int newLine = text.IndexOf('\n');
                if(newLine >= 0)
                    maxLength = Math.Min(maxLength, newLine); // redefine max length to before the first newline character
            }

            if(text.Length > maxLength)
            {
                if(addDots)
                    s.Append(text, 0, maxLength - 1).Append("...");
                else
                    s.Append(text, 0, maxLength);
            }
            else
            {
                s.Append(text);
            }

            return s;
        }

        public static StringBuilder Separator(this StringBuilder s)
        {
            return s.Append(", ");
        }

        public static StringBuilder NewLine(this StringBuilder s)
        {
            return s.Append('\n');
        }

        public static StringBuilder NewCleanLine(this StringBuilder s)
        {
            if(BuildInfoMod.Instance.TextAPI.IsEnabled)
                return s.Append("<reset>\n");
            else
                return s.Append('\n');
        }

        public static StringBuilder Label(this StringBuilder s, string label)
        {
            return s.Append(label).Append(": ");
        }

        public static StringBuilder LabelHardcoded(this StringBuilder s, string label, Color color)
        {
            return s.Append(label).Hardcoded().Color(color).Append(": ");
        }

        public static StringBuilder LabelHardcoded(this StringBuilder s, string label)
        {
            return s.LabelHardcoded(label, BuildInfoMod.Instance.TextGeneration.COLOR_NORMAL);
        }

        public static StringBuilder Hardcoded(this StringBuilder s)
        {
            return s.Color(new Color(255, 200, 100)).Append('*');
        }

        public static StringBuilder BoolFormat(this StringBuilder s, bool b)
        {
            return s.Append(b ? "Yes" : "No");
        }

        public static StringBuilder MoreInfoInHelp(this StringBuilder s, int num)
        {
            return s.Color(BuildInfoMod.Instance.TextGeneration.COLOR_UNIMPORTANT).Append(" ([").Append(num).Append("] @ /bi)");
        }

        public static StringBuilder Color(this StringBuilder s, Color color)
        {
            if(BuildInfoMod.Instance.TextAPI.IsEnabled)
                s.Append("<color=").Append(color.R).Append(',').Append(color.G).Append(',').Append(color.B).Append('>');
            return s;
        }

        public static StringBuilder ColorA(this StringBuilder s, Color color)
        {
            if(BuildInfoMod.Instance.TextAPI.IsEnabled)
                s.Append("<color=").Append(color.R).Append(',').Append(color.G).Append(',').Append(color.B).Append(',').Append(color.A).Append('>');
            return s;
        }

        public static StringBuilder ResetFormatting(this StringBuilder s)
        {
            if(BuildInfoMod.Instance.TextAPI.IsEnabled)
                return s.Append("<reset>");
            else
                return s;
        }

        // Some ResourceSinkGroup are string and some are MyStringHash...
        public static StringBuilder ResourcePriority(this StringBuilder s, string groupName, bool hardcoded = false, bool isSource = false)
        {
            return s.ResourcePriority(MyStringHash.GetOrCompute(groupName), hardcoded, isSource);
        }

        public static StringBuilder ResourcePriority(this StringBuilder s, MyStringHash groupId, bool hardcoded = false, bool isSource = false)
        {
            if(hardcoded)
                s.LabelHardcoded("Priority");
            else
                s.Label("Priority");

            Constants constants = BuildInfoMod.Instance.Constants;
            ResourceGroupData data;

            if(groupId != MyStringHash.NullOrEmpty && constants.ResourceGroupPriority.TryGetValue(groupId, out data))
            {
                s.Append(data.Priority).Append("/").Append(data.Def.IsSource ? constants.ResourceSourceGroups : constants.ResourceSinkGroups).Append(" (").Append(groupId.String).Append(")");
            }
            else
            {
                s.Append("last (Undefined)");
            }

            return s;
        }

        public static StringBuilder CurrencyFormat(this StringBuilder s, long money)
        {
            // game does this too, so why not.
            if(money > 1000000000000L || money < -1000000000000L)
            {
                money /= 1000000000000L;
                return s.Append(money.ToString("N0")).Append(" T ").Append(Constants.CurrencySuffix);
            }

            return s.Append(money.ToString("N0")).Append(" ").Append(Constants.CurrencySuffix);
        }

        private static bool IsValid(StringBuilder s, float f, string suffix = "", string prefix = "")
        {
            if(float.IsInfinity(f))
            {
                s.Append(prefix).Append("Inf.").Append(suffix);
                return false;
            }

            if(float.IsNaN(f))
            {
                s.Append(prefix).Append("NaN").Append(suffix);
                return false;
            }

            return true;
        }

        public static StringBuilder ForceFormat(this StringBuilder s, float N)
        {
            if(!IsValid(s, N, " N"))
                return s;

            if(N >= 300000000000000000000000000000000000000f)
                return s.Append("Infinite N"); // close enough for infinite

            if(N >= 1000000000000000000)
                return s.Append(N.ToString("E2")).Append(" N"); // scientific notation

            if(N >= 1000000000000000)
                return s.Number(N / 1000000000000000).Append(" PN");

            if(N >= 1000000000000)
                return s.Number(N / 1000000000000).Append(" TN");

            if(N >= 1000000000)
                return s.Number(N / 1000000000).Append(" GN");

            if(N >= 1000000)
                return s.Number(N / 1000000).Append(" MN");

            if(N >= 1000)
                return s.Number(N / 1000).Append(" kN");

            return s.Number(N).Append(" N");
        }

        public static StringBuilder RotationSpeed(this StringBuilder s, float radPerSecond, int digits = 2)
        {
            if(!IsValid(s, radPerSecond, "°/s"))
                return s;

            return s.Append(Math.Round(MathHelper.ToDegrees(radPerSecond), digits)).Append(" °/s");
        }

        public static StringBuilder TorqueFormat(this StringBuilder s, float N)
        {
            if(!IsValid(s, N, " N-m"))
                return s;

            if(N >= 300000000000000000000000000000000000000f)
                return s.Append("Infinite N-m"); // close enough for infinite

            if(N >= 1000000000000000000)
                return s.Append(N.ToString("E2")).Append(" N-m"); // scientific notation

            if(N >= 1000000000000000)
                return s.Number(N / 1000000000000000).Append(" PN-m");

            if(N >= 1000000000000)
                return s.Number(N / 1000000000000).Append(" TN-m");

            if(N >= 1000000000)
                return s.Number(N / 1000000000).Append(" GN-m");

            if(N >= 1000000)
                return s.Number(N / 1000000).Append(" MN-m");

            if(N >= 1000)
                return s.Number(N / 1000).Append(" kN-m");

            return s.Number(N).Append(" N-m");
        }

        public static StringBuilder PowerFormat(this StringBuilder s, float MW)
        {
            if(!IsValid(s, MW, " W"))
                return s;

            if(MW >= 1000000000000)
                return s.Append(MW.ToString("E2")).Append(" MW"); // scientific notation

            if(MW >= 1000000000)
                return s.Number(MW / 1000000000).Append(" PW");

            if(MW >= 1000000)
                return s.Number(MW / 1000000).Append(" TW");

            if(MW >= 1000)
                return s.Number(MW / 1000).Append(" GW");

            if(MW >= 1)
                return s.Number(MW).Append(" MW");

            if(MW >= 0.001)
                return s.Number(MW * 1000f).Append(" kW");

            return s.Number(MW * 1000000f).Append(" W");
        }

        public static StringBuilder PowerStorageFormat(this StringBuilder s, float MWh)
        {
            return s.PowerFormat(MWh).Append('h');
        }

        public static StringBuilder DistanceFormat(this StringBuilder s, float m, int digits = -1)
        {
            if(!IsValid(s, m, " m"))
                return s;

            if(digits < 0)
            {
                if(m >= 1000)
                    return s.Number(m / 1000).Append(" km");

                if(m < 10)
                    return s.Number(m).Append(" m");

                return s.Append((int)m).Append(" m");
            }
            else
            {
                if(m >= 1000)
                    return s.RoundedNumber(m / 1000, digits).Append(" km");

                return s.RoundedNumber(m, digits).Append(" m");
            }
        }

        public static StringBuilder DistanceRangeFormat(this StringBuilder s, float m1, float m2)
        {
            bool valid = IsValid(s, m1);

            if(!IsValid(s, m2, prefix: (valid ? "" : "~")))
                valid = false;

            if(!valid)
                return s.Append(" m");

            if(m1 >= 1000)
                return s.Number(m1 / 1000).Append("~").Number(m2 / 1000).Append(" km");

            if(m1 < 10)
                return s.Number(m1).Append("~").Number(m2).Append(" m");

            return s.RoundedNumber(m1, 0).Append("~").RoundedNumber(m2, 0).Append(" m");
        }

        public static StringBuilder MassFormat(this StringBuilder s, float kg)
        {
            if(!IsValid(s, kg, " kg"))
                return s;

            if(kg == 0)
                return s.Append("0 kg");

            if(kg >= 1000000000)
                return s.Number(kg / 1000000000f).Append(" Mt");

            if(kg >= 1000000)
                return s.Number(kg / 1000000f).Append(" kt");

            if(kg >= 10000)
                return s.Number(kg / 1000f).Append(" t");

            if(kg >= 1)
                return s.Number(kg).Append(" kg");

            if(kg >= 0.001f)
                return s.Number(kg * 1000f).Append(" grams");

            if(kg >= 0.000001f)
                return s.Number(kg * 1000000f).Append(" mg");

            //if(kg >= 0.000000001f)
            return s.Number(kg * 1000000000f).Append(" µg");
        }

        public static StringBuilder IntegrityFormat(this StringBuilder s, float integrity)
        {
            if(!IsValid(s, integrity))
                return s;

            if(integrity >= 1000000000000)
                return s.Number(integrity / 1000000000000f).Append(" T");

            if(integrity >= 1000000000)
                return s.Number(integrity / 1000000000f).Append(" G");

            if(integrity >= 1000000)
                return s.Number(integrity / 1000000f).Append(" M");

            if(integrity >= 1000)
                return s.Number(integrity / 1000f).Append(" k");

            return s.Number(integrity);
        }

        public static StringBuilder VolumeFormat(this StringBuilder s, float l)
        {
            if(!IsValid(s, l, " L"))
                return s;

            if(l == 0)
                return s.Append("0 L");

            if(l >= 1000000000000)
                return s.Number(l / 1000000000000f).Append(" TL");

            if(l >= 1000000000)
                return s.Number(l / 1000000000f).Append(" GL");

            if(l >= 1000000)
                return s.Number(l / 1000000f).Append(" ML");

            if(l >= 1000)
                return s.Number(l / 1000f).Append(" kL");

            if(l >= 1)
                return s.Number(l).Append(" L");

            if(l >= 0.001f)
                return s.Number(l * 1000f).Append(" mL");

            //if(l >= 0.000001f)
            return s.Number(l * 1000000f).Append(" µL");

            //else
            //{
            //    if(l >= 1000000000000)
            //        return s.Number(l / 1000000000000f).Append(" km³");

            //    if(l >= 1000000000)
            //        return s.Number(l / 1000000000f).Append(" hm³");

            //    if(l >= 1000000)
            //        return s.Number(l / 1000000f).Append(" dam³");

            //    if(l >= 1000)
            //        return s.Number(l / 1000f).Append(" m³");

            //    if(l >= 1)
            //        return s.Number(l).Append(" L");

            //    if(l >= 0.001f)
            //        return s.Number(l * 1000f).Append(" cm³");

            //    //if(l >= 0.000001f)
            //    return s.Number(l * 1000000f).Append(" mm³");
            //}
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyInventoryConstraint inputConstraint, MyInventoryConstraint outputConstraint, MyInventoryComponentDefinition invComp)
        {
            HashSet<MyObjectBuilderType> types = Caches.GetObTypeSet();
            types.AddSetReader(inputConstraint.ConstrainedTypes);
            types.AddSetReader(outputConstraint.ConstrainedTypes);

            HashSet<MyDefinitionId> items = Caches.GetDefIdSet();
            items.AddSetReader(inputConstraint.ConstrainedIds);
            items.AddSetReader(outputConstraint.ConstrainedIds);

            // HACK only using input constraint's whitelist status, not sure if output inventory's whitelist is needed
            return s.InventoryFormat(volume, invComp,
                types: types,
                items: items,
                isWhitelist: inputConstraint.IsWhitelist);
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyInventoryConstraint inventoryConstraint, MyInventoryComponentDefinition invComp)
        {
            if(inventoryConstraint == null)
                return s.InventoryFormat(volume, invComp);

            HashSet<MyObjectBuilderType> types = Caches.GetObTypeSet();
            types.AddSetReader(inventoryConstraint.ConstrainedTypes);

            HashSet<MyDefinitionId> items = Caches.GetDefIdSet();
            items.AddSetReader(inventoryConstraint.ConstrainedIds);

            return s.InventoryFormat(volume, invComp,
                types: types,
                items: items,
                isWhitelist: inventoryConstraint.IsWhitelist);
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyObjectBuilderType allowedType, MyInventoryComponentDefinition invComp)
        {
            HashSet<MyObjectBuilderType> types = Caches.GetObTypeSet();
            types.Add(allowedType);

            return s.InventoryFormat(volume, invComp, types: types);
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyDefinitionId[] allowedItems, MyInventoryComponentDefinition invComp)
        {
            HashSet<MyDefinitionId> items = Caches.GetDefIdSet();
            items.AddArray(allowedItems);

            return s.InventoryFormat(volume, invComp, items: items);
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyDefinitionId allowedItem, MyInventoryComponentDefinition invComp)
        {
            HashSet<MyDefinitionId> items = Caches.GetDefIdSet();
            items.Add(allowedItem);

            return s.InventoryFormat(volume, invComp, items: items);
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyInventoryComponentDefinition invComp, HashSet<MyObjectBuilderType> types = null, HashSet<MyDefinitionId> items = null, bool isWhitelist = true)
        {
            if(volume == 0)
            {
                s.Append("0 L");
                return s;
            }

            BuildInfoMod bi = BuildInfoMod.Instance;
            bool allowMultiplier = invComp?.MultiplierEnabled ?? true;
            float blockInvMul = MyAPIGateway.Session.BlocksInventorySizeMultiplier;

            if(allowMultiplier && bi.Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryVolumeMultiplied))
            {
                s.VolumeFormat(volume * 1000 * blockInvMul);

                if(Math.Abs(blockInvMul - 1) > 0.001f)
                    s.Color(bi.TextGeneration.COLOR_UNIMPORTANT).Append(" (x").Append(Math.Round(blockInvMul, 2)).Append(")").ResetFormatting();
            }
            else
            {
                s.VolumeFormat(volume * 1000);

                if(!allowMultiplier && blockInvMul != 1f)
                {
                    s.Color(bi.TextGeneration.COLOR_WARNING).Append(" (ignores multiplier)").ResetFormatting();
                }
            }

            if(bi.Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryExtras))
            {
                if(types == null && items == null)
                    types = bi.Constants.DefaultItemsForMass;

                float minMass = float.MaxValue;
                float maxMass = 0f;

                foreach(MyPhysicalItemDefinition physDef in bi.Caches.ItemDefs)
                {
                    if(!physDef.Public || physDef.Mass <= 0 || physDef.Volume <= 0)
                        continue; // skip hidden and physically impossible items

                    if((types != null && isWhitelist == types.Contains(physDef.Id.TypeId)) || (items != null && isWhitelist == items.Contains(physDef.Id)))
                    {
                        float fillMass = physDef.Mass;
                        if(physDef.HasIntegralAmounts)
                            fillMass *= (int)Math.Floor(volume / physDef.Volume);
                        else
                            fillMass *= (volume / physDef.Volume);

                        minMass = Math.Min(fillMass, minMass);
                        maxMass = Math.Max(fillMass, maxMass);
                    }
                }

                if(minMass != float.MaxValue && maxMass != 0)
                {
                    if(Math.Abs(minMass - maxMass) > 0.00001f)
                        s.Append(", Cargo mass: ").MassFormat(minMass).Append(" to ").MassFormat(maxMass);
                    else
                        s.Append(", Max cargo mass: ").MassFormat(minMass);
                }
            }

            return s;
        }

        public static StringBuilder TimeFormat(this StringBuilder s, float totalSeconds)
        {
            if(!IsValid(s, totalSeconds))
                return s.Append(" seconds");

            if(totalSeconds > (60 * 60 * 24 * 365))
                return s.Append("1 y+");

            TimeSpan span = TimeSpan.FromSeconds(totalSeconds);

            if(span.Days > 7)
            {
                s.Append(span.Days / 7).Append('w');
                s.Append(' ').Append(span.Days % 7).Append('d');
                s.Append(' ').Append(span.Hours).Append('h');
                s.Append(' ').Append(span.Minutes).Append('m');
                return s;
            }

            if(span.Days > 0)
            {
                s.Append(span.Days).Append('d');
                s.Append(' ').Append(span.Hours).Append('h');
                s.Append(' ').Append(span.Minutes).Append('m');
                s.Append(' ').Append(span.Seconds).Append('s');
                return s;
            }

            if(span.Hours > 0)
            {
                s.Append(span.Hours).Append('h');
                s.Append(' ').Append(span.Minutes).Append('m');
                s.Append(' ').Append(span.Seconds).Append('s');
                return s;
            }

            if(span.Minutes > 0)
            {
                s.Append(span.Minutes).Append('m');
                s.Append(' ').RoundedNumber(totalSeconds % 60, 0).Append('s');
                return s;
            }

            s.RoundedNumber(totalSeconds, 1).Append('s');
            return s;
        }

        public static StringBuilder AngleFormat(this StringBuilder s, float radians, int digits = 0)
        {
            if(!IsValid(s, radians))
                return s.Append('°');

            return s.AngleFormatDeg(MathHelper.ToDegrees(radians), digits);
        }

        public static StringBuilder AngleFormatDeg(this StringBuilder s, float degrees, int digits = 0)
        {
            if(!IsValid(s, degrees))
                return s.Append('°');

            return s.Append(Math.Round(degrees, digits)).Append('°');
        }

        public static StringBuilder Size3DFormat(this StringBuilder s, Vector3 vec)
        {
            return s.Number(vec.X).Append('x').Number(vec.Y).Append('x').Number(vec.Z);
        }

        public static StringBuilder SpeedFormat(this StringBuilder s, float metersPerSecond, int digits = 2)
        {
            if(!IsValid(s, metersPerSecond))
                return s.Append(" m/s");

            return s.RoundedNumber(metersPerSecond, digits).Append(" m/s");
        }

        public static StringBuilder AccelerationFormat(this StringBuilder s, float metersPerSecondSquared)
        {
            if(!IsValid(s, metersPerSecondSquared))
                return s.Append(" m/s²");

            return s.SpeedFormat(metersPerSecondSquared).Append('²');
        }

        public static StringBuilder ProportionToPercent(this StringBuilder s, float proportion, int digits = 0)
        {
            if(!IsValid(s, proportion))
                return s.Append('%');

            return s.RoundedNumber(proportion * 100, digits).Append('%');
        }

        public static StringBuilder MultiplierToPercent(this StringBuilder s, float multiplier)
        {
            if(!IsValid(s, multiplier))
                return s.Append('%');

            float ratio = multiplier - 1;
            if(ratio < 0)
                s.Append(Math.Round(ratio * 100));
            else
                s.Append('+').Append(Math.Round(ratio * 100));

            return s.Append('%').Append(" (x").RoundedNumber(multiplier, 2).Append(")");
        }

        public static StringBuilder MultiplierFormat(this StringBuilder s, float mul)
        {
            if(Math.Abs(mul - 1f) > 0.001f)
                s.Append(" (x").Number(mul).Append(")");

            return s;
        }

        public static StringBuilder IdTypeFormat(this StringBuilder s, MyObjectBuilderType type, bool useFriendlyName = true)
        {
            string friendlyName;
            if(useFriendlyName && Constants.TypeToFriendlyName.TryGetValue(type, out friendlyName))
            {
                s.Append(friendlyName);
                return s;
            }

            // fallback to the objectbuilder name without the prefix
            string typeName = type.ToString();
            int index = typeName.IndexOf('_') + 1;

            if(index > -1)
                s.Append(typeName, index, typeName.Length - index);
            else
                s.Append(typeName);
            return s;
        }

        public static StringBuilder IdTypeSubtypeFormat(this StringBuilder s, MyDefinitionId id, bool useFriendlyName = true)
        {
            if(id == MyResourceDistributorComponent.ElectricityId)
                return s.Append("Electricity");

            if(id == MyResourceDistributorComponent.OxygenId)
                return s.Append("Oxygen");

            if(id == MyResourceDistributorComponent.HydrogenId)
                return s.Append("Hydrogen");

            if(id.TypeId == typeof(MyObjectBuilder_GasProperties))
                return s.Append(id.SubtypeName).Append(" gas");

            if(id.TypeId == typeof(MyObjectBuilder_GasContainerObject) || id.TypeId == typeof(MyObjectBuilder_OxygenContainerObject))
                return s.Append(id.SubtypeName).Append(" bottle");

            return s.Append(id.SubtypeName).Append(' ').IdTypeFormat(id.TypeId);
        }

        /// <summary>
        /// Looks up the physical item or gas resource name.
        /// </summary>
        public static StringBuilder ItemName(this StringBuilder s, MyDefinitionId id)
        {
            if(id == MyResourceDistributorComponent.ElectricityId)
                return s.Append("Electricity");

            if(id == MyResourceDistributorComponent.OxygenId)
                return s.Append("Oxygen");

            if(id == MyResourceDistributorComponent.HydrogenId)
                return s.Append("Hydrogen");

            if(id.TypeId == typeof(MyObjectBuilder_GasProperties))
                return s.Append(id.SubtypeName).Append(" gas");

            MyPhysicalItemDefinition itemDef;
            if(MyDefinitionManager.Static.TryGetPhysicalItemDefinition(id, out itemDef))
            {
                return s.Append(itemDef.DisplayNameText);
            }

            return s.Append(id.SubtypeName).Append(' ').IdTypeFormat(id.TypeId);
        }

        public static StringBuilder ModFormat(this StringBuilder s, MyModContext context)
        {
            TextGeneration tg = BuildInfoMod.Instance.TextGeneration;
            s.Color(tg.COLOR_MOD_TITLE).AppendMaxLength(context.ModName, TextGeneration.MOD_NAME_MAX_LENGTH);

            MyObjectBuilder_Checkpoint.ModItem modItem = context.GetModItem();
            if(modItem.Name != null && modItem.PublishedFileId > 0)
                s.Color(tg.COLOR_UNIMPORTANT).Append(" (").Append(modItem.PublishedServiceName).Append(":").Append(modItem.PublishedFileId).Append(")");

            return s;
        }

        /// <summary>
        /// Number with thousands separator and 2 digits after period.
        /// </summary>
        public static StringBuilder Number(this StringBuilder s, float value)
        {
            if(!IsValid(s, value))
                return s;

            return s.Append(value.ToString("###,###,###,###,###,##0.##"));
        }

        public static StringBuilder RoundedNumber(this StringBuilder s, float value, int digits)
        {
            if(!IsValid(s, value))
                return s;

            return s.Append(Math.Round(value, digits).ToString("###,###,###,###,###,##0.##########"));
        }

        public static StringBuilder ShortNumber(this StringBuilder s, float value)
        {
            if(!IsValid(s, value))
                return s;

            if(value >= 1000000)
                return s.RoundedNumber(value / 1000, 0).Append("k");

            if(value >= 1000)
                return s.RoundedNumber(value / 1000, 1).Append("k");

            return s.RoundedNumber(value, 1);
        }

        public static StringBuilder NumberCapped(this StringBuilder s, int value, int maxDigits = 2)
        {
            if(value < 0) throw new Exception("negative values not supported");
            if(maxDigits <= 0) throw new Exception("max digits can't be 0 or lower");
            if(maxDigits > 6) throw new Exception("max digits supported up to 6");

            if(maxDigits == 1 && value > 9) return s.Append("9+");
            if(maxDigits == 2 && value > 99) return s.Append("99+");
            if(maxDigits == 3 && value > 999) return s.Append("999+");
            if(maxDigits == 4 && value > 9999) return s.Append("9999+");
            if(maxDigits == 5 && value > 99999) return s.Append("99999+");
            if(maxDigits == 6 && value > 999999) return s.Append("999999+");

            return s.Append(value);
        }

        public static StringBuilder AppendUpgrade(this StringBuilder s, MyUpgradeModuleInfo upgrade)
        {
            float modifier = (float)Math.Round(upgrade.Modifier, 3);

            switch(upgrade.ModifierType)
            {
                case MyUpgradeModifierType.Additive: s.Append('+').ProportionToPercent(modifier); break;
                case MyUpgradeModifierType.Multiplicative: s.Append('x').Append(modifier); break;
                default: s.Append(modifier).Append(' ').Append(upgrade.ModifierType.ToString()); break;
            }

            s.Append(' ').Append(upgrade.UpgradeType);
            return s;
        }

        #region Detailed info formats
        public static StringBuilder DetailInfo_Type(this StringBuilder s, IMyTerminalBlock block)
        {
            return s.Append("Type: ").Append(block.DefinitionDisplayNameText).Append('\n');
        }

        public static StringBuilder DetailInfo_Inventory(this StringBuilder s, IMyInventory inv, float definedMaxVolume = 0f, string customName = null)
        {
            if(inv == null)
                return s;

            float maxVolume = 0f;

            if(inv.MaxVolume >= MyFixedPoint.MaxValue)
            {
                if(definedMaxVolume > 0)
                    maxVolume = definedMaxVolume;
            }
            else
            {
                maxVolume = (float)inv.MaxVolume;
            }

            float volume = (float)inv.CurrentVolume;

            if(maxVolume > 0)
            {
                s.Append(customName ?? "Inventory").Append(':').Append(' ').ProportionToPercent(volume / maxVolume).Append(" (").VolumeFormat(volume * 1000f).Append(" / ").VolumeFormat(maxVolume * 1000f).Append(')').Append('\n');
            }
            else
            {
                s.Append(customName ?? "Inventory").Append(':').Append(' ').VolumeFormat(volume * 1000f).Append(" / Infinite\n");
            }

            s.Append(customName ?? "Inventory").Append(" Mass: ").MassFormat((float)inv.CurrentMass).Append('\n');
            return s;
        }

        public static StringBuilder DetailInfo_InputPower(this StringBuilder s, MyResourceSinkComponent sink)
        {
            if(sink == null)
                return s;

            float current = sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
            float max = sink.MaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId);
            return s.Append("Input Power: ").PowerFormat(current).Append(" (max: ").PowerFormat(max).Append(")\n");
        }

        public static StringBuilder DetailInfo_CurrentPowerUsage(this StringBuilder s, MyResourceSinkComponent sink)
        {
            if(sink == null)
                return s;

            float current = sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
            return s.Append("Current Power Usage: ").PowerFormat(current).Append('\n');
        }

        public static StringBuilder DetailInfo_MaxPowerUsage(this StringBuilder s, MyResourceSinkComponent sink)
        {
            if(sink == null)
                return s;

            float max = sink.MaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId);
            return s.Append("Max Power Usage: ").PowerFormat(max).Append('\n');
        }

        public static StringBuilder DetailInfo_InputHydrogen(this StringBuilder s, MyResourceSinkComponent sink)
        {
            if(sink == null)
                return s;

            float current = sink.CurrentInputByType(MyResourceDistributorComponent.HydrogenId);
            float max = sink.RequiredInputByType(MyResourceDistributorComponent.HydrogenId);
            return s.Append("Input H2: ").VolumeFormat(current).Append("/s (max: ").VolumeFormat(max).Append("/s)\n");
        }

        public static StringBuilder DetailInfo_OutputHydrogen(this StringBuilder s, MyResourceSourceComponent source)
        {
            if(source == null)
                return s;

            float current = source.CurrentOutputByType(MyResourceDistributorComponent.HydrogenId);
            float max = source.MaxOutputByType(MyResourceDistributorComponent.HydrogenId);
            return s.Append("Output H2: ").VolumeFormat(current).Append("/s (max: ").VolumeFormat(max).Append("/s)\n");
        }

        public static StringBuilder DetailInfo_InputOxygen(this StringBuilder s, MyResourceSinkComponent sink)
        {
            if(sink == null)
                return s;

            float current = sink.CurrentInputByType(MyResourceDistributorComponent.OxygenId);
            float max = sink.RequiredInputByType(MyResourceDistributorComponent.OxygenId);
            return s.Append("Input O2: ").VolumeFormat(current).Append("/s (max: ").VolumeFormat(max).Append("/s)\n");
        }

        public static StringBuilder DetailInfo_OutputOxygen(this StringBuilder s, MyResourceSourceComponent source)
        {
            if(source == null)
                return s;

            float current = source.CurrentOutputByType(MyResourceDistributorComponent.OxygenId);
            float max = source.MaxOutputByType(MyResourceDistributorComponent.OxygenId);
            return s.Append("Output O2: ").VolumeFormat(current).Append("/s (max: ").VolumeFormat(max).Append("/s)\n");
        }

        public static StringBuilder DetailInfo_InputGasList(this StringBuilder s, MyResourceSinkComponent sink, string linePrefix = "Input ")
        {
            if(sink == null)
                return s;

            foreach(MyDefinitionId res in sink.AcceptedResources)
            {
                if(res == MyResourceDistributorComponent.ElectricityId)
                    continue;

                float current = sink.CurrentInputByType(res);
                float max = sink.RequiredInputByType(res);

                s.Append(linePrefix);

                if(res == MyResourceDistributorComponent.HydrogenId)
                    s.Append("H2");
                else if(res == MyResourceDistributorComponent.OxygenId)
                    s.Append("O2");
                else
                    s.Append(res.SubtypeName);

                s.Append(": ").VolumeFormat(current).Append("/s (max: ").VolumeFormat(max).Append("/s)\n");
            }

            return s;
        }

        public static StringBuilder DetailInfo_OutputGasList(this StringBuilder s, MyResourceSourceComponent source, string linePrefix = "Output ")
        {
            if(source == null)
                return s;

            foreach(MyDefinitionId res in source.ResourceTypes)
            {
                if(res == MyResourceDistributorComponent.ElectricityId)
                    continue;

                float current = source.CurrentOutputByType(res);
                float max = source.MaxOutputByType(res);

                s.Append(linePrefix);

                if(res == MyResourceDistributorComponent.HydrogenId)
                    s.Append("H2");
                else if(res == MyResourceDistributorComponent.OxygenId)
                    s.Append("O2");
                else
                    s.Append(res.SubtypeName);

                s.Append(": ").VolumeFormat(current).Append("/s (max: ").VolumeFormat(max).Append("/s)\n");
            }

            return s;
        }
        #endregion Detailed info formats
    }
}