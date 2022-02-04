using System;
using System.Text;

namespace Digi.ConfigLib
{
    public abstract class SettingBase<T> : ISetting
    {
        public string Name { get; private set; }

        private T _value;
        public T Value
        {
            get { return _value; }
            set
            {
                T oldValue = _value;
                _value = value;

                try
                {
                    ValueAssigned?.Invoke(oldValue, _value, this);
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }

                try
                {
                    ConfigInstance?.InvokeSettingValueSet(this);
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public readonly T DefaultValue;
        public bool IsMultiLine { get; protected set; }
        public bool AddDefaultValueComment { get; set; } = true;
        public bool AddValidRangeComment { get; set; } = true;

        /// <summary>
        /// Called when Value is assigned regardless if the new value is different.
        /// Also always gets called in first update, to simplify client code.
        /// </summary>
        public event ValueChangedDel ValueAssigned;
        public delegate void ValueChangedDel(T oldValue, T newValue, SettingBase<T> setting);

        protected readonly string[] CommentLines;
        protected readonly ConfigHandler ConfigInstance;

        protected const string FLOAT_FORMAT = "0.0#########";

        public SettingBase(ConfigHandler configInstance, string name, T defaultValue, params string[] commentLines)
        {
            ConfigInstance = configInstance;
            Name = name;
            DefaultValue = defaultValue;
            CommentLines = commentLines;

            configInstance.Settings.Add(Name, this);
        }

        public void AddCompatibilityNames(string name1, string name2 = null, string name3 = null, string name4 = null, string name5 = null, string name6 = null)
        {
            if(name1 != null) ConfigInstance.SettingsAlias.Add(name1, this);
            if(name2 != null) ConfigInstance.SettingsAlias.Add(name2, this);
            if(name3 != null) ConfigInstance.SettingsAlias.Add(name3, this);
            if(name4 != null) ConfigInstance.SettingsAlias.Add(name4, this);
            if(name5 != null) ConfigInstance.SettingsAlias.Add(name5, this);
            if(name6 != null) ConfigInstance.SettingsAlias.Add(name6, this);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
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
            output.Append(Value.ToString());
        }

        public virtual void WriteDefaultValue(StringBuilder output)
        {
            output.Append(DefaultValue.ToString());
        }

        public virtual void SaveSetting(StringBuilder output)
        {
            AppendComments(output);

            output.Append(Name).Append(' ').Append(ConfigHandler.VALUE_SEPARATOR).Append(' ');
            WriteValue(output);
            output.AppendLine();
        }

        protected virtual void AppendComments(StringBuilder output)
        {
            foreach(string line in CommentLines)
            {
                if(string.IsNullOrEmpty(line))
                    output.AppendLine();
                else
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

        public void TriggerValueSetEvent()
        {
            try
            {
                ValueAssigned?.Invoke(Value, Value, this);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
