using System.Text;
using VRageMath;

namespace Digi.ConfigLib
{
    public class IntegerSetting : SettingBase<int>
    {
        public readonly int Min;
        public readonly int Max;

        public IntegerSetting(ConfigHandler configInstance, string name, int defaultValue, int min, int max, params string[] commentLines)
            : base(configInstance, name, defaultValue, commentLines)
        {
            Min = min;
            Max = max;
        }

        public override void ReadValue(string valueString, out string error)
        {
            error = null;
            int tmp;
            if(int.TryParse(valueString, out tmp))
                Value = MathHelper.Clamp(tmp, Min, Max);
            else
                error = "expected an integer (no decimal)";
        }

        protected override void AppendDefaultValue(StringBuilder output)
        {
            if(!AddDefaultValueComment && !AddValidRangeComment)
                return;

            output.Append(ConfigHandler.COMMENT_PREFIX);

            if(AddValidRangeComment)
            {
                output.Append("Value range: ").Append(Min).Append(" to ").Append(Max).Append(". ");
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
