using System.Text;
using VRageMath;

namespace Digi.ConfigLib
{
    public class FloatSetting : SettingBase<float>
    {
        public readonly float Min;
        public readonly float Max;

        public FloatSetting(ConfigHandler configInstance, string name, float defaultValue, float min, float max, params string[] commentLines)
            : base(configInstance, name, defaultValue, commentLines)
        {
            Min = min;
            Max = max;
        }

        public override void ReadValue(string valueString, out string error)
        {
            error = null;
            float tmp;
            if(float.TryParse(valueString, out tmp))
                SetValue(MathHelper.Clamp(tmp, Min, Max));
            else
                error = "expected a number";
        }

        public override void WriteValue(StringBuilder output)
        {
            output.Append(Value.ToString(FLOAT_FORMAT));
        }

        public override void WriteDefaultValue(StringBuilder output)
        {
            output.Append(DefaultValue.ToString(FLOAT_FORMAT));
        }

        protected override void AppendDefaultValue(StringBuilder output)
        {
            if(!AddDefaultValueComment && !AddValidRangeComment)
                return;

            output.Append(ConfigHandler.COMMENT_PREFIX);

            if(AddValidRangeComment)
            {
                output.Append("Value range: ").Append(Min.ToString(FLOAT_FORMAT)).Append(" to ").Append(Max.ToString(FLOAT_FORMAT)).Append(". ");
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
