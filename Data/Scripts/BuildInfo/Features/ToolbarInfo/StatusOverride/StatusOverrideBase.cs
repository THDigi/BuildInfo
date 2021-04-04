namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    public abstract class StatusOverrideBase
    {
        protected readonly ToolbarStatusProcessor Processor;

        public StatusOverrideBase(ToolbarStatusProcessor processor)
        {
            Processor = processor;
        }
    }
}
