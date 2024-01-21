using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.ConfigLib;
using VRageMath;

namespace Digi.BuildInfo.Features.Config
{
    public class BackgroundOpacitySetting : FloatSetting
    {
        public const float ValueHUD = -0.1f;

        public BackgroundOpacitySetting(ConfigHandler configInstance, string name, float defaultValue, params string[] commentLines)
            : base(configInstance, name, defaultValue, min: 0f, max: 1f, commentLines: commentLines)
        {
        }

        public override void ReadValue(string valueString, out string error)
        {
            error = null;

            if(valueString.ContainsIgnoreCase("HUD"))
            {
                SetValue(ValueHUD);
                return;
            }

            float tmp;
            if(float.TryParse(valueString, out tmp))
            {
                if(tmp < 0)
                    SetValue(ValueHUD);
                else
                    SetValue(MathHelper.Clamp(tmp, Min, Max));
                return;
            }

            error = "expected either HUD or a number";
        }

        public override void WriteValue(StringBuilder output)
        {
            output.Append(Value < 0 ? "HUD" : Value.ToString(FLOAT_FORMAT));
        }

        public override void WriteDefaultValue(StringBuilder output)
        {
            output.Append(DefaultValue < 0 ? "HUD" : DefaultValue.ToString(FLOAT_FORMAT));
        }
    }
}
