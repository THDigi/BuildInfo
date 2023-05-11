using System.Collections.Generic;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    public abstract class StatusOverrideBase
    {
        protected readonly ToolbarStatusProcessor Processor;

        protected static readonly HashSet<int> TempUniqueInt = new HashSet<int>();
        protected static readonly HashSet<string> TempUniqueString = new HashSet<string>();

        public StatusOverrideBase(ToolbarStatusProcessor processor)
        {
            Processor = processor;
        }
    }
}
