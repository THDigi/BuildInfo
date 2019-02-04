using System.Text;

namespace Digi.Input.Devices
{
    public enum InputTypeEnum
    {
        NONE = 0,
        KEY,
        MOUSE,
        GAMEPAD,
        CONTROL,
        CUSTOM
    }

    public abstract class InputBase
    {
        public readonly InputTypeEnum Type;
        public readonly string Id;
        private readonly string DisplayName;

        public InputBase(InputTypeEnum type, string id, string displayName)
        {
            Type = type;
            Id = id;
            DisplayName = displayName ?? id;
        }

        public virtual bool IsAssigned(ControlContext contextId = ControlContext.CHARACTER)
        {
            return true;
        }

        public virtual string GetDisplayName(bool specialChars = true)
        {
            return DisplayName;
        }

        public virtual void GetBind(StringBuilder output, ControlContext contextId = ControlContext.CHARACTER, bool specialChars = true)
        {
            output.Append(DisplayName);
        }

        public abstract bool IsPressed(ControlContext contextId = ControlContext.CHARACTER);

        public abstract bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER);
    }
}
