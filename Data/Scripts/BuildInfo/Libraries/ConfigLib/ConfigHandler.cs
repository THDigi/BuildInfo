using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Sandbox.ModAPI;

namespace Digi.ConfigLib
{
    public sealed class ConfigHandler
    {
        public readonly string FileName;

        public readonly IntegerSetting ConfigVersion;

        /// <summary>
        /// NOTE: Already contains some comments, feel free to remove them.
        /// </summary>
        public readonly List<string> HeaderComments = new List<string>();
        public readonly List<string> FooterComments = new List<string>();

        public readonly Dictionary<string, ISetting> Settings = new Dictionary<string, ISetting>(StringComparer.InvariantCultureIgnoreCase);
        public readonly Dictionary<string, ISetting> SettingsAlias = new Dictionary<string, ISetting>(StringComparer.InvariantCultureIgnoreCase);

        private readonly char[] separatorCache = { VALUE_SEPARATOR };
        private readonly StringBuilder sb;

        public const char VALUE_SEPARATOR = '=';
        public const string COMMENT_PREFIX = "# ";
        public const char MULTILINE_PREFIX = '|';
        public const StringComparison KEY_COMPARE_TYPE = StringComparison.InvariantCultureIgnoreCase;

        /// <summary>
        /// After settings were succesfully loaded.
        /// </summary>
        public event Action SettingsLoaded;

        /// <summary>
        /// Before settings are serialized and saved to file.
        /// </summary>
        public event Action SettingsSaving;

        /// <summary>
        /// When any setting's value is set.
        /// </summary>
        public event Action<ISetting> SettingValueSet;

        public ConfigHandler(string fileName, int configVersion, int expectedExportCharacters)
        {
            sb = new StringBuilder(expectedExportCharacters);
            FileName = fileName;

            HeaderComments.Add($"Config for {Log.ModName}.");
            HeaderComments.Add($"Lines starting with {COMMENT_PREFIX} are comments. All values are case and space insensitive unless otherwise specified.");
            HeaderComments.Add("NOTE: This file gets overwritten after being loaded.");

            ConfigVersion = new IntegerSetting(this, "ConfigVersion", defaultValue: configVersion, min: 0, max: int.MaxValue, commentLines: new string[]
            {
                "Used for partial config edits for compatibility.",
                "Do not change."
            });
            ConfigVersion.AddDefaultValueComment = false;
            ConfigVersion.AddValidRangeComment = false;
        }

        public void ResetToDefaults()
        {
            foreach(ISetting setting in Settings.Values)
            {
                if(object.ReferenceEquals(setting, ConfigVersion))
                    continue; // don't affect config version

                setting.ResetToDefault();
            }
        }

        public bool LoadFromFile()
        {
            bool success = false;

            try
            {
                ResetToDefaults();

                if(MyAPIGateway.Utilities.FileExistsInLocalStorage(FileName, typeof(ConfigHandler)))
                {
                    using(TextReader file = MyAPIGateway.Utilities.ReadFileInLocalStorage(FileName, typeof(ConfigHandler)))
                    {
                        string line;
                        int lineNumber = 0;
                        ISetting setting = null;

                        while((line = file.ReadLine()) != null)
                        {
                            ++lineNumber;

                            if(line.Length == 0)
                                continue;

                            int index = line.IndexOf(COMMENT_PREFIX);

                            if(index > -1)
                                line = (index == 0 ? "" : line.Substring(0, index));

                            if(line.Length == 0)
                                continue;

                            if(setting != null && setting.IsMultiLine && line[0] == MULTILINE_PREFIX)
                            {
                                string value = line.Substring(1);
                                ReadLine(setting, value, lineNumber);
                            }
                            else
                            {
                                setting = null;
                                string[] args = line.Split(separatorCache, 2);

                                if(args.Length != 2)
                                {
                                    Log.Error($"{FileName} unknown format on line #{lineNumber.ToString()}: '{line}'", Log.PRINT_MESSAGE);
                                    continue;
                                }

                                string key = args[0].Trim();

                                if(Settings.TryGetValue(key, out setting) || SettingsAlias.TryGetValue(key, out setting))
                                {
                                    string value = args[1];

                                    if(setting.IsMultiLine)
                                    {
                                        setting.BeforeMultiline(value);
                                    }
                                    else
                                    {
                                        ReadLine(setting, value, lineNumber);
                                    }
                                }
                            }
                        }
                    }

                    success = true;
                }

                SettingsLoaded?.Invoke();

                ConfigVersion.ResetToDefault(); // update config version
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            return success;
        }

        private void ReadLine(ISetting setting, string value, int lineNumber)
        {
            string error;
            setting.ReadValue(value, out error);

            if(error != null)
                Log.Error($"{FileName} line #{lineNumber.ToString()} has an error: {error}", Log.PRINT_MESSAGE);
        }

        public void SaveToFile()
        {
            try
            {
                SettingsSaving?.Invoke();

                sb.Clear();

                for(int i = 0; i < HeaderComments.Count; i++)
                {
                    sb.Append(COMMENT_PREFIX).Append(HeaderComments[i]).AppendLine();
                }

                sb.AppendLine();
                sb.AppendLine();

                foreach(ISetting setting in Settings.Values)
                {
                    if(object.ReferenceEquals(setting, ConfigVersion))
                        continue; // config version is added last

                    setting.SaveSetting(sb);

                    sb.AppendLine();
                }

                sb.AppendLine();

                for(int i = 0; i < FooterComments.Count; i++)
                {
                    sb.Append(COMMENT_PREFIX).Append(FooterComments[i]).AppendLine();
                }

                sb.AppendLine();
                sb.AppendLine();

                ConfigVersion.SaveSetting(sb);

                using(TextWriter file = MyAPIGateway.Utilities.WriteFileInLocalStorage(FileName, typeof(ConfigHandler)))
                {
                    file.Write(sb.ToString());
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public void InvokeSettingValueSet(ISetting setting)
        {
            SettingValueSet?.Invoke(setting);
        }
    }
}
