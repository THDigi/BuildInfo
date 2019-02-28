using System.Text;

namespace Digi.ConfigLib
{
    public abstract class SettingBase<T> : ISetting
    {
        public string Name { get; private set; }
        public T Value { get; set; }
        public readonly T DefaultValue;
        public bool IsMultiLine { get; protected set; }
        public bool AddDefaultValueComment { get; set; } = true;
        public bool AddValidRangeComment { get; set; } = true;

        protected readonly string[] commentLines;
        protected readonly ConfigHandler configInstance;

        protected const string FLOAT_FORMAT = "0.0#########";

        public SettingBase(ConfigHandler configInstance, string name, T defaultValue, params string[] commentLines)
        {
            this.configInstance = configInstance;
            this.Name = name;
            this.DefaultValue = defaultValue;
            this.commentLines = commentLines;

            configInstance.Settings.Add(Name, this);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            WriteValue(sb);
            return sb.ToString();
        }

        public void ResetToDefault()
        {
            Value = DefaultValue;
        }

        public abstract void ReadValue(string valueString, out string error);

        public virtual void WriteValue(StringBuilder output)
        {
            output.Append(Value);
        }

        public virtual void WriteDefaultValue(StringBuilder output)
        {
            output.Append(DefaultValue);
        }

        public virtual void SaveSetting(StringBuilder output)
        {
            AppendComments(output);

            output.Append(Name).Append(' ').Append(ConfigHandler.VALUE_SEPARATOR).Append(' ');
            WriteValue(output);
            output.AppendLine();
        }

        protected void AppendComments(StringBuilder output)
        {
            foreach(var line in commentLines)
            {
                output.Append(ConfigHandler.COMMENT_PREFIX).Append(line).AppendLine();
            }

            AppendDefaultValue(output);
        }

        protected virtual void AppendDefaultValue(StringBuilder output)
        {
            if(AddDefaultValueComment)
            {
                output.Append(ConfigHandler.COMMENT_PREFIX).Append("Default value: ");
                WriteDefaultValue(output);
                output.AppendLine();
            }
        }

        public static implicit operator T(SettingBase<T> setting)
        {
            return setting.Value;
        }
    }
}
