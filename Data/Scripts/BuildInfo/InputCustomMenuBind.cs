using Digi.Input.Devices;

namespace Digi.BuildInfo
{
    // NOTE: placed in this folder and namespace because Input folder is a junction with a shared project.

    /// <summary>
    /// Custom control for InputHandler
    /// </summary>
    public class InputCustomMenuBind : InputCustomBase
    {
        public InputCustomMenuBind() : base(Settings.MENU_BIND_INPUT_NAME, "BuildInfo Menu")
        {
        }

        public override bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return BuildInfo.Instance.Settings.MenuBind.IsJustPressed();
        }

        public override bool IsAssigned(ControlContext contextId = ControlContext.CHARACTER)
        {
            return BuildInfo.Instance.Settings.MenuBind.IsAssigned(contextId);
        }

        public override bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
        {
            return BuildInfo.Instance.Settings.MenuBind.IsPressed(contextId);
        }

        public override string GetBind(ControlContext contextId = ControlContext.CHARACTER, bool specialChars = true)
        {
            return BuildInfo.Instance.Settings.MenuBind.GetBinds(contextId, specialChars);
        }
    }
}
