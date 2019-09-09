namespace Digi.ComponentLib
{
    public static class UpdateFlagsExtensions
    {
        public static bool IsSet(this UpdateFlags flags, UpdateFlags flag)
        {
            return (flags & flag) != 0;
        }
    }
}