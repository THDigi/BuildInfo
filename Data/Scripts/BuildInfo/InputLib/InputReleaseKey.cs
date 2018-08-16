using System;
using Digi.Input.Devices;

namespace Digi.Input
{
    public struct InputReleaseKey : IEquatable<InputReleaseKey>
    {
        public readonly int Tick;
        public readonly InputLib.Combination Combination;
        public readonly ControlContext ContextId;

        public InputReleaseKey(InputLib.Combination combination, ControlContext contextId, int tick)
        {
            Combination = combination;
            ContextId = contextId;
            Tick = tick;
        }

        public bool ShouldKeep()
        {
            return Combination.IsPressed(ContextId);
        }

        public bool Equals(InputReleaseKey other)
        {
            return (ContextId == other.ContextId && Combination.CombinationString == other.Combination.CombinationString);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + Combination.GetHashCode();
            hash = hash * 31 + ContextId.GetHashCode();
            return hash;
        }
    }
}
