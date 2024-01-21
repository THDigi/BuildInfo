using System;
using Digi.BuildInfo.Systems;
using Digi.ConfigLib;
using Digi.Input;

namespace Digi.BuildInfo.Features.Config
{
    public enum TextShowMode
    {
        Off = 0,
        AlwaysOn = 1,
        ShowOnPress = 2,
        HudHints = 3,
    }

    /// <summary>
    /// For reading backwards compatible values (true/false).
    /// </summary>
    public class TextShowModeSetting : EnumSetting<TextShowMode>
    {
        public TextShowModeSetting(ConfigHandler configInstance, string name, TextShowMode defaultValue, params string[] commentLines)
            : base(configInstance, name, defaultValue, commentLines)
        {
        }

        public bool ShouldShowText
        {
            get
            {
                TextShowMode mode = (TextShowMode)Value;
                switch(mode)
                {
                    case TextShowMode.Off: return false;
                    case TextShowMode.AlwaysOn: return true;
                    case TextShowMode.HudHints: return BuildInfoMod.Instance.GameConfig.HudState == HudState.HINTS;
                    case TextShowMode.ShowOnPress: return BuildInfoMod.Instance.Config.TextShowBind.Value.IsPressed(InputLib.GetCurrentInputContext());
                    default: throw new Exception($"Unknown TextShowMode: {Value.ToString()}");
                }
            }
        }

        public override void ReadValue(string valueString, out string error)
        {
            error = null;

            if(valueString.IndexOf("true", StringComparison.OrdinalIgnoreCase) != -1)
            {
                SetValue(DefaultValue);
                Log.Info($"Old '{valueString}' value for '{Name}' got converted to {Value.ToString()} ({ValueName})");
                return;
            }

            if(valueString.IndexOf("false", StringComparison.OrdinalIgnoreCase) != -1)
            {
                SetValue((int)TextShowMode.Off);
                Log.Info($"Old '{valueString}' value for '{Name}' got converted to {Value.ToString()} ({ValueName})");
                return;
            }

            base.ReadValue(valueString, out error);
        }
    }
}
