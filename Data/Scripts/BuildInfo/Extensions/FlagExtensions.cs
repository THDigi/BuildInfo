using static Digi.BuildInfo.Settings;

namespace Digi.BuildInfo.Extensions
{
    public static class FlagExtensions
    {
        public static bool IsSet(this AimInfoFlags source, AimInfoFlags flag)
        {
            return (source & flag) != 0;
        }

        public static bool IsSet(this HeldInfoFlags source, HeldInfoFlags flag)
        {
            return (source & flag) != 0;
        }
    }
}
