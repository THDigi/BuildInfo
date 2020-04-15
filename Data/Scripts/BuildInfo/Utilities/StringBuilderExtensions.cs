using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features;
using Digi.BuildInfo.Features.Config;
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
        private static Config Settings => BuildInfoMod.Instance.Config;
        private static Constants Constants => BuildInfoMod.Instance.Constants;
        private static TextGeneration TextGeneration => BuildInfoMod.Instance.TextGeneration;
        private static bool TextAPIEnabled => BuildInfoMod.Instance.TextAPI.IsEnabled;

        // copy of StringBuilderExtensions_2.TrimTrailingWhitespace() since it's not whitelisted in modAPI
        public static StringBuilder TrimEndWhitespace(this StringBuilder sb)
        {
            int num = sb.Length;

            while(num > 0 && (sb[num - 1] == ' ' || sb[num - 1] == '\r' || sb[num - 1] == '\n'))
            {
                num--;
            }

            sb.Length = num;
            return sb;
        }

        public static StringBuilder AppendRGBA(this StringBuilder sb, Color color)
        {
            return sb.Append(color.R).Append(", ").Append(color.G).Append(", ").Append(color.B).Append(", ").Append(color.A);
        }

        public static StringBuilder AppendSubstring(this StringBuilder sb, string text, int start, int count)
        {
            sb.EnsureCapacity(sb.Length + count);

            for(int i = 0; i < count; i++)
            {
                sb.Append(text[start + i]);
            }

            return sb;
        }

        public static StringBuilder AppendMaxLength(this StringBuilder s, string text, int maxLength, bool addDots = true, bool noNewLines = true)
        {
            if(text == null)
                return s.Append("(NULL)");

            if(noNewLines)
            {
                var newLine = text.IndexOf('\n');
                if(newLine >= 0)
                    maxLength = Math.Min(maxLength, newLine); // redefine max length to before the first newline character
            }

            if(text.Length > maxLength)
            {
                if(addDots)
                    s.AppendSubstring(text, 0, maxLength - 1).Append('…');
                else
                    s.AppendSubstring(text, 0, maxLength);
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
            return s.LabelHardcoded(label, TextGeneration.COLOR_NORMAL);
        }

        public static StringBuilder Hardcoded(this StringBuilder s)
        {
            return s.Color(new Color(255, 200, 100)).Append('*');
        }

        public static StringBuilder BoolFormat(this StringBuilder s, bool b)
        {
            return s.Append(b ? "Yes" : "No");
        }

        public static StringBuilder Color(this StringBuilder s, Color color)
        {
            if(TextAPIEnabled)
                s.Append("<color=").Append(color.R).Append(',').Append(color.G).Append(',').Append(color.B).Append('>');

            return s;
        }

        public static StringBuilder ResetColor(this StringBuilder s)
        {
            if(TextAPIEnabled)
            {
                var color = TextGeneration.COLOR_NORMAL;
                s.Append("<color=").Append(color.R).Append(',').Append(color.G).Append(',').Append(color.B).Append('>');
            }

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

            var constants = BuildInfoMod.Instance.Constants;
            ResourceGroupData data;

            if(groupId != MyStringHash.NullOrEmpty && constants.resourceGroupPriority.TryGetValue(groupId, out data))
            {
                s.Append(groupId.String).Append(" (").Append(data.Priority).Append("/").Append(data.Def.IsSource ? constants.resourceSourceGroups : constants.resourceSinkGroups).Append(")");
            }
            else
            {
                s.Append("Undefined (last)");
            }

            return s;
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

            if(m >= 1000)
                return s.Number(m / 1000).Append(" km");

            if(digits <= -1)
            {
                if(m < 10)
                    return s.RoundedNumber(m, 2).Append(" m");

                return s.Append((int)m).Append(" m");
            }
            else
            {
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

            return s.Append((int)m1).Append("~").Append((int)m2).Append(" m");
        }

        public static StringBuilder MassFormat(this StringBuilder s, float kg)
        {
            if(!IsValid(s, kg, " kg"))
                return s;

            if(Math.Abs(kg) < 0.0000001f)
                return s.Append("0 kg");

            if(kg >= 1000000000)
                return s.Number(kg / 1000000000f).Append(" Mt");

            if(kg >= 1000000)
                return s.Number(kg / 1000000f).Append(" kt");

            if(kg >= 1000)
                return s.Number(kg / 1000f).Append(" t");

            if(kg >= 1)
                return s.Number(kg).Append(" kg");

            if(kg >= 0.001f)
                return s.Number(kg * 1000f).Append(" grams");

            return s.Number(kg * 1000000f).Append(" miligrams");
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
            if(!IsValid(s, l, " l"))
                return s;

            if(l >= 1000000000000)
                return s.Number(l / 1000000000000f).Append(" TL");

            if(l >= 1000000000)
                return s.Number(l / 1000000000f).Append(" GL");

            if(l >= 1000000)
                return s.Number(l / 1000000f).Append(" ML");

            if(l >= 1000)
                return s.Number(l / 1000f).Append(" kL");

            return s.Number(l).Append(" L");
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyInventoryConstraint inputConstraint, MyInventoryConstraint outputConstraint)
        {
            var types = Caches.GetObTypeSet();
            types.AddSetReader(inputConstraint.ConstrainedTypes);
            types.AddSetReader(outputConstraint.ConstrainedTypes);

            var items = Caches.GetDefIdSet();
            items.AddSetReader(inputConstraint.ConstrainedIds);
            items.AddSetReader(outputConstraint.ConstrainedIds);

            // HACK only using input constraint's whitelist status, not sure if output inventory's whitelist is needed
            return s.InventoryFormat(volume,
                types: types,
                items: items,
                isWhitelist: inputConstraint.IsWhitelist);
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyInventoryConstraint inventoryConstraint)
        {
            var types = Caches.GetObTypeSet();
            types.AddSetReader(inventoryConstraint.ConstrainedTypes);

            var items = Caches.GetDefIdSet();
            items.AddSetReader(inventoryConstraint.ConstrainedIds);

            return s.InventoryFormat(volume,
                types: types,
                items: items,
                isWhitelist: inventoryConstraint.IsWhitelist);
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyObjectBuilderType allowedType)
        {
            var types = Caches.GetObTypeSet();
            types.Add(allowedType);

            return s.InventoryFormat(volume, types: types);
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyDefinitionId[] allowedItems)
        {
            var items = Caches.GetDefIdSet();
            items.AddArray(allowedItems);

            return s.InventoryFormat(volume, items: items);
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyDefinitionId allowedItem)
        {
            var items = Caches.GetDefIdSet();
            items.Add(allowedItem);

            return s.InventoryFormat(volume, items: items);
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, HashSet<MyObjectBuilderType> types = null, HashSet<MyDefinitionId> items = null, bool isWhitelist = true)
        {
            if(Settings.PlaceInfo.IsSet(PlaceInfoFlags.InventoryVolumeMultiplied))
            {
                var mul = MyAPIGateway.Session.BlocksInventorySizeMultiplier;

                MyValueFormatter.AppendVolumeInBestUnit(volume * mul, s);

                if(Math.Abs(mul - 1) > 0.001f)
                    s.Color(TextGeneration.COLOR_UNIMPORTANT).Append(" (x").Append(Math.Round(mul, 2)).Append(")").ResetColor();
            }
            else
            {
                MyValueFormatter.AppendVolumeInBestUnit(volume, s);
            }

            if(Settings.PlaceInfo.IsSet(PlaceInfoFlags.InventoryExtras))
            {
                if(types == null && items == null)
                    types = Constants.DEFAULT_ALLOWED_TYPES;

                var physicalItems = MyDefinitionManager.Static.GetPhysicalItemDefinitions();
                var minMass = float.MaxValue;
                var maxMass = 0f;

                foreach(var item in physicalItems)
                {
                    if(!item.Public || item.Mass <= 0 || item.Volume <= 0)
                        continue; // skip hidden and physically impossible items

                    if((types != null && isWhitelist == types.Contains(item.Id.TypeId)) || (items != null && isWhitelist == items.Contains(item.Id)))
                    {
                        var fillMass = item.Mass * (volume / item.Volume);
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

        public static StringBuilder TimeFormat(this StringBuilder s, float seconds)
        {
            if(!IsValid(s, seconds))
                return s.Append(" seconds");

            if(seconds > (60 * 60 * 24 * 365))
                return s.Append("1 y+");

            var span = TimeSpan.FromSeconds(seconds);

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
                s.Append(' ').Append((seconds % 60).ToString("0.#")).Append('s');
                return s;
            }

            s.Append(seconds.ToString("0.#")).Append('s');
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

        public static StringBuilder ProportionToPercent(this StringBuilder s, float proportion)
        {
            if(!IsValid(s, proportion))
                return s.Append('%');

            return s.Append((int)(proportion * 100)).Append('%');
        }

        public static StringBuilder MultiplierFormat(this StringBuilder s, float mul)
        {
            if(Math.Abs(mul - 1f) > 0.001f)
                s.Append(" (x").Number(mul).Append(")");

            return s;
        }

        public static StringBuilder IdTypeFormat(this StringBuilder s, MyObjectBuilderType type)
        {
            var typeName = type.ToString();
            var index = typeName.IndexOf('_') + 1;
            s.Append(typeName, index, typeName.Length - index);

            if(typeName.EndsWith("GasProperties"))
                s.Length -= "Properties".Length; // manually fixing "GasProperties" to just "Gas"

            return s;
        }

        public static StringBuilder IdTypeSubtypeFormat(this StringBuilder s, MyDefinitionId id)
        {
            return s.Append(id.SubtypeName).Append(' ').IdTypeFormat(id.TypeId);
        }

        public static StringBuilder ModFormat(this StringBuilder s, MyModContext context)
        {
            s.Color(TextGeneration.COLOR_MOD_TITLE).AppendMaxLength(context.ModName, TextGeneration.MOD_NAME_MAX_LENGTH);

            var id = context.GetWorkshopID();

            if(id > 0)
                s.Color(TextGeneration.COLOR_UNIMPORTANT).Append(" (id: ").Append(id).Append(")");

            return s;
        }

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

        public static StringBuilder AppendUpgrade(this StringBuilder s, MyUpgradeModuleInfo upgrade)
        {
            var modifier = Math.Round(upgrade.Modifier, 3);

            switch(upgrade.ModifierType)
            {
                case MyUpgradeModifierType.Additive: s.Append('+').Append(modifier); break;
                case MyUpgradeModifierType.Multiplicative: s.Append('x').Append(modifier); break;
                default: s.Append(modifier).Append(' ').Append(upgrade.ModifierType.ToString()); break;
            }

            s.Append(' ').Append(upgrade.UpgradeType);
            return s;
        }

        #region Detailed info formats
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

            var volume = (float)inv.CurrentVolume;

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

            var current = sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
            var max = sink.MaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId);

            return s.Append("Input Power: ").PowerFormat(current).Append(" / ").PowerFormat(max).Append('\n');
        }

        public static StringBuilder DetailInfo_CurrentPowerUsage(this StringBuilder s, MyResourceSinkComponent sink)
        {
            if(sink == null)
                return s;

            var current = sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);

            return s.Append("Current Power Usage: ").PowerFormat(current).Append('\n');
        }

        public static StringBuilder DetailInfo_MaxPowerUsage(this StringBuilder s, MyResourceSinkComponent sink)
        {
            if(sink == null)
                return s;

            var max = sink.MaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId);

            return s.Append("Max Power Usage: ").PowerFormat(max).Append('\n');
        }

        public static StringBuilder DetailInfo_InputHydrogen(this StringBuilder s, MyResourceSinkComponent sink)
        {
            if(sink == null)
                return s;

            var current = sink.CurrentInputByType(MyResourceDistributorComponent.HydrogenId);
            var max = sink.RequiredInputByType(MyResourceDistributorComponent.HydrogenId);

            return s.Append("Input H2: ").VolumeFormat(current).Append(" / ").VolumeFormat(max).Append('\n');
        }

        public static StringBuilder DetailInfo_OutputHydrogen(this StringBuilder s, MyResourceSourceComponent source)
        {
            if(source == null)
                return s;

            var current = source.CurrentOutputByType(MyResourceDistributorComponent.HydrogenId);
            var max = source.MaxOutputByType(MyResourceDistributorComponent.HydrogenId);

            return s.Append("Output H2: ").VolumeFormat(current).Append(" / ").VolumeFormat(max).Append('\n');
        }

        public static StringBuilder DetailInfo_InputOxygen(this StringBuilder s, MyResourceSinkComponent sink)
        {
            if(sink == null)
                return s;

            var current = sink.CurrentInputByType(MyResourceDistributorComponent.OxygenId);
            var max = sink.RequiredInputByType(MyResourceDistributorComponent.OxygenId);

            return s.Append("Input O2: ").VolumeFormat(current).Append(" / ").VolumeFormat(max).Append('\n');
        }

        public static StringBuilder DetailInfo_OutputOxygen(this StringBuilder s, MyResourceSourceComponent source)
        {
            if(source == null)
                return s;

            var current = source.CurrentOutputByType(MyResourceDistributorComponent.OxygenId);
            var max = source.MaxOutputByType(MyResourceDistributorComponent.OxygenId);

            return s.Append("Output O2: ").VolumeFormat(current).Append(" / ").VolumeFormat(max).Append('\n');
        }

        public static StringBuilder DetailInfo_InputGasList(this StringBuilder s, MyResourceSinkComponent sink)
        {
            if(sink == null)
                return s;

            foreach(var res in sink.AcceptedResources)
            {
                if(res == MyResourceDistributorComponent.ElectricityId)
                    continue;

                var current = sink.CurrentInputByType(res);
                var max = sink.RequiredInputByType(res);

                s.Append("Input ");

                if(res == MyResourceDistributorComponent.HydrogenId)
                    s.Append("H2");
                else if(res == MyResourceDistributorComponent.OxygenId)
                    s.Append("O2");
                else
                    s.Append(res.SubtypeName);

                s.Append(": ").VolumeFormat(current).Append(" / ").VolumeFormat(max).Append('\n');
            }

            return s;
        }

        public static StringBuilder DetailInfo_OutputGasList(this StringBuilder s, MyResourceSourceComponent source)
        {
            if(source == null)
                return s;

            foreach(var res in source.ResourceTypes)
            {
                if(res == MyResourceDistributorComponent.ElectricityId)
                    continue;

                var current = source.CurrentOutputByType(res);
                var max = source.MaxOutputByType(res);

                s.Append("Output ");

                if(res == MyResourceDistributorComponent.HydrogenId)
                    s.Append("H2");
                else if(res == MyResourceDistributorComponent.OxygenId)
                    s.Append("O2");
                else
                    s.Append(res.SubtypeName);

                s.Append(": ").VolumeFormat(current).Append(" / ").VolumeFormat(max).Append('\n');
            }

            return s;
        }
        #endregion Detailed info formats
    }
}