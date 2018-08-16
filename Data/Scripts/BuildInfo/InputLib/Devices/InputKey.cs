using Sandbox.ModAPI;
using VRage.Input;

namespace Digi.Input.Devices
{
    public class InputKey : InputBase
    {
        public readonly MyKeys Key;

        public InputKey(MyKeys key, string id, string displayName) : base(InputTypeEnum.KEY, id, displayName)
        {
            Key = key;
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return MyAPIGateway.Input.IsKeyPress(Key);
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return MyAPIGateway.Input.IsNewKeyPressed(Key);
        }
    }
}
