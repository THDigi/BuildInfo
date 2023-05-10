using VRageMath;

namespace RichHudFramework.UI.Client
{
    /// <summary>
    /// An RGB color picker using sliders for each channel. Designed to mimic the appearance of the color picker
    /// in the SE terminal.
    /// </summary>
    public class TerminalColorPicker : TerminalValue<Color>
    {
        public TerminalColorPicker() : base(MenuControls.ColorPicker)
        { }
    }
}