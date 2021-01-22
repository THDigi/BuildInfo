namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    public abstract class StatusOverrideBase
    {
        protected readonly ToolbarStatusProcessor Processor;
        protected BuildInfoMod Main => BuildInfoMod.Instance;

        public StatusOverrideBase(ToolbarStatusProcessor processor)
        {
            Processor = processor;
        }
    }
}
