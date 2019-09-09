using System;

namespace Digi.ComponentLib
{
    [Flags]
    public enum UpdateFlags
    {
        INVALID = -1,
        NONE = 0,
        UPDATE_INPUT = 1,
        UPDATE_BEFORE_SIM = 2,
        UPDATE_AFTER_SIM = 4,
        UPDATE_DRAW = 8,
    }
}