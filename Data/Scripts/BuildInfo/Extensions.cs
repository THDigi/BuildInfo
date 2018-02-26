using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Input;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo
{
    static class Extensions
    {
        // HACK copy of TrimTrailingWhitespace() since it's not whitelisted in modAPI
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

        public static StringBuilder Separator(this StringBuilder s)
        {
            return s.Append(", ");
        }

        public static void EndLine(this StringBuilder s)
        {
            BuildInfo.instance.EndAddedLines();
        }

        public static StringBuilder BoolFormat(this StringBuilder s, bool b)
        {
            return s.Append(b ? "Yes" : "No");
        }

        public static StringBuilder SetTextAPIColor(this StringBuilder s, Color color)
        {
            if(BuildInfo.instance.TextAPIEnabled)
                s.Append("<color=").Append(color.R).Append(',').Append(color.G).Append(',').Append(color.B).Append('>');

            return s;
        }

        public static StringBuilder ResetTextAPIColor(this StringBuilder s)
        {
            if(BuildInfo.instance.TextAPIEnabled)
            {
                var color = BuildInfo.instance.COLOR_NORMAL;
                s.Append("<color=").Append(color.R).Append(',').Append(color.G).Append(',').Append(color.B).Append('>');
            }

            return s;
        }

        public static StringBuilder ResourcePriority(this StringBuilder s, string groupName, bool hardcoded = false) // HACK some ResourceSinkGroup are string type for SOME reason
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

            if(groupId == null || !BuildInfo.instance.resourceGroupPriority.TryGetValue(groupId, out data))
                s.Append("(Undefined)");
            else
                s.Append(groupId.String).Append(" (").Append(data.priority).Append("/").Append(data.def.IsSource ? BuildInfo.instance.resourceSourceGroups : BuildInfo.instance.resourceSinkGroups).Append(")");

            return s;
        }

        public static StringBuilder ForceFormat(this StringBuilder s, float N)
        {
            if(N > 1000000)
                return s.NumFormat(N / 1000000, 3).Append(" MN");

            if(N > 1000)
                return s.NumFormat(N / 1000, 3).Append(" kN");

            return s.NumFormat(N, 3).Append(" N");
        }

        public static StringBuilder RotationSpeed(this StringBuilder s, float radPerSecond)
        {
            return s.Append(Math.Round(MathHelper.ToDegrees(radPerSecond), 2)).Append("°/s");
        }

        public static StringBuilder TorqueFormat(this StringBuilder s, float N)
        {
            if(N > 1000000)
                return s.NumFormat(N / 1000000, 3).Append(" MN-m");

            if(N > 1000)
                return s.NumFormat(N / 1000, 3).Append(" kN-m");

            return s.NumFormat(N, 3).Append("N-m");
        }

        public static StringBuilder PowerFormat(this StringBuilder s, float MW)
        {
            float W = MW * 1000000f;

            if(W > 1000000)
                return s.NumFormat(MW, 3).Append(" MW");
            if(W > 1000)
                return s.NumFormat(W / 1000f, 3).Append(" kW");

            return s.NumFormat(W, 3).Append(" W");
        }

        public static StringBuilder PowerStorageFormat(this StringBuilder s, float MWh)
        {
            return s.PowerFormat(MWh).Append("h");
        }

        public static StringBuilder DistanceFormat(this StringBuilder s, float m)
        {
            if(m > 1000)
                return s.NumFormat(m / 1000, 2).Append("km");

            if(m < 10)
                return s.NumFormat(m, 2).Append("m");

            return s.Append((int)m).Append("m");
        }

        public static StringBuilder DistanceRangeFormat(this StringBuilder s, float m1, float m2)
        {
            if(m1 > 1000)
                return s.NumFormat(m1 / 1000, 2).Append("~").NumFormat(m2 / 1000, 2).Append(" km");

            if(m1 < 10)
                return s.NumFormat(m1, 2).Append("~").NumFormat(m2, 2).Append(" m");

            return s.Append((int)m1).Append("~").Append((int)m2).Append(" m");
        }

        public static StringBuilder MassFormat(this StringBuilder s, float kg)
        {
            if(kg > 1000000)
                return s.Append(Math.Round(kg / 1000000, 2)).Append(" Mt");

            if(kg > 1000)
                return s.Append(Math.Round(kg / 1000, 2)).Append(" t");

            if(kg < 1f)
                return s.Append((int)(kg * 1000)).Append(" g");

            return s.Append(Math.Round(kg, 2)).Append(" kg");
        }

        public static StringBuilder VolumeFormat(this StringBuilder s, float l)
        {
            return s.NumFormat(l, 3).Append(" l");
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
                s.SetTextAPIColor(BuildInfo.instance.COLOR_UNIMPORTANT).Append(" (x").Append(Math.Round(mul, 2)).Append(")").ResetTextAPIColor();

            if(types == null && items == null)
                types = BuildInfo.instance.DEFAULT_ALLOWED_TYPES;

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
            if(seconds >= 3600)
                return s.AppendFormat("{0:0}h {1:0}m {2:0.##}s", (seconds / 3600), (seconds / 60), (seconds % 60));

            if(seconds >= 60)
                return s.AppendFormat("{0:0}m {1:0.##}s", (seconds / 60), (seconds % 60));

            return s.AppendFormat("{0:0.##}s", seconds);
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

        public static StringBuilder SpeedFormat(this StringBuilder s, float mps)
        {
            return s.NumFormat(mps, 2).Append(" m/s");
        }

        public static StringBuilder PercentFormat(this StringBuilder s, float ratio)
        {
            return s.Append((int)(ratio * 100)).Append('%');
        }

        public static StringBuilder MultiplierFormat(this StringBuilder s, float mul)
        {
            if(Math.Abs(mul - 1f) > 0.001f)
                s.Append(" (x").NumFormat(mul, 2).Append(")");

            return s;
        }

        public static StringBuilder IdTypeFormat(this StringBuilder s, MyObjectBuilderType type)
        {
            var typeName = type.ToString();
            var index = typeName.IndexOf('_') + 1;
            s.Append(typeName, index, typeName.Length - index);
            return s;
        }

        public static StringBuilder IdTypeSubtypeFormat(this StringBuilder s, MyDefinitionId id)
        {
            s.IdTypeFormat(id.TypeId).Append("/").Append(id.SubtypeName);
            return s;
        }

        public static StringBuilder ModFormat(this StringBuilder s, MyModContext context)
        {
            s.Append(context.ModName);

            // HACK workaround for MyModContext not having workshop ID as ulong... only filename which is unreliable in determining that
            var mod = MyAPIGateway.Session.Mods.First((m) => m.Name == context.ModId);
            if(mod.PublishedFileId != 0)
                s.SetTextAPIColor(BuildInfo.instance.COLOR_UNIMPORTANT).Append("(WorkshopID: ").Append(mod.PublishedFileId).Append(")");

            return s;
        }

        public static StringBuilder NumFormat(this StringBuilder s, float f, int d)
        {
            return s.Append(Math.Round(f, d));
        }

        public static string ToTextAPIColor(this Color color)
        {
            return $"<color={color.R},{color.G},{color.B}>";
        }

        /// <summary>
        /// Gets the key/button name assigned to the specified control.
        /// </summary>
        public static string GetControlAssignedName(this MyStringId controlId)
        {
            var control = MyAPIGateway.Input.GetGameControl(controlId);

            if(control.GetKeyboardControl() != MyKeys.None)
                return control.GetKeyboardControl().ToString();
            else if(control.GetSecondKeyboardControl() != MyKeys.None)
                return control.GetSecondKeyboardControl().ToString();
            else if(control.GetMouseControl() != MyMouseButtonsEnum.None)
                return MyAPIGateway.Input.GetName(control.GetMouseControl());

            return null;
        }
    }
}