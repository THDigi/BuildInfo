using System.Text;
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

        public override void GetBind(StringBuilder output, ControlContext contextId = ControlContext.CHARACTER, bool specialChars = true)
        {
            string bind = null;

            if(MyAPIGateway.Input.IsJoystickLastUsed)
            {
                bind = InputLib.GetInputDisplayName(contextId, ControlId, specialChars);
            }

            // using kb/m or it's unassigned for gamepad/joystick
            if(bind == null)
            {
                var control = MyAPIGateway.Input.GetGameControl(ControlId);

                if(control.GetMouseControl() != MyMouseButtonsEnum.None)
                {
                    bind = InputLib.GetInputDisplayName(control.GetMouseControl());
                }
                else if(control.GetKeyboardControl() != MyKeys.None)
                {
                    bind = InputLib.GetInputDisplayName(control.GetKeyboardControl());
                }
                else if(control.GetSecondKeyboardControl() != MyKeys.None)
                {
                    bind = InputLib.GetInputDisplayName(control.GetSecondKeyboardControl());
                }
            }

            if(bind != null)
                output.Append(bind);
            else
                output.Append("<Unassigned:").Append(GetDisplayName(specialChars)).Append('>');
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
            return InputLib.GetGameControlPressed(contextId, ControlId);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return InputLib.GetGameControlJustPressed(contextId, ControlId);
        }
    }

    public class InputGameControlMovement : InputAdvancedBase
    {
        public InputGameControlMovement() : base(InputTypeEnum.CONTROL, InputLib.CONTROL_PREFIX + "move", "Movement (analog)", analog: true)
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            var input = InputLib.GetMovementInput();
            return !Vector3.IsZero(input);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed(); // TODO: this is probably wrong
        }
    }

    public class InputGameControlRotation : InputAdvancedBase
    {
        public InputGameControlRotation() : base(InputTypeEnum.CONTROL, InputLib.CONTROL_PREFIX + "view", "Rotation (analog)", analog: true)
        {
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            var input = InputLib.GetRotationInput();
            return !Vector3.IsZero(input);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return IsPressed(); // TODO: this is probably wrong
        }
    }
}
