using Sandbox.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace Digi.Input.Devices
{
    public class InputGameControl : InputBase
    {
        public readonly MyStringId ControlId;

        public InputGameControl(MyStringId controlId, string id, string displayName) : base(InputTypeEnum.CONTROL, id, displayName)
        {
            ControlId = controlId;
        }

        public override string GetBind(ControlContext contextId = ControlContext.CHARACTER, bool specialChars = true)
        {
            string name = null;

            if(MyAPIGateway.Input.IsJoystickLastUsed)
            {
                name = InputHandler.GetInputDisplayName(contextId, ControlId, specialChars);
            }

            // using kb/m or unassigned for gamepad/joystick
            if(name == null)
            {
                var control = MyAPIGateway.Input.GetGameControl(ControlId);

                if(control.GetMouseControl() != MyMouseButtonsEnum.None)
                {
                    name = InputHandler.GetInputDisplayName(control.GetMouseControl());
                }
                else if(control.GetKeyboardControl() != MyKeys.None)
                {
                    name = InputHandler.GetInputDisplayName(control.GetKeyboardControl());
                }
                else if(control.GetSecondKeyboardControl() != MyKeys.None)
                {
                    name = InputHandler.GetInputDisplayName(control.GetSecondKeyboardControl());
                }
            }

            if(name != null)
                return name;

            return $"[Unassigned:{GetDisplayName(specialChars)}]";
        }

        public override bool IsAssigned(ControlContext contextId = ControlContext.CHARACTER)
        {
            var control = MyAPIGateway.Input.GetGameControl(ControlId);

            return (control.GetKeyboardControl() != MyKeys.None
                 || control.GetSecondKeyboardControl() != MyKeys.None
                 || control.GetMouseControl() != MyMouseButtonsEnum.None);
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return InputHandler.GetGameControlPressed(contextId, ControlId);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return InputHandler.GetGameControlJustPressed(contextId, ControlId);
        }
    }

    public class InputGameControlMovement : InputAdvancedBase
    {
        public InputGameControlMovement() : base(InputTypeEnum.CONTROL, InputHandler.CONTROL_PREFIX + "move", "Movement (analog)", analog: true)
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            var input = InputHandler.GetMovementInput();
            return !Vector3.IsZero(input);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed(); // TODO this is probably wrong
        }
    }

    public class InputGameControlRotation : InputAdvancedBase
    {
        public InputGameControlRotation() : base(InputTypeEnum.CONTROL, InputHandler.CONTROL_PREFIX + "view", "Rotation (analog)", analog: true)
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            var input = InputHandler.GetRotationInput();
            return !Vector3.IsZero(input);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed(); // TODO this is probably wrong
        }
    }
}
