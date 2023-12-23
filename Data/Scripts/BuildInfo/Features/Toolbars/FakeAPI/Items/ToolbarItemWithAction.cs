using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Collections;
using VRage.Game;
using VRageMath;

namespace Digi.BuildInfo.Features.Toolbars.FakeAPI.Items
{
    // TODO: some way to warn about PB's RunWithDefaultArgument's issues in MP?

    public abstract class ToolbarItemWithAction : ToolbarItem
    {
        public IMyTerminalAction Action { get; private set; }
        public string PBArg { get; protected set; }

        protected void SetAction(string actionId)
        {
            Action = null;

            foreach(IMyTerminalAction a in GetActions(null))
            {
                if(a.Id == actionId)
                {
                    Action = a;
                    break;
                }
            }
        }

        protected abstract ListReader<IMyTerminalAction> GetActions(MyToolbarType? toolbarType);

        protected void AppendActionName(StringBuilder sb, float opacity)
        {
            sb.ColorA(Color.Gray * opacity).Append(" - ").ResetFormatting();

            if(Action == null)
            {
                sb.ColorA(Color.Red * opacity).Append("(Unknown)");
            }
            else
            {
                sb.AppendMaxLength(Action.Name, ToolbarRender.MaxActionNameLength);
            }

            if(PBArg != null)
            {
                // ToolbarRender.MaxArgLength is kinda small for this, letting it stretch.
                sb.Append(": <i>").ColorA(ToolbarRender.ArgColor * opacity).AppendMaxLength(PBArg, 48);
            }
        }
    }
}
