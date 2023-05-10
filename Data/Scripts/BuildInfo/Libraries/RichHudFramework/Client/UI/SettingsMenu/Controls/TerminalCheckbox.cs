namespace RichHudFramework.UI.Client
{
    /// <summary>
    /// Labeled checkbox designed to mimic the appearance of checkboxes in the SE terminal.
    /// </summary>
    public class TerminalCheckbox : TerminalValue<bool>
    {
        public TerminalCheckbox() : base(MenuControls.Checkbox)
        { }
    }
}