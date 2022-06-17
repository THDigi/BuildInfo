using System;
using System.Text;

namespace Digi.ConfigLib
{
    public class FlagsSetting<TFlags> : SettingBase<int> where TFlags : struct
    {
        public TFlags ValueEnum { get; set; }

        public FlagsSetting(ConfigHandler configInstance, string name, TFlags defaultValue, params string[] commentLines)
            : base(configInstance, name, GetDefault(defaultValue), commentLines)
        {
            IsMultiLine = true;
            ValueAssigned += OnValueAssigned;
        }

        /// <summary>
        /// If default is int.MaxValue then it generates default from all flags.
        /// This allows <see cref="SettingBase{T}.Value"/> to be 0 when it has no flags picked.
        /// </summary>
        static int GetDefault(TFlags flags)
        {
            int defaultValue = Convert.ToInt32(flags);

            if(defaultValue == int.MaxValue)
            {
                int[] values = (int[])Enum.GetValues(typeof(TFlags));

                defaultValue = 0;

                foreach(int value in values)
                {
                    if(value == 0 || value == int.MaxValue)
                        continue;

                    defaultValue |= value;
                }
            }

            return defaultValue;
        }

        void OnValueAssigned(int oldValue, int newValue, SettingBase<int> setting)
        {
            ValueEnum = (TFlags)(object)newValue;
        }

        bool _ignoreValues;
        public override void BeforeMultiline(string valueOnKeyLine)
        {
            // no longer doing this because newly added flags would start turned off for players with existing configs.
            //Value = 0;

            _ignoreValues = false;

            valueOnKeyLine = valueOnKeyLine.Trim();

            if(valueOnKeyLine.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                _ignoreValues = true;
                Value = 0;
            }
            else if(valueOnKeyLine.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                _ignoreValues = true;
                Value = 0;
                int[] values = (int[])Enum.GetValues(typeof(TFlags));

                foreach(int value in values)
                {
                    if(value == 0 || value == int.MaxValue)
                        continue;

                    Value |= value;
                }
            }
            else if(valueOnKeyLine.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                _ignoreValues = true;
                Value = DefaultValue;
            }
        }

        // this gets executed for every line value
        public override void ReadValue(string valueString, out string error)
        {
            error = null;

            if(_ignoreValues)
                return;

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
            output.Append(ConfigHandler.COMMENT_PREFIX).Append("To change all flags at once, after '=' add one of these: none, all, default").AppendLine();

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
