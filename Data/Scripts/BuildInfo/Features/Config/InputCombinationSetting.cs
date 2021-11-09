using System.Text;
using Digi.ConfigLib;
using static Digi.Input.InputLib;

namespace Digi.BuildInfo.Features.Config
{
    public class InputCombinationSetting : SettingBase<Combination>
    {
        public InputCombinationSetting(ConfigHandler configInstance, string name, Combination defaultValue, params string[] commentLines)
            : base(configInstance, name, defaultValue, commentLines)
        {
        }

        public override void ReadValue(string valueString, out string error)
        {
            Combination combination = Combination.Create(Name, valueString, out error);
            if(combination != null)
                Value = combination;
            else if(error == null)
                error = "Unknown error";
        }

        public override void WriteValue(StringBuilder output)
        {
            output.Append(Value.CombinationString);
        }

        public override void WriteDefaultValue(StringBuilder output)
        {
            output.Append(DefaultValue.CombinationString);
        }

        public override void SaveSetting(StringBuilder output)
        {
            AppendComments(output);
            output.Append(ConfigHandler.COMMENT_PREFIX).Append(" (see inputs and instructions at the bottom of this file)").AppendLine();

            output.Append(Name).Append(' ').Append(ConfigHandler.VALUE_SEPARATOR).Append(' ');
            WriteValue(output);
            output.AppendLine();
        }
    }
}
