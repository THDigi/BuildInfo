using System.Text;
using Digi.ConfigLib;

namespace Digi.BuildInfo.Features.Config
{
    public class TextAlignSetting : SettingBase<int>
    {
        public TextAlignSetting(ConfigHandler configInstance, string name, TextAlignFlags defaultValue, params string[] commentLines)
            : base(configInstance, name, (int)defaultValue, commentLines)
        {
        }

        public override void ReadValue(string valueString, out string error)
        {
            error = null;
            valueString = valueString.ToUpper();
            Value = 0;

            if(valueString.Contains("RIGHT"))
                Value |= (int)TextAlignFlags.Right;
            else
                Value |= (int)TextAlignFlags.Left;

            if(valueString.Contains("TOP"))
                Value |= (int)TextAlignFlags.Top;
            else
                Value |= (int)TextAlignFlags.Bottom;
        }

        public override void WriteValue(StringBuilder output)
        {
            output.Append((TextAlignFlags)Value);
        }

        public override void WriteDefaultValue(StringBuilder output)
        {
            output.Append((TextAlignFlags)DefaultValue);
        }

        public void Set(TextAlignFlags flag, bool on)
        {
            if(on)
                Value |= (int)flag;
            else
                Value &= ~(int)flag;
        }

        public bool IsSet(TextAlignFlags flag)
        {
            return (Value & (int)flag) != 0;
        }
    }
}
