using System;
using System.Collections.Generic;
using System.Text;
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

namespace Digi.BuildInfo.Extensions
{
    public static class StringBuilderExtensions
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

        // HACK copy of StringBuilderExtensions_2.TrimTrailingWhitespace() since it's not whitelisted in modAPI
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

        // HACK copy of StringBuilderExtensions_Format.AppendStringBuilder() since it's not whitelisted in modAPI
        public static StringBuilder AppendSB(this StringBuilder sb, StringBuilder otherSb)
        {
            sb.EnsureCapacity(sb.Length + otherSb.Length);

            for(int i = 0; i < otherSb.Length; i++)
            {
                sb.Append(otherSb[i]);
            }

            return sb;
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

        public static StringBuilder AppendMaxLength(this StringBuilder s, string text, int maxLength)
        {
            if(text == null)
                return s.Append("(NULL)");

            if(text.Length > maxLength)
                s.AppendSubstring(text, 0, maxLength - 1).Append('…');
            else
                s.Append(text);

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

        public static void EndLine(this StringBuilder s)
        {
            BuildInfo.Instance.EndAddedLines();
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
            return s.LabelHardcoded(label, BuildInfo.Instance.COLOR_NORMAL);
        }

        public static StringBuilder Hardcoded(this StringBuilder s)
        {
            return s.Color(new Color(255, 255, 155)).Append('*');
        }

        public static StringBuilder PCUFormat(this StringBuilder s, int PCU)
        {
            if(PCU >= 500)
                s.Color(BuildInfo.Instance.COLOR_BAD);
            else if(PCU >= 200)
                s.Color(BuildInfo.Instance.COLOR_WARNING);

            s.Append("PCU: ").Append(PCU).ResetColor();
            return s;
        }

        public static StringBuilder BoolFormat(this StringBuilder s, bool b)
        {
            return s.Append(b ? "Yes" : "No");
        }

        public static StringBuilder Color(this StringBuilder s, Color color)
        {
            if(BuildInfo.Instance.TextAPIEnabled)
                s.Append("<color=").Append(color.R).Append(',').Append(color.G).Append(',').Append(color.B).Append('>');

            return s;
        }

        public static StringBuilder ResetColor(this StringBuilder s)
        {
            if(BuildInfo.Instance.TextAPIEnabled)
            {
                var color = BuildInfo.Instance.COLOR_NORMAL;
                s.Append("<color=").Append(color.R).Append(',').Append(color.G).Append(',').Append(color.B).Append('>');
            }

            return s;
        }

        public static StringBuilder ResourcePriority(this StringBuilder s, string groupName, bool hardcoded = false) // HACK some ResourceSinkGroup are string and some are MyStringHash...
        {
            return s.ResourcePriority(MyStringHash.GetOrCompute(groupName), hardcoded);
        }

        public static StringBuilder ResourcePriority(this StringBuilder s, MyStringHash groupId, bool hardcoded = false)
        {
            s.Append("Priority");

            if(hardcoded)
                s.Append('*');

            s.Append(": ");

            BuildInfo.ResourceGroupData data;

            if(groupId == null || !BuildInfo.Instance.resourceGroupPriority.TryGetValue(groupId, out data))
                s.Append("(Undefined)");
            else
                s.Append(groupId.String).Append(" (").Append(data.priority).Append("/").Append(data.def.IsSource ? BuildInfo.Instance.resourceSourceGroups : BuildInfo.Instance.resourceSinkGroups).Append(")");

            return s;
        }

        public static StringBuilder ForceFormat(this StringBuilder s, float N)
        {
            if(N >= 1000000)
                return s.Number(N / 1000000).Append(" MN");

            if(N >= 1000)
                return s.Number(N / 1000).Append(" kN");

            return s.Number(N).Append(" N");
        }

        public static StringBuilder RotationSpeed(this StringBuilder s, float radPerSecond, int digits = 2)
        {
            return s.Append(Math.Round(MathHelper.ToDegrees(radPerSecond), digits)).Append("°/s");
        }

        public static StringBuilder TorqueFormat(this StringBuilder s, float N)
        {
            if(N >= 1000000)
                return s.Number(N / 1000000).Append(" MN-m");

            if(N >= 1000)
                return s.Number(N / 1000).Append(" kN-m");

            return s.Number(N).Append("N-m");
        }

        public static StringBuilder PowerFormat(this StringBuilder s, float MW)
        {
            if(MW >= 1000000000)
                return s.Number(MW / 1000000000).Append(" PetaWatts");

            if(MW >= 1000000)
                return s.Number(MW / 1000000).Append(" TerraWatts");

            if(MW >= 1000)
                return s.Number(MW / 1000).Append(" GigaWatts");

            if(MW >= 1)
                return s.Number(MW).Append(" MW");

            if(MW >= 0.001)
                return s.Number(MW * 1000f).Append(" kW");

            return s.Number(MW * 1000000f).Append(" W");
        }

        public static StringBuilder PowerStorageFormat(this StringBuilder s, float MWh)
        {
            return s.PowerFormat(MWh).Append("h");
        }

        public static StringBuilder DistanceFormat(this StringBuilder s, float m, int digits = -1)
        {
            if(m >= 1000)
                return s.Number(m / 1000).Append("km");

            if(digits <= -1)
            {
                if(m < 10)
                    return s.RoundedNumber(m, 2).Append("m");

                return s.Append((int)m).Append("m");
            }
            else
            {
                return s.RoundedNumber(m, digits).Append("m");
            }
        }

        public static StringBuilder DistanceRangeFormat(this StringBuilder s, float m1, float m2)
        {
            if(m1 >= 1000)
                return s.Number(m1 / 1000).Append("~").Number(m2 / 1000).Append(" km");

            if(m1 < 10)
                return s.Number(m1).Append("~").Number(m2).Append(" m");

            return s.Append((int)m1).Append("~").Append((int)m2).Append(" m");
        }

        public static StringBuilder MassFormat(this StringBuilder s, float kg)
        {
            if(kg >= 1000000)
                return s.Number(kg / 1000000).Append(" Mt");

            if(kg >= 1000)
                return s.Number(kg / 1000).Append(" t");

            if(kg >= 1)
                return s.Number(kg).Append(" kg");

            if(kg >= 0.001f)
                return s.Number(kg * 1000).Append(" g");

            return s.Number(kg * 1000000).Append(" mg");
        }

        public static StringBuilder IntegrityFormat(this StringBuilder s, float integrity)
        {
            if(integrity >= 1000000000)
                return s.Number(integrity / 1000000000).Append(" G");

            if(integrity >= 1000000)
                return s.Number(integrity / 1000000).Append(" M");

            if(integrity >= 1000)
                return s.Number(integrity / 1000).Append(" k");

            return s.Number(integrity);
        }

        public static StringBuilder VolumeFormat(this StringBuilder s, float l)
        {
            if(l >= 1000)
                return s.Number(l / 1000f).Append(" m³");

            return s.Number(l).Append(" L");
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyInventoryConstraint inputConstraint, MyInventoryConstraint outputConstraint)
        {
            var types = new HashSet<MyObjectBuilderType>(inputConstraint.ConstrainedTypes);
            types.UnionWith(outputConstraint.ConstrainedTypes);

            var items = new HashSet<MyDefinitionId>(inputConstraint.ConstrainedIds);
            items.UnionWith(outputConstraint.ConstrainedIds);

            return s.InventoryFormat(volume, types: types, items: items, isWhitelist: inputConstraint.IsWhitelist); // HACK only using input constraint's whitelist status, not sure if output inventory's whitelist is needed
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyInventoryConstraint inventoryConstraint)
        {
            return s.InventoryFormat(volume,
                types: new HashSet<MyObjectBuilderType>(inventoryConstraint.ConstrainedTypes),
                items: new HashSet<MyDefinitionId>(inventoryConstraint.ConstrainedIds),
                isWhitelist: inventoryConstraint.IsWhitelist);
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, params MyObjectBuilderType[] allowedTypesParams)
        {
            return s.InventoryFormat(volume, types: new HashSet<MyObjectBuilderType>(allowedTypesParams));
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, params MyDefinitionId[] allowedItems)
        {
            return s.InventoryFormat(volume, items: new HashSet<MyDefinitionId>(allowedItems));
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, HashSet<MyObjectBuilderType> types = null, HashSet<MyDefinitionId> items = null, bool isWhitelist = true)
        {
            var mul = MyAPIGateway.Session.InventoryMultiplier;

            MyValueFormatter.AppendVolumeInBestUnit(volume * mul, s);

            if(Math.Abs(mul - 1) > 0.001f)
                s.Color(BuildInfo.Instance.COLOR_UNIMPORTANT).Append(" (x").Append(Math.Round(mul, 2)).Append(")").ResetColor();

            if(types == null && items == null)
                types = BuildInfo.Instance.DEFAULT_ALLOWED_TYPES;

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

            return s;
        }

        public static StringBuilder TimeFormat(this StringBuilder s, float seconds)
        {
            if(seconds > (60 * 60 * 24 * 365))
                return s.Append("1y+");

            var span = TimeSpan.FromSeconds(seconds);

            if(span.Days > 7)
                return s.AppendFormat("{0:0}w {1:0}d {2:0}h {3:0.#}m", (span.Days / 7), (span.Days % 7), span.Hours, (span.TotalMinutes % 60));

            if(span.Days > 0)
                return s.AppendFormat("{0:0}d {1:0}h {2:0}m {3:0.#}s", span.Days, span.Hours, span.Minutes, span.Seconds);

            if(span.Hours > 0)
                return s.AppendFormat("{0:0}h {1:0}m {2:0.#}s", span.Hours, span.Minutes, span.Seconds);

            if(span.Minutes > 0)
                return s.AppendFormat("{0:0}m {1:0.#}s", span.Minutes, (seconds % 60));

            return s.AppendFormat("{0:0.#}s", seconds);
        }

        public static StringBuilder AngleFormat(this StringBuilder s, float radians, int digits = 0)
        {
            return s.AngleFormatDeg(MathHelper.ToDegrees(radians), digits);
        }

        public static StringBuilder AngleFormatDeg(this StringBuilder s, float degrees, int digits = 0)
        {
            return s.Append(Math.Round(degrees, digits)).Append('°');
        }

        public static StringBuilder VectorFormat(this StringBuilder s, Vector3 vec)
        {
            return s.Append(vec.X).Append('x').Append(vec.Y).Append('x').Append(vec.Z);
        }

        public static StringBuilder SpeedFormat(this StringBuilder s, float metersPerSecond, int digits = 2)
        {
            return s.RoundedNumber(metersPerSecond, digits).Append(" m/s");
        }

        public static StringBuilder AccelerationFormat(this StringBuilder s, float metersPerSecond)
        {
            return s.SpeedFormat(metersPerSecond).Append('²');
        }

        public static StringBuilder ProportionToPercent(this StringBuilder s, float proportion)
        {
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

            if(typeName.EndsWith("GasProperties")) // manually fixing "GasProperties" to just "Gas"
                s.Length -= "Properties".Length;

            return s;
        }

        public static StringBuilder IdTypeSubtypeFormat(this StringBuilder s, MyDefinitionId id)
        {
            return s.Append(id.SubtypeName).Append(' ').IdTypeFormat(id.TypeId);
        }

        public static StringBuilder ModFormat(this StringBuilder s, MyModContext context)
        {
            s.Color(BuildInfo.Instance.COLOR_MOD_TITLE).AppendMaxLength(context.ModName, BuildInfo.MOD_NAME_MAX_LENGTH);

            var id = context.GetWorkshopID();

            if(id > 0)
                s.Color(BuildInfo.Instance.COLOR_UNIMPORTANT).Append(" (id: ").Append(id).Append(")");

            return s;
        }

        public static StringBuilder Number(this StringBuilder s, float value)
        {
            return s.AppendFormat("{0:###,###,###,###,###,##0.##}", value);
        }

        public static StringBuilder RoundedNumber(this StringBuilder s, float value, int digits)
        {
            return s.AppendFormat("{0:###,###,###,###,###,##0.##########}", Math.Round(value, digits));
        }

        public static StringBuilder AppendUpgrade(this StringBuilder s, MyUpgradeModuleInfo upgrade)
        {
            var modifier = Math.Round(upgrade.Modifier, 3);

            switch(upgrade.ModifierType)
            {
                case MyUpgradeModifierType.Additive: s.Append('+').Append(modifier); break;
                case MyUpgradeModifierType.Multiplicative: s.Append('x').Append(modifier); break;
                default: s.Append(modifier).Append(' ').Append(upgrade.ModifierType); break;
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
        #endregion
    }
}