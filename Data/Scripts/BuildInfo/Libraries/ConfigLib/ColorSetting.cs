using System.Text;
using VRageMath;

namespace Digi.ConfigLib
{
    public class ColorSetting : SettingBase<Color>
    {
        public readonly bool UseAlpha;

        private readonly char[] SPLIT_BY = new char[] { ',' };

        public ColorSetting(ConfigHandler configInstance, string name, Color defaultValue, bool useAlpha = false, params string[] commentLines)
            : base(configInstance, name, defaultValue, commentLines)
        {
            UseAlpha = useAlpha;
        }

        public override void ReadValue(string value, out string error)
        {
            error = null;

            byte r, g, b, a;
            string[] split = value.Split(SPLIT_BY);
            if(split.Length >= 3 && byte.TryParse(split[0], out r) && byte.TryParse(split[1], out g) && byte.TryParse(split[2], out b))
            {
                a = 255; // default alpha if not defined
                if(split.Length == 3 || (UseAlpha && byte.TryParse(split[3], out a)))
                {
                    SetValue(new Color(r, g, b, a));
                    return;
                }
            }

            error = "expected format: R,G,B or R,G,B,A";
        }

        public override void WriteValue(StringBuilder output)
        {
            output.Append(Value.R).Append(", ").Append(Value.G).Append(", ").Append(Value.B);

            if(UseAlpha)
                output.Append(", ").Append(Value.A);
        }

        public override void WriteDefaultValue(StringBuilder output)
        {
            output.Append(DefaultValue.R).Append(", ").Append(DefaultValue.G).Append(", ").Append(DefaultValue.B);

            if(UseAlpha)
                output.Append(", ").Append(DefaultValue.A);
        }
    }
}
