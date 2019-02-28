using Digi.BuildInfo.Features.Config;
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
    }
}
