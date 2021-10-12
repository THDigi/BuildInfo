using System;
using System.Collections.Generic;
using System.Text;

namespace Digi.ConfigLib
{
    public struct EnumData
    {
        public readonly string Name;
        public readonly int Value;
        public readonly string Comment;

        public EnumData(string name, int value, string comment)
        {
            Name = name;
            Value = value;
            Comment = comment;
        }
    }

    public class EnumSetting<TEnum> : SettingBase<int> where TEnum : struct
    {
        public TEnum ValueEnum { get; private set; }
        public string ValueName { get; private set; }

        /// <summary>
        /// Only useful if the enum is sequential
        /// </summary>
        public readonly int HighestValue;

        public readonly Dictionary<TEnum, EnumData> EnumInfo = new Dictionary<TEnum, EnumData>();

        public EnumSetting(ConfigHandler configInstance, string name, TEnum defaultValue, params string[] commentLines)
            : base(configInstance, name, (int)(object)defaultValue, commentLines)
        {
            int[] enumValues = (int[])Enum.GetValues(typeof(TEnum));

            HighestValue = enumValues[enumValues.Length - 1];

            for(int i = 0; i < enumValues.Length; i++)
            {
                int intValue = enumValues[i];
                TEnum enumValue = (TEnum)(object)intValue;
                EnumInfo[enumValue] = new EnumData(enumValue.ToString(), intValue, null);
            }

            ValueAssigned += OnValueAssigned;
            OnValueAssigned(0, Value, this);
        }

        public void SetEnumComment(TEnum enumName, string comment)
        {
            EnumData data;
            if(!EnumInfo.TryGetValue(enumName, out data))
                throw new Exception($"Unknown enum: {enumName}");

            EnumInfo[enumName] = new EnumData(data.Name, data.Value, comment);
        }

        void OnValueAssigned(int oldValue, int newValue, SettingBase<int> setting)
        {
            ValueEnum = (TEnum)(object)newValue;
            ValueName = EnumInfo[ValueEnum].Name;
        }

        public override void ReadValue(string valueString, out string error)
        {
            error = null;

            int? setValue = null;

            int num;
            if(int.TryParse(valueString, out num))
            {
                setValue = num;
            }
            else
            {
                TEnum enumValue;
                if(Enum.TryParse<TEnum>(valueString, out enumValue))
                {
                    setValue = (int)(object)enumValue;
                }
            }

            if(setValue.HasValue)
            {
                foreach(EnumData data in EnumInfo.Values)
                {
                    if(data.Value == setValue.Value)
                    {
                        Value = setValue.Value;
                        return;
                    }
                }

                error = $"parsed value {setValue.Value.ToString()} is not a known value, ignored.";
            }
            else
            {
                error = "expected an integer (no decimal) or enum value name.";
            }
        }

        public override void WriteDefaultValue(StringBuilder output)
        {
            TEnum defaultEnum = (TEnum)(object)DefaultValue;
            EnumData data = EnumInfo[defaultEnum];
            output.Append(data.Name);
        }

        public override void WriteValue(StringBuilder output)
        {
            output.Append(ValueName);
        }

        protected override void AppendDefaultValue(StringBuilder output)
        {
            if(!AddDefaultValueComment && !AddValidRangeComment)
                return;

            output.Append(ConfigHandler.COMMENT_PREFIX);

            if(AddDefaultValueComment)
            {
                output.Append("Default value: ");
                WriteDefaultValue(output);
            }

            output.AppendLine();
        }

        protected override void AppendComments(StringBuilder output)
        {
            foreach(string line in CommentLines)
            {
                if(string.IsNullOrEmpty(line))
                    output.AppendLine();
                else
                    output.Append(ConfigHandler.COMMENT_PREFIX).Append(line).AppendLine();
            }

            foreach(EnumData data in EnumInfo.Values)
            {
                output.Append(ConfigHandler.COMMENT_PREFIX).Append("    ").Append(data.Value).Append(" or ").Append(data.Name);

                if(!string.IsNullOrEmpty(data.Comment))
                    output.Append("  (").Append(data.Comment).Append(")");

                output.AppendLine();
            }

            AppendDefaultValue(output);
        }
    }
}
