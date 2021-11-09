using System;
using System.Text;

namespace Digi.ConfigLib
{
    public class FlagsSetting<TFlags> : SettingBase<int> where TFlags : struct
    {
        public FlagsSetting(ConfigHandler configInstance, string name, TFlags defaultValue, params string[] commentLines)
            : base(configInstance, name, Convert.ToInt32(defaultValue), commentLines)
        {
            IsMultiLine = true;
        }

        // this gets executed for every line value
        public override void ReadValue(string valueString, out string error)
        {
            error = null;
            int switchIndex = valueString.IndexOf('-');
            bool set = false;

            if(switchIndex == -1)
            {
                switchIndex = valueString.IndexOf('+');
                set = true;
            }

            if(switchIndex == -1)
            {
                error = "+ or - prefix expected to determine ON or OFF state";
                return;
            }

            string name = valueString.Substring(switchIndex + 1).Trim();
            string[] names = Enum.GetNames(typeof(TFlags));

            for(int i = 0; i < names.Length; ++i)
            {
                if(names[i] == name)
                {
                    int[] values = (int[])Enum.GetValues(typeof(TFlags));

                    if(set)
                        Value = Value | values[i];
                    else
                        Value = Value & ~values[i];

                    return;
                }
            }
        }

        public override void WriteValue(StringBuilder output)
        {
            throw new Exception("Not implemented and should not be used!");
        }

        public override void WriteDefaultValue(StringBuilder output)
        {
            output.Append("(all enabled)");
        }

        public override void SaveSetting(StringBuilder output)
        {
            AppendComments(output);

            output.Append(ConfigHandler.COMMENT_PREFIX).Append("Change + to - on any item to disable it.").AppendLine();

            output.Append(Name).Append(' ').Append(ConfigHandler.VALUE_SEPARATOR).AppendLine();

            int[] values = (int[])Enum.GetValues(typeof(TFlags));
            string[] names = Enum.GetNames(typeof(TFlags));
            int value = Convert.ToInt32(Value);

            for(int i = 0; i < names.Length; ++i)
            {
                string name = names[i];

                if(name == "None" || name == "All")
                    continue;

                bool set = (value & values[i]) != 0;
                output.Append(ConfigHandler.MULTILINE_PREFIX).Append("    ").Append(set ? '+' : '-').Append(' ').Append(name).AppendLine();
            }
        }

        public void Set(int flag, bool set)
        {
            if(set)
                Value |= flag;
            else
                Value &= ~flag;
        }

        public bool IsSet(int flag)
        {
            return (flag & Value) != 0;
        }
    }
}
