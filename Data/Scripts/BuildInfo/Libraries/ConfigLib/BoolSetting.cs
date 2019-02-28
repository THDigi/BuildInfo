using System.Text;

namespace Digi.ConfigLib
{
    public class BoolSetting : SettingBase<bool>
    {
        public BoolSetting(ConfigHandler configInstance, string name, bool defaultValue, params string[] commentLines)
            : base(configInstance, name, defaultValue, commentLines)
        {
        }

        public override void ReadValue(string valueString, out string error)
        {
            error = null;
            bool tmp;
            if(bool.TryParse(valueString, out tmp))
                Value = tmp;
            else
                error = "expected 'true' or 'false'";
        }

        public override void WriteValue(StringBuilder output)
        {
            output.Append(Value ? "true" : "false");
        }

        public override void WriteDefaultValue(StringBuilder output)
        {
            output.Append(DefaultValue ? "true" : "false");
        }
    }
}
