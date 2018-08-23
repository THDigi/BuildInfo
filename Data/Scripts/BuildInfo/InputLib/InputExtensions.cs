using Sandbox.ModAPI;
using VRage.Input;
using VRage.Utils;

namespace Digi.Input
{
    public static class InputExtensions
    {
        /// <summary>
        /// Gets the key/button name assigned to this control.
        /// </summary>
        public static string GetAssignedInputName(this MyStringId controlId)
        {
            var control = MyAPIGateway.Input.GetGameControl(controlId);

            if(control.GetKeyboardControl() != MyKeys.None)
                return control.GetKeyboardControl().ToString();
            else if(control.GetSecondKeyboardControl() != MyKeys.None)
                return control.GetSecondKeyboardControl().ToString();
            else if(control.GetMouseControl() != MyMouseButtonsEnum.None)
                return MyAPIGateway.Input.GetName(control.GetMouseControl());

            return null;
        }
    }
}
