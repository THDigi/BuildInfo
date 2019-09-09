using Digi.ConfigLib;

namespace Digi.BuildInfo.Features.Config
{
    public static class FlagExtensions
    {
        public static bool IsSet(this FlagsSetting<AimInfoFlags> setting, AimInfoFlags flag)
        {
            return (setting.Value & (int)flag) != 0;
        }

        public static bool IsSet(this FlagsSetting<PlaceInfoFlags> setting, PlaceInfoFlags flag)
        {
            return (setting.Value & (int)flag) != 0;
        }

        public static bool IsSet(this FlagsSetting<OverlayLabelsFlags> setting, OverlayLabelsFlags flag)
        {
            return (setting.Value & (int)flag) != 0;
        }

        public static string GetName(this TextAlignFlags flag)
        {
            switch(flag)
            {
                case TextAlignFlags.Top: return "Top";
                case TextAlignFlags.Bottom: return "Bottom";
                case TextAlignFlags.Left: return "Left";
                case TextAlignFlags.Right: return "Right";
                default: return flag.ToString();
            }
        }
    }
}
