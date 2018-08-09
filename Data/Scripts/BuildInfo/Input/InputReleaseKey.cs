using System;
using Digi.Input.Devices;

namespace Digi.Input
{
    public struct InputReleaseKey : IEquatable<InputReleaseKey>
    {
        private readonly InputHandler.Combination combination;
        private readonly ControlContext contextId;
        private readonly int hash;

        public InputReleaseKey(InputHandler.Combination combination, ControlContext contextId)
        {
            this.combination = combination;
            this.contextId = contextId;

            hash = 17;
            hash = hash * 31 + combination.GetHashCode();
            hash = hash * 31 + contextId.GetHashCode();
        }

        public bool ShouldKeep()
        {
            return combination.IsPressed(contextId);
        }

        public bool Equals(InputReleaseKey other)
        {
            return (contextId == other.contextId && combination.CombinationString == other.combination.CombinationString);
        }

        public override int GetHashCode()
        {
            return hash;
        }
    }
}
