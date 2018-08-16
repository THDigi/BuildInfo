using System;
using Sandbox.ModAPI;
using VRage.Input;

namespace Digi.Input.Devices
{
    public class InputGamepadAxis : InputAdvancedBase
    {
        public readonly MyJoystickAxesEnum Axis;

        public InputGamepadAxis(MyJoystickAxesEnum axis, string id, string displayName, char printChar) : base(InputTypeEnum.GAMEPAD, id, displayName, false, printChar)
        {
            Axis = axis;
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return MyAPIGateway.Input.IsJoystickAxisPressed(Axis);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return MyAPIGateway.Input.IsJoystickAxisNewPressed(Axis);
        }
    }

    public class InputGamepadButton : InputAdvancedBase
    {
        public readonly MyJoystickButtonsEnum Button;

        public InputGamepadButton(MyJoystickButtonsEnum button, string id, string displayName, char printChar) : base(InputTypeEnum.GAMEPAD, id, displayName, false, printChar)
        {
            Button = button;
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return MyAPIGateway.Input.IsJoystickButtonPressed(Button);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return MyAPIGateway.Input.IsJoystickButtonNewPressed(Button);
        }
    }

    public class InputGamepadLeftTrigger : InputAdvancedBase
    {
        public InputGamepadLeftTrigger() : base(InputTypeEnum.GAMEPAD, InputLib.GAMEPAD_PREFIX + "ltanalog", "Left Trigger (analog)", analog: true, printChar: '\xe008')
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return (MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Zpos) != 0);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }

    public class InputGamepadRightTrigger : InputAdvancedBase
    {
        public InputGamepadRightTrigger() : base(InputTypeEnum.GAMEPAD, InputLib.GAMEPAD_PREFIX + "rtanalog", "Right Trigger (analog)", analog: true, printChar: '\xe007')
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return (MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Zneg) != 0);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }

    public class InputGamepadLeftStick : InputAdvancedBase
    {
        public InputGamepadLeftStick() : base(InputTypeEnum.GAMEPAD, InputLib.GAMEPAD_PREFIX + "lsanalog", "Left Stick (analog)", analog: true, printChar: '\xe00b')
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            var x = -MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xneg) + MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Xpos);
            var y = -MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Yneg) + MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.Ypos);

            return (Math.Abs(x) > InputLib.EPSILON || Math.Abs(y) > InputLib.EPSILON);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }

    public class InputGamepadRightStick : InputAdvancedBase
    {
        public InputGamepadRightStick() : base(InputTypeEnum.GAMEPAD, InputLib.GAMEPAD_PREFIX + "rsanalog", "Right Stick (analog)", analog: true, printChar: '\xe00c')
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            var x = -MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXneg) + MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationXpos);
            var y = -MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYneg) + MyAPIGateway.Input.GetJoystickAxisStateForGameplay(MyJoystickAxesEnum.RotationYpos);

            return (Math.Abs(x) > InputLib.EPSILON || Math.Abs(y) > InputLib.EPSILON);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed();
        }
    }
}
