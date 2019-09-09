using System.Text;
using Digi.Input.Devices;

namespace Digi.BuildInfo.Features.Config
{
    /// <summary>
    /// Custom control for InputHandler
    /// </summary>
    public class MenuCustomInput : InputCustomBase
    {
        public MenuCustomInput() : base(Config.MENU_BIND_INPUT_NAME, "BuildInfo Menu")
        {
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return BuildInfoMod.Instance.Config.MenuBind.Value.IsJustPressed();
        }

        public override bool IsAssigned(ControlContext contextId = ControlContext.CHARACTER)
        {
            return BuildInfoMod.Instance.Config.MenuBind.Value.IsAssigned(contextId);
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return BuildInfoMod.Instance.Config.MenuBind.Value.IsPressed(contextId);
        }

        public override void GetBind(StringBuilder output, ControlContext contextId = ControlContext.CHARACTER, bool specialChars = true)
        {
            BuildInfoMod.Instance.Config.MenuBind.Value.GetBinds(output, contextId, specialChars);
        }
    }
}
