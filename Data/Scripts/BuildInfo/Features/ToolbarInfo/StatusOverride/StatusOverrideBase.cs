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

        protected const char IconPowerOn = '\ue100';
        protected const char IconPowerOff = '\ue101';
        protected const char IconBroken = '\ue102';
        protected const char IconUnk3 = '\ue103';
        protected const char IconUnk4 = '\ue104';
        protected const char IconUnk5 = '\ue105';
        protected const char IconUnk6 = '\ue106';
        protected const char IconUnk7 = '\ue107';

        public StatusOverrideBase(ToolbarStatusProcessor processor)
        {
            Processor = processor;
        }
    }
}
