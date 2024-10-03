using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
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

            Stack<List<IMyTerminalAction>> pool = BuildInfoMod.Instance.Caches.PoolActions;
            List<IMyTerminalAction> actions = (pool.Count > 0 ? pool.Pop() : new List<IMyTerminalAction>(Caches.ExpectedActions));

            try
            {
                GetActions(null, actions);

                foreach(IMyTerminalAction a in actions)
                {
                    if(a.Id == actionId)
                    {
                        Action = a;
                        break;
                    }
                }
            }
            finally
            {
                actions.Clear();
                pool.Push(actions);
            }
        }

        protected static readonly List<IMyTerminalBlock> TempBlocks = new List<IMyTerminalBlock>(Caches.ExpectedTerminalBlocks);

        protected abstract void GetActions(MyToolbarType? toolbarType, List<IMyTerminalAction> results);

        protected void AppendActionName(StringBuilder sb, float opacity)
        {
            sb.Color(Color.Gray * opacity).Append(" - ").ResetFormatting();

            if(Action == null)
            {
                sb.Color(Color.Red * opacity).Append("(Unknown)");
            }
            else
            {
                sb.AppendMaxLength(Action.Name, ToolbarRender.MaxActionNameLength);
            }

            if(PBArg != null)
            {
                // ToolbarRender.MaxArgLength is kinda small for this, letting it stretch.
                sb.Append(": <i>").Color(ToolbarRender.ArgColor * opacity).AppendMaxLength(PBArg, 48);
            }
        }
    }
}
