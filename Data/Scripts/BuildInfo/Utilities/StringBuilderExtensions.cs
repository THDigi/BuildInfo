using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
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

        public static int IndexOf(this StringBuilder sb, string findString, int startIndex = 0, int endIndex = -1, bool ignoreCase = false)
        {
            int length = sb.Length;
            if(length == 0)
                return -1;

            if(endIndex > -1)
                length = endIndex + 1;

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

        public static StringBuilder AppendMaxLength(this StringBuilder s, string text, int maxLength, bool addDots = true, bool noNewLines = true, bool fillWhitespace = false)
        {
            if(text == null)
            {
                s.Append("(NULL)");
                if(fillWhitespace)
                    s.Append(' ', maxLength - 6);
            }
            else
            {
                int len = s.Length;

                if(noNewLines)
                {
                    int newLine = text.IndexOf('\n');
                    if(newLine >= 0)
                        maxLength = Math.Min(maxLength, newLine); // redefine max length to before the first newline character
                }

                if(text.Length > maxLength)
                {
                    if(addDots)
                        s.Append(text, 0, maxLength - 1).Append('…');
                    else
                        s.Append(text, 0, maxLength);
                }
                else
                {
                    s.Append(text);
                }

                if(fillWhitespace)
                {
                    s.Append(' ', maxLength - (s.Length - len));
                }
            }

            return s;
        }

        public static StringBuilder AppendMaxLength(this StringBuilder s, StringBuilder text, int maxLength, bool addDots = true, bool noNewLines = true)
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
                s.EnsureCapacity(s.Length + maxLength);

                if(addDots)
                    maxLength -= 1;

                for(int i = 0; i < maxLength; i++)
                {
                    s.Append(text[i]);
                }

                if(addDots)
                    s.Append('…');
            }
            else
            {
                s.AppendStringBuilder(text);
            }

            return s;
        }

        public const string LineInfoSeparator = ", ";

        public static StringBuilder Separator(this StringBuilder s)
        {
            return s.Append(LineInfoSeparator);
        }

        public static StringBuilder RemoveLastSeparator(this StringBuilder s)
        {
            if(s.Length >= LineInfoSeparator.Length
            && s[s.Length - 2] == ','
            && s[s.Length - 1] == ' ')
                s.Length -= LineInfoSeparator.Length;
            return s;
        }

        public static StringBuilder NewLine(this StringBuilder s)
        {
            return s.Append('\n');
        }

        /// <summary>
        /// Appends <reset> then new line.
        /// Does nothing if TextAPI is not present or disabled.
        /// </summary>
        public static StringBuilder NewCleanLine(this StringBuilder s)
        {
            if(BuildInfoMod.Instance.TextAPI.IsEnabled)
            {
                CurrentColor = VRageMath.Color.White;
                return s.Append("<reset>\n");
            }
            return s.Append('\n');
        }

        public static StringBuilder Label(this StringBuilder s, string label)
        {
            return s.Append(label).Append(": ");
        }

        public static StringBuilder LabelHardcoded(this StringBuilder s, string label)
        {
            Color prevColor = CurrentColor;
            return s.Append(label).Color(new Color(255, 200, 100)).Append('*').Color(prevColor).Append(": ");
        }

        public static StringBuilder HardcodedMarker(this StringBuilder s)
        {
            Color prevColor = CurrentColor;
            return s.Color(new Color(255, 200, 100)).Append('*').Color(prevColor);
        }

        public static StringBuilder BoolFormat(this StringBuilder s, bool b)
        {
            return s.Append(b ? "Yes" : "No");
        }

        /// <summary>
        /// Appends the tooltip hint icon, or nothing if TextAPI is not present or disabled.
        /// </summary>
        public static StringBuilder MarkTooltip(this StringBuilder s)
        {
            if(BuildInfoMod.Instance.TextAPI.IsEnabled)
                return s.Append("<reset> ").Append(FontsHandler.IconTooltip).Color(CurrentColor);
            return s;
        }

        /// <summary>
        /// Assigned by Color methods as well as Reset ones.
        /// </summary>
        public static Color CurrentColor { get; set; } = VRageMath.Color.White;

        /// <summary>
        /// Adds a <color=r,g,b> in the text for TextAPI coloring.
        /// Also adds alpha if it's <255.
        /// And assigns <see cref="CurrentColor"/>.
        /// Does nothing if TextAPI is not present or disabled.
        /// </summary>
        public static StringBuilder Color(this StringBuilder s, Color color)
        {
            if(BuildInfoMod.Instance.TextAPI.IsEnabled)
            {
                s.Append("<color=").Append(color.R).Append(',').Append(color.G).Append(',').Append(color.B).Append('>');

                if(color.A < 255)
                    s.Append(color.A).Append('>');

                CurrentColor = color;
            }
            return s;
        }

        /// <summary>
        /// Appends <reset> to the stringbuilder which clears all formatting for TextAPI.
        /// And assigns <see cref="CurrentColor"/>.
        /// Does nothing if TextAPI is not present or disabled.
        /// </summary>
        public static StringBuilder ResetFormatting(this StringBuilder s)
        {
            if(BuildInfoMod.Instance.TextAPI.IsEnabled)
            {
                CurrentColor = VRageMath.Color.White;
                return s.Append("<reset>");
            }
            return s;
        }

        /// <summary>
        /// Appends <reset> then the given icon character, after which it restores text color after.
        /// Only appends given character if TextAPI is not present or disabled.
        /// </summary>
        public static StringBuilder Icon(this StringBuilder s, char icon)
        {
            if(BuildInfoMod.Instance.TextAPI.IsEnabled)
                s.Append("<reset>");

            s.Append(icon);

            if(BuildInfoMod.Instance.TextAPI.IsEnabled)
                s.Color(CurrentColor);

            return s;
        }

        /// <summary>
        /// Saves existing color, calls <see cref="Color(StringBuilder, VRageMath.Color)"/> with the given input, appends character then calls Color() again with original color.
        /// Only appends given character if TextAPI is not present or disabled.
        /// </summary>
        public static StringBuilder Icon(this StringBuilder s, Color color, char icon)
        {
            Color prevColor = CurrentColor;
            if(BuildInfoMod.Instance.TextAPI.IsEnabled)
                s.Color(color);

            s.Append(icon);

            if(BuildInfoMod.Instance.TextAPI.IsEnabled)
                s.Color(prevColor);

            return s;
        }

        /// <summary>
        /// Append the given string without TextAPI formatting tags.
        /// </summary>
        public static StringBuilder AppendSanitized(this StringBuilder sb, string text)
        {
            for(int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if(c == '<')
                {
                    if(text.IndexOf("<color=", i) == i)
                    {
                        int endIdx = text.IndexOf('>', i + "<color=".Length);
                        if(endIdx != -1)
                        {
                            i = endIdx + 1;
                            continue;
                        }
                    }
                    else if(text.IndexOf("<reset>", i) == i)
                    {
                        i += "<reset>".Length;
                        continue;
                    }
                    else if(text.IndexOf("<i>", i) == i)
                    {
                        i += "<i>".Length;
                        continue;
                    }
                    else if(text.IndexOf("</i>", i) == i)
                    {
                        i += "</i>".Length;
                        continue;
                    }
                }

                sb.Append(c);
            }

            return sb;
        }

        public static StringBuilder IsPowerAdaptable(this StringBuilder s, MyStringHash groupId, bool showNotAdaptable = false)
        {
            Constants constants = BuildInfoMod.Instance.Constants;
            ResourceGroupData data;

            bool adaptible = false;

            if(groupId != MyStringHash.NullOrEmpty && constants.ResourceGroupPriority.TryGetValue(groupId, out data))
            {
                if(data.Def.IsAdaptible)
                    adaptible = true;
            }

            if(adaptible)
                s.Append(" (adaptable)");
            else if(showNotAdaptable)
                s.Append(" (non-adaptable)");

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
            // from MyBankingSystem.GetFormatedValue()
            if(money > 1000000000000L || money < -1000000000000L)
            {
                money /= 1000000000000L;
                return s.Append(money.ToString("N0")).Append(" T ").Append(Constants.CurrencyShortName);
            }

            return s.Append(money.ToString("N0")).Append(" ").Append(Constants.CurrencyShortName);
        }

        static bool IsValid(StringBuilder s, float f, string suffix = "", string prefix = "")
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

            if(N < 0)
            {
                s.Append("-");
                N = -N;
            }

            if(N > 1e18f || BuildInfoMod.Instance.Config.ScientificNotation.Value)
                return s.ScientificNumber(N).Append(" N");

            if(N >= 1e15f)
                return s.Number(N / 1e15f).Append(" PN");

            if(N >= 1e12f)
                return s.Number(N / 1e12f).Append(" TN");

            if(N >= 1e9f)
                return s.Number(N / 1e9f).Append(" GN");

            if(N >= 1e6f)
                return s.Number(N / 1e6f).Append(" MN");

            if(N >= 1e3f)
                return s.Number(N / 1e3f).Append(" kN");

            return s.Number(N).Append(" N");
        }

        public static StringBuilder TorqueFormat(this StringBuilder s, float Nm)
        {
            return s.ForceFormat(Nm).Append(FontsHandler.CharMiddleDot).Append('m');
        }

        public static StringBuilder RotationSpeed(this StringBuilder s, float radPerSecond, int digits = 2)
        {
            float degPerSec = MathHelper.ToDegrees(radPerSecond);

            if(!IsValid(s, degPerSec, "°/s"))
                return s;

            return s.RoundedNumber(degPerSec, digits).Append("°/s");
        }

        public static StringBuilder PowerFormat(this StringBuilder s, float MW)
        {
            float w = MW * 1e6f;

            if(!IsValid(s, w, " W"))
                return s;

            if(w < 0)
            {
                s.Append("-");
                w = -w;
            }

            if(w >= 1e18f || BuildInfoMod.Instance.Config.ScientificNotation.Value)
                return s.ScientificNumber(w).Append(" W");

            if(w >= 1e15f)
                return s.Number(w / 1e15f).Append(" PW");

            if(w >= 1e12f)
                return s.Number(w / 1e12f).Append(" TW");

            if(w >= 1e9f)
                return s.Number(w / 1e9f).Append(" GW");

            if(w >= 1e6f)
                return s.Number(w / 1e6f).Append(" MW");

            if(w >= 1e3f)
                return s.Number(w / 1e3f).Append(" kW");

            return s.Number(w).Append(" W");
        }

        public static StringBuilder PowerStorageFormat(this StringBuilder s, float MWh)
        {
            return s.PowerFormat(MWh).Append('h');
        }

        /// <summary>
        /// If <paramref name="digits"/> is left as -1 it will the default for <see cref="Number(StringBuilder, float)"/> (2 digits)
        /// </summary>
        public static StringBuilder DistanceFormat(this StringBuilder s, float m, int digits = -1)
        {
            if(!IsValid(s, m, " m"))
                return s;

            if(m < 0)
            {
                s.Append("-");
                m = -m;
            }

            if(m >= 1e12f || BuildInfoMod.Instance.Config.ScientificNotation.Value)
                return s.ScientificNumber(m).Append(" m");

            if(digits < 0)
            {
                if(m >= 1000)
                    return s.Number(m / 1000).Append(" km");

                return s.Number(m).Append(" m");
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

            bool m2negative = false;

            if(m1 < 0)
            {
                s.Append("-");
                m1 = -m1;
            }

            if(m2 < 0)
            {
                m2negative = true;
                m2 = -m2;
            }

            if(m1 >= 1e12f || BuildInfoMod.Instance.Config.ScientificNotation.Value)
                return s.ScientificNumber(m1).Append(m2negative ? "~-" : "~").ScientificNumber(m2).Append(" m");

            if(m1 >= 1000)
                return s.Number(m1 / 1000).Append(m2negative ? "~-" : "~").Number(m2 / 1000).Append(" km");

            if(m1 < 10)
                return s.Number(m1).Append(m2negative ? "~-" : "~").Number(m2).Append(" m");

            return s.RoundedNumber(m1, 0).Append(m2negative ? "~-" : "~").RoundedNumber(m2, 0).Append(" m");
        }

        /// <summary>
        /// Maximum precision number, no division to other units
        /// </summary>
        public static StringBuilder ExactMassFormat(this StringBuilder s, float kg, bool includeUnit = true)
        {
            string unit = includeUnit ? " kg" : "";

            if(!IsValid(s, kg, unit))
                return s;

            if(kg == 0)
                return s.Append('0').Append(unit);

            if(kg < 0)
            {
                s.Append("-");
                kg = -kg;
            }

            if(kg > 1e12f)
                return s.Append(kg.ToString("0.######e0")).Append(unit);

            return s.Number(kg).Append(unit);
        }

        public static StringBuilder MassFormat(this StringBuilder s, float kg)
        {
            if(!IsValid(s, kg, " kg"))
                return s;

            if(kg == 0)
                return s.Append("0 kg");

            float g = kg * 1e3f;

            if(g < 0)
            {
                s.Append("-");
                g = -g;
            }

            if(g >= 1e18f || BuildInfoMod.Instance.Config.ScientificNotation.Value)
                return s.ScientificNumber(kg).Append(" kg");

            if(g >= 1e15f)
                return s.Number(g / 1e15f).Append(" Pg");

            if(g >= 1e12f)
                return s.Number(g / 1e12f).Append(" Tg");

            if(g >= 1e9f)
                return s.Number(g / 1e9f).Append(" Gg");

            if(g >= 1e6f)
                return s.Number(g / 1e6f).Append(" Mg");

            if(g >= 1e3f)
                return s.Number(g / 1e3f).Append(" kg");

            if(g >= 1)
                return s.Number(g).Append(" grams");

            if(g >= 1e-3f)
                return s.Number(g * 1e3f).Append(" mg");

            //if(g >= 1e-6f)
            return s.Number(g * 1e6f).Append(" µg");
        }

        public static StringBuilder IntegrityFormat(this StringBuilder s, float integrity)
        {
            if(!IsValid(s, integrity))
                return s;

            if(integrity < 0)
            {
                s.Append("-");
                integrity = -integrity;
            }

            //if(integrity >= 1e12f || BuildInfoMod.Instance.Config.ScientificNotation.Value)
            //    return s.NumberScientific(integrity);

            //if(integrity >= 1e9f)
            //    return s.Number(integrity / 1e9f).Append(" G");
            //
            //if(integrity >= 1e6f)
            //    return s.Number(integrity / 1e6f).Append(" M");
            //
            //if(integrity >= 1e3f)
            //    return s.Number(integrity / 1e3f).Append(" k");

            return s.Number(integrity);
        }

        public static StringBuilder VolumeFormat(this StringBuilder s, float L)
        {
            if(!IsValid(s, L, " L"))
                return s;

            if(L == 0)
                return s.Append("0 L");

            if(L < 0)
            {
                s.Append("-");
                L = -L;
            }

            if(L > 1e18f || BuildInfoMod.Instance.Config.ScientificNotation.Value)
                return s.ScientificNumber(L).Append(" L");

            if(L >= 1e15f)
                return s.Number(L / 1e15f).Append(" PL");

            if(L >= 1e12f)
                return s.Number(L / 1e12f).Append(" TL");

            if(L >= 1e9f)
                return s.Number(L / 1e9f).Append(" GL");

            if(L >= 1e6f)
                return s.Number(L / 1e6f).Append(" ML");

            if(L >= 1e3f)
                return s.Number(L / 1e3f).Append(" kL");

            if(L >= 1f)
                return s.Number(L).Append(" L");

            if(L >= 1e-3f)
                return s.Number(L * 1e3f).Append(" mL");

            //if(l >= 1e-6f)
            return s.Number(L * 1e6f).Append(" µL");
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

            if(totalSeconds < 0)
            {
                totalSeconds = -totalSeconds;
                s.Append('-');
            }

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

        public static StringBuilder AngleFormat(this StringBuilder s, float radians, int digits = 0) => s.AngleFormatDeg(MathHelper.ToDegrees(radians), digits);

        public static StringBuilder AngleFormatDeg(this StringBuilder s, float degrees, int digits = 0)
        {
            if(!IsValid(s, degrees))
                return s.Append('°');

            return s.RoundedNumber(degrees, digits).Append('°');
        }

        public static StringBuilder Size3DFormat(this StringBuilder s, Vector3 vec)
        {
            return s.Number(vec.X).Append('x').Number(vec.Y).Append('x').Number(vec.Z);
        }

        public static StringBuilder VectorOffsetFormat(this StringBuilder s, Vector3 vec)
        {
            int lenBefore = s.Length;

            if(Math.Abs(vec.X) > 0)
                s.DistanceFormat(vec.X).Append(" right, ");

            if(Math.Abs(vec.Y) > 0)
                s.DistanceFormat(vec.Y).Append(" up, ");

            if(Math.Abs(vec.Z) > 0)
                s.DistanceFormat(vec.Z).Append(" back, ");

            if(s.Length > lenBefore)
                s.Length -= 2; // remove last comma and space
            else
                s.Append("None");

            return s;
        }

        public static StringBuilder SpeedFormat(this StringBuilder s, float metersPerSecond, int digits = 2)
        {
            if(!IsValid(s, metersPerSecond))
                return s.Append(" m/s");

            if(metersPerSecond == 0)
                return s.Append("0 m/s");

            if(metersPerSecond < 0)
            {
                s.Append('-');
                metersPerSecond = -metersPerSecond;
            }

            if(metersPerSecond > 1e9f || BuildInfoMod.Instance.Config.ScientificNotation.Value)
                return s.ScientificNumber(metersPerSecond).Append(" m/s");

            return s.RoundedNumber(metersPerSecond, digits).Append(" m/s");
        }

        public static StringBuilder AccelerationFormat(this StringBuilder s, float metersPerSecondSquared) => s.SpeedFormat(metersPerSecondSquared).Append('²');

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

        public static StringBuilder MultiplierFormat(this StringBuilder s, float multiplier, int digits = 2)
        {
            if(!IsValid(s, multiplier))
                return s;

            return s.Append("x").RoundedNumber(multiplier, digits);
        }

        public static StringBuilder OptionalMultiplier(this StringBuilder s, float mul)
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

        public static StringBuilder IdTypeSubtypeFormat(this StringBuilder s, MyDefinitionId id)
        {
            string typeName = id.TypeId.ToString();
            int index = typeName.IndexOf('_') + 1;

            if(index > -1)
                s.Append(typeName, index, typeName.Length - index);
            else
                s.Append(typeName);

            s.Append("/").Append(id.SubtypeName);
            return s;
        }

        public static StringBuilder IdFriendlyFormat(this StringBuilder s, MyDefinitionId id)
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

            if(id.TypeId == typeof(MyObjectBuilder_BlueprintDefinition) || id.TypeId == typeof(MyObjectBuilder_CompositeBlueprintDefinition))
                return s.Append(id.SubtypeName).Append(" bp");

            return s.Append(id.SubtypeName).Append(' ').IdTypeFormat(id.TypeId);
        }

        /// <summary>
        /// Append only first line of the given string.
        /// </summary>
        public static StringBuilder FirstLine(this StringBuilder s, string multiLineString)
        {
            int newLineIdx = multiLineString.IndexOf('\n');
            if(newLineIdx != -1)
                s.Append(multiLineString, 0, newLineIdx);
            else
                s.Append(multiLineString);

            return s;
        }

        public static StringBuilder CleanPlayerName(this StringBuilder s, string playerName, int maxChars = int.MaxValue)
        {
            int appended = 0;
            for(int i = 0; i < playerName.Length; i++)
            {
                char c = playerName[i];

                if(Hardcoded.PlatformIcon.List.Contains(c))
                    continue;

                s.Append(c);

                if(++appended >= maxChars)
                    break;
            }

            return s;
        }

        public static StringBuilder DefinitionName(this StringBuilder s, MyDefinitionBase def, MyDefinitionId? idIfDefNull = null)
        {
            if(def == null)
            {
                s.Append("(Inexistent)");
                if(idIfDefNull != null)
                    s.Append(' ').IdFriendlyFormat(idIfDefNull.Value);
                return s;
            }

            string name = def.DisplayNameText;
            if(!string.IsNullOrWhiteSpace(name) && name[0] != '\n')
            {
                int newLineIdx = name.IndexOf('\n');
                if(newLineIdx != -1)
                    s.Append(name, 0, newLineIdx);
                else
                    s.Append(name);
            }
            else
                s.IdFriendlyFormat(def.Id);
            return s;
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
                string name = itemDef.DisplayNameText;

                // ignore other lines in name if any
                int newlineIndex = name.IndexOf('\n');
                if(newlineIndex != -1)
                    return s.Append(name, 0, newlineIndex);
                else
                    return s.Append(name);
            }

            return s.Append(id.SubtypeName).Append(' ').IdTypeFormat(id.TypeId);
        }

        public static StringBuilder ModFormat(this StringBuilder s, MyModContext context)
        {
            TextGeneration tg = BuildInfoMod.Instance.TextGeneration;
            s.Color(tg.COLOR_MOD_TITLE).AppendMaxLength(context.ModName, TextGeneration.MOD_NAME_MAX_LENGTH);

            MyObjectBuilder_Checkpoint.ModItem modItem = context.ModItem;
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

            if(value == 0)
                return s.Append("0");

            if(value < 0)
            {
                s.Append('-');
                value = -value;
            }

            if(value > 1e12f || BuildInfoMod.Instance.Config.ScientificNotation.Value)
                return s.ScientificNumber(value);

            return s.Append(value.ToString("###,###,###,###,###,##0.##"));
        }

        public static StringBuilder RoundedNumber(this StringBuilder s, float value, int digits)
        {
            if(digits == 3)
            {
                if(BuildInfoMod.IsDevMod)
                    Log.Error("Used 3-digit rounding! that's ambiguous, overriding to 4.");

                digits = 4;
            }

            if(!IsValid(s, value))
                return s;

            //if(value == 0)
            //{
            //    if(digits > 2)
            //        return s.Append("0.").Append('0', digits);
            //    else
            //        return s.Append("0");
            //}

            if(value < 0)
            {
                s.Append('-');
                value = -value;
            }

            bool useSci = value > 1e12f || (BuildInfoMod.Instance.Config.ScientificNotation.Value && value > MinForScientific);

            if(digits <= 2)
            {
                if(useSci)
                    return s.Append(value.ToString("0.##e0"));
                else
                    // no need for zero-fill if 2 or less as it can't be mistaken with thousands
                    return s.Append(Math.Round(value, digits).ToString("###,###,###,###,###,##0.##"));
            }
            else
            {
                if(useSci)
                    return s.Append(value.ToString("0.0000e0"));
                else
                {
                    // use zero-fill up to 4 digits to avoid mistaking it with thousands
                    string formatted = Math.Round(value, digits).ToString("###,###,###,###,###,##0.0000########");

                    if(formatted.EndsWith(".0000"))
                        return s.Append(formatted, 0, formatted.Length - 5); // 1.0000 -> 1

                    if(formatted.IndexOf('.') != -1) // must first ensure we're not removing from integers
                    {
                        if(formatted.EndsWith("000"))
                            return s.Append(formatted, 0, formatted.Length - 3); // 0.5000 -> 0.5

                        if(formatted.EndsWith("00"))
                            return s.Append(formatted, 0, formatted.Length - 2); // 0.0500 -> 0.05
                    }

                    return s.Append(formatted);
                }
            }
        }

        const float MinForScientific = 1e3f;

        /// <summary>
        /// Scientific notation for numbers larger than 1000 (or smaller than -1000)
        /// </summary>
        public static StringBuilder ScientificNumber(this StringBuilder s, float value, int digits = 2)
        {
            if(value == 0)
                return s.Append("0");

            if(value < 0)
            {
                s.Append('-');
                value = -value;
            }

            if(value >= MinForScientific)
                return s.Append(value.ToString("0.##e0"));
            else
                return s.Append(Math.Round(value, digits));
        }

        //static char[] SplitExponent = new char[] { 'e' };
        //public static StringBuilder ExponentNumber(this StringBuilder s, double value)
        //{
        //    if(value >= 10000 || value < 0.0001)
        //    {
        //        //Vector3 hsv = CurrentColor.ColorToHSV();
        //
        //        //hsv.X += 0.2f;
        //        //if(hsv.X > 1f)
        //        //    hsv.X -= 1f;
        //
        //        //if(hsv.Y <= 0.8f)
        //        //    hsv.Y += 0.2f;
        //        //else if(hsv.Z <= 0.8f)
        //        //    hsv.Z += 0.2f;
        //
        //        //Color expColor = hsv.HSVtoColor();
        //
        //        //s.Append(value.ToString($"0.##<i><color={expColor.R}\\,{expColor.G}\\,{expColor.B}>e+0</i>")).Color(CurrentColor);
        //
        //        string numText = value.ToString($"0.##e+0");
        //        string[] parts = numText.Split(SplitExponent);
        //
        //        if(parts.Length != 2)
        //            Log.Error($"Exponent retrieval error, expected 2 parts, got {parts.Length}; split '{numText}' by 'e'; value={value.ToString("N10")}");
        //
        //        Color prevColor = CurrentColor;
        //        s.Append(parts[0]).Color(new Color(200, 55, 200)).Append('e').Color(prevColor).Append(parts[1]);
        //    }
        //    else
        //    {
        //        if(value > 10)
        //            s.Append(Math.Round(value, 2));
        //        else
        //            s.Append(Math.Round(value, 4));
        //    }
        //    return s;
        //}

        public static StringBuilder ShortNumber(this StringBuilder s, float value)
        {
            if(!IsValid(s, value))
                return s;

            if(value < 0)
            {
                s.Append('-');
                value = -value;
            }

            if(value >= 1e6f)
                return s.RoundedNumber(value / 1e3f, 0).Append('k');

            if(value >= 1e3f)
                return s.RoundedNumber(value / 1e3f, 1).Append('k');

            return s.RoundedNumber(value, 1);
        }

        /// <summary>
        /// Capped in character width, for use in toolbar status
        /// </summary>
        public static StringBuilder NumberCapped(this StringBuilder s, int value, int maxLength)
        {
            if(value < 0) throw new Exception("negative values not supported");
            if(maxLength <= 1) throw new Exception("max digits can't be 1 or lower");
            if(maxLength > 8) throw new Exception("max digits supported up to 8");

            if(maxLength == 2 && value > 9) return s.Append("9+");
            if(maxLength == 3 && value > 99) return s.Append("99+");
            if(maxLength == 4 && value > 999) return s.Append("999+");
            if(maxLength == 5 && value > 9999) return s.Append("9999+");
            if(maxLength == 6 && value > 99999) return s.Append("99999+");
            if(maxLength == 7 && value > 999999) return s.Append("999999+");
            if(maxLength == 8 && value > 9999999) return s.Append("9999999+");

            return s.Append(value);
        }

        /// <summary>
        /// Capped in character width with a space at the end that is replaced by + if too large, for use in toolbar status
        /// </summary>
        public static StringBuilder NumberCappedSpaced(this StringBuilder s, int value, int maxLength)
        {
            if(value < 0) throw new Exception("negative values not supported");
            if(maxLength <= 1) throw new Exception("max digits can't be 1 or lower");
            if(maxLength > 8) throw new Exception("max digits supported up to 8");

            if(maxLength == 2 && value > 9) return s.Append("9+");
            if(maxLength == 3 && value > 99) return s.Append("99+");
            if(maxLength == 4 && value > 999) return s.Append("999+");
            if(maxLength == 5 && value > 9999) return s.Append("9999+");
            if(maxLength == 6 && value > 99999) return s.Append("99999+");
            if(maxLength == 7 && value > 999999) return s.Append("999999+");
            if(maxLength == 8 && value > 9999999) return s.Append("9999999+");

            return s.Append(value).Append(' ');
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

        public static StringBuilder DetailInfo_CustomGas(this StringBuilder s, string title, MyDefinitionId resId, float current, float max)
        {
            s.Append(title).Append(' ');

            if(resId == MyResourceDistributorComponent.HydrogenId)
                s.Append("H2");
            else if(resId == MyResourceDistributorComponent.OxygenId)
                s.Append("O2");
            else
                s.Append(resId.SubtypeName);

            s.Append(": ").VolumeFormat(current).Append("/s (max: ").VolumeFormat(max).Append("/s)\n");

            return s;
        }
        #endregion Detailed info formats
    }
}