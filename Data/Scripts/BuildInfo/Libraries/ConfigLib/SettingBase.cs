﻿using System;
using System.Text;

namespace Digi.ConfigLib
{
    public abstract class SettingBase<T> : ISetting
    {
        public string Name { get; private set; }

        /// <summary>
        /// Its current value. Use <see cref="SetValue(T)"/> to change it.
        /// </summary>
        public T Value { get; private set; }

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

        /// <summary>
        /// Sets the value for this setting and invokes this setting's <see cref="ValueAssigned"/> as well as the global <see cref="ConfigHandler.SettingValueSet"/>.
        /// <br />It will not automatically save the config.
        /// </summary>
        public void SetValue(T newValue)
        {
            T oldValue = Value;
            Value = newValue;

            try
            {
                ValueAssigned?.Invoke(oldValue, newValue, this);
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
            SetValue(DefaultValue);
        }

        public abstract void ReadValue(string valueString, out string error);

        public virtual void BeforeMultiline(string valueOnKeyLine)
        {
        }

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

        public string GetDescription()
        {
            StringBuilder sb = new StringBuilder(512);
            AppendComments(sb, commentPrefix: false);
            return sb.ToString();
        }

        protected virtual void AppendComments(StringBuilder output, bool commentPrefix = true)
        {
            foreach(string line in CommentLines)
            {
                if(string.IsNullOrEmpty(line))
                    output.AppendLine();
                else
                    output.Append(commentPrefix ? ConfigHandler.COMMENT_PREFIX : string.Empty).Append(line).AppendLine();
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
