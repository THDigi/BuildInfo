using System;

namespace Digi.ComponentLib
{
    [Flags]
    public enum UpdateFlags
    {
        NONE = 0,
        UPDATE_INPUT = 1,
        //UPDATE_BEFORE_SIM = 2, // not used in this mod
        UPDATE_AFTER_SIM = 4,
        UPDATE_DRAW = 8,
    }

    public static class UpdateFlagsExtensions
    {
        /// <summary>
        /// Checks if <paramref name="flag"/> is set.
        /// </summary>
        public static bool HasFlag(this UpdateFlags flags, UpdateFlags flag)
        {
            return (flags & flag) != 0;
        }
    }
}
