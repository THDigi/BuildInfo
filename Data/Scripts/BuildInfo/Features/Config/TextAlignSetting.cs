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
            int flags = 0;

            if(valueString.Contains("RIGHT"))
                flags |= (int)TextAlignFlags.Right;
            else
                flags |= (int)TextAlignFlags.Left;

            if(valueString.Contains("TOP"))
                flags |= (int)TextAlignFlags.Top;
            else
                flags |= (int)TextAlignFlags.Bottom;

            SetValue(flags);
        }

        public override void WriteValue(StringBuilder output)
        {
            output.Append(((TextAlignFlags)Value).GetName());
        }

        public override void WriteDefaultValue(StringBuilder output)
        {
            output.Append(((TextAlignFlags)DefaultValue).GetName());
        }

        public void Set(TextAlignFlags flag, bool on)
        {
            if(on)
                SetValue(Value | (int)flag);
            else
                SetValue(Value & ~(int)flag);
        }

        public bool IsSet(TextAlignFlags flag)
        {
            return (Value & (int)flag) != 0;
        }
    }
}
