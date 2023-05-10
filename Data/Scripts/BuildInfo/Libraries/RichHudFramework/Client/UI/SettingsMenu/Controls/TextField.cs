using System;

namespace RichHudFramework.UI.Client
{
    public enum TextFieldAccessors : int
    {
        CharFilterFunc = 16,
    }

    /// <summary>
    /// One-line text field with a configurable input filter delegate. Designed to mimic the appearance of the text field
    /// in the SE terminal.
    /// </summary>
    public class TerminalTextField : TerminalValue<string>
    {
        /// <summary>
        /// Restricts the range of characters allowed for input.
        /// </summary>
        public Func<char, bool> CharFilterFunc
        {
            get { return GetOrSetMember(null, (int)TextFieldAccessors.CharFilterFunc) as Func<char, bool>; }
            set { GetOrSetMember(value, (int)TextFieldAccessors.CharFilterFunc); }
        }

        public TerminalTextField() : base(MenuControls.TextField)
        { }
    }
}