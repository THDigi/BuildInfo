using System.Collections.Generic;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    public abstract class StatusOverrideBase
    {
        protected readonly ToolbarStatusProcessor Processor;

        protected static readonly HashSet<int> TempUniqueInt = new HashSet<int>();
        protected static readonly HashSet<string> TempUniqueString = new HashSet<string>();

        protected const int MaxChars = ToolbarStatusProcessor.MaxChars;
        protected const int MaxLines = ToolbarStatusProcessor.MaxLines;

        protected const char IconPowerOn = ToolbarStatusProcessor.IconPowerOn;
        protected const char IconPowerOff = ToolbarStatusProcessor.IconPowerOff;
        // TODO better damage icon
        protected const char IconBroken = ' '; // ToolbarStatusProcessor.IconBroken;
        protected const char IconGood = ToolbarStatusProcessor.IconGood;
        protected const char IconBad = ToolbarStatusProcessor.IconBad;
        protected const char IconAlert = ToolbarStatusProcessor.IconAlert;

        public StatusOverrideBase(ToolbarStatusProcessor processor)
        {
            Processor = processor;
        }
    }
}
