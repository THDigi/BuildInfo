using Sandbox.ModAPI;
using VRage.Input;

namespace Digi.Input.Devices
{
    public class InputMouseButton : InputBase
    {
        public readonly MyMouseButtonsEnum Button;

        public InputMouseButton(MyMouseButtonsEnum button, string id, string displayName) : base(InputTypeEnum.MOUSE, id, displayName)
        {
            Button = button;
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return MyAPIGateway.Input.IsMousePressed(Button);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return MyAPIGateway.Input.IsNewMousePressed(Button);
        }
    }

    public class InputMouseAnalog : InputAdvancedBase
    {
        public InputMouseAnalog() : base(InputTypeEnum.MOUSE, InputLib.MOUSE_PREFIX + "analog", "Mouse X/Y/Scroll (analog)", analog: true)
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return (MyAPIGateway.Input.GetMouseXForGamePlay() != 0 || MyAPIGateway.Input.GetMouseYForGamePlay() != 0 || MyAPIGateway.Input.DeltaMouseScrollWheelValue() != 0);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }

    public class InputMouseScroll : InputAdvancedBase
    {
        public InputMouseScroll() : base(InputTypeEnum.MOUSE, InputLib.MOUSE_PREFIX + "scroll", "Mouse Scroll (analog)", analog: true)
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return (MyAPIGateway.Input.DeltaMouseScrollWheelValue() != 0);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }

    public class InputMouseScrollUp : InputAdvancedBase
    {
        public InputMouseScrollUp() : base(InputTypeEnum.MOUSE, InputLib.MOUSE_PREFIX + "scrollup", "Mouse Scroll Up")
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return (MyAPIGateway.Input.DeltaMouseScrollWheelValue() > 0);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }

    public class InputMouseScrollDown : InputAdvancedBase
    {
        public InputMouseScrollDown() : base(InputTypeEnum.MOUSE, InputLib.MOUSE_PREFIX + "scrolldown", "Mouse Scroll Down")
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return (MyAPIGateway.Input.DeltaMouseScrollWheelValue() < 0);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }

    public class InputMouseX : InputAdvancedBase
    {
        public InputMouseX() : base(InputTypeEnum.MOUSE, InputLib.MOUSE_PREFIX + "x", "Mouse X (analog)", analog: true)
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return (MyAPIGateway.Input.GetMouseXForGamePlay() != 0);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }

    public class InputMouseY : InputAdvancedBase
    {
        public InputMouseY() : base(InputTypeEnum.MOUSE, InputLib.MOUSE_PREFIX + "y", "Mouse Y (analog)", analog: true)
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return (MyAPIGateway.Input.GetMouseYForGamePlay() != 0);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }

    public class InputMouseXPos : InputAdvancedBase
    {
        public InputMouseXPos() : base(InputTypeEnum.MOUSE, InputLib.MOUSE_PREFIX + "x+", "Mouse X+")
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return (MyAPIGateway.Input.GetMouseXForGamePlay() > 0);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }

    public class InputMouseXNeg : InputAdvancedBase
    {
        public InputMouseXNeg() : base(InputTypeEnum.MOUSE, InputLib.MOUSE_PREFIX + "x-", "Mouse X-")
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return (MyAPIGateway.Input.GetMouseXForGamePlay() < 0);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }

    public class InputMouseYPos : InputAdvancedBase
    {
        public InputMouseYPos() : base(InputTypeEnum.MOUSE, InputLib.MOUSE_PREFIX + "y+", "Mouse Y+")
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return (MyAPIGateway.Input.GetMouseYForGamePlay() > 0);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }

    public class InputMouseYNeg : InputAdvancedBase
    {
        public InputMouseYNeg() : base(InputTypeEnum.MOUSE, InputLib.MOUSE_PREFIX + "y-", "Mouse Y-")
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return (MyAPIGateway.Input.GetMouseYForGamePlay() < 0);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }
}
