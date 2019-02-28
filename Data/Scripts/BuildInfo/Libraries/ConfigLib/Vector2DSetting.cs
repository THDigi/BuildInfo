using System;
using System.Text;
using VRageMath;

namespace Digi.ConfigLib
{
    public class Vector2DSetting : SettingBase<Vector2D>
    {
        public readonly Vector2D Min;
        public readonly Vector2D Max;

        public Vector2DSetting(ConfigHandler configInstance, string name, Vector2D defaultValue, Vector2D min, Vector2D max, params string[] commentLines)
            : base(configInstance, name, defaultValue, commentLines)
        {
            Min = min;
            Max = max;
        }

        public override void ReadValue(string valueString, out string error)
        {
            error = null;
            double x, y;
            var coords = valueString.Split(',');
            if(coords.Length == 2 && double.TryParse(coords[0], out x) && double.TryParse(coords[1], out y))
                Value = Vector2D.Clamp(new Vector2D(x, y), Min, Max);
            else
                error = "expected format: 0.0, 0.0";
        }

        public override void WriteValue(StringBuilder output)
        {
            output.Append(Value.X.ToString(FLOAT_FORMAT)).Append(", ").Append(Value.Y.ToString(FLOAT_FORMAT));
        }

        public override void WriteDefaultValue(StringBuilder output)
        {
            output.Append(DefaultValue.X.ToString(FLOAT_FORMAT)).Append(", ").Append(DefaultValue.Y.ToString(FLOAT_FORMAT));
        }

        protected override void AppendDefaultValue(StringBuilder output)
        {
            if(!AddValidRangeComment && !AddDefaultValueComment)
                return;

            output.Append(ConfigHandler.COMMENT_PREFIX);

            if(AddValidRangeComment)
            {
                output.Append("Value range: ");
                output.Append(Min.X.ToString(FLOAT_FORMAT)).Append(", ").Append(Min.Y.ToString(FLOAT_FORMAT));
                output.Append(" to ");
                output.Append(Max.X.ToString(FLOAT_FORMAT)).Append(", ").Append(Max.Y.ToString(FLOAT_FORMAT));
                output.Append(". ");
            }

            if(AddDefaultValueComment)
            {
                output.Append("Default value: ");
                WriteDefaultValue(output);
            }

            output.AppendLine();
        }
    }
}
