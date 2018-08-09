namespace Digi.Input.Devices
{
    public abstract class InputAdvancedBase : InputBase
    {
        public readonly bool Analog;
        public readonly string PrintChar;

        public InputAdvancedBase(InputTypeEnum type, string id, string displayName, bool analog = false, char printChar = ' ') : base(type, id, displayName)
        {
            Analog = analog;
            PrintChar = (printChar == ' ' ? null : printChar.ToString());
        }

        public override string GetDisplayName(bool specialChars = true)
        {
            return (specialChars ? PrintChar : base.GetDisplayName(specialChars));
        }
    }

    public abstract class InputCustomBase : InputAdvancedBase
    {
        public InputCustomBase(string id, string displayName, bool analog = false, char printChar = ' ') : base(InputTypeEnum.CUSTOM, id, displayName, analog, printChar)
        {
        }
    }
}
