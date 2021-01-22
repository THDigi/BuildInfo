using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    /// <summary>
    /// Grabs all possible actions and wraps them in <see cref="ActionWrapper"/> which takes control over their Writer func.
    /// </summary>
    public class ToolbarOverride : ModComponent
    {
        public readonly Dictionary<IMyTerminalAction, ActionWrapper> ActionWrappers = new Dictionary<IMyTerminalAction, ActionWrapper>(16);
        readonly Func<ITerminalAction, bool> CollectActionFunc;

        public ToolbarOverride(BuildInfoMod main) : base(main)
        {
            CollectActionFunc = new Func<ITerminalAction, bool>(CollectAction);

            Main.BlockMonitor.BlockAdded += BlockMonitor_BlockAdded;
        }

        protected override void RegisterComponent()
        {
        }

        protected override void UnregisterComponent()
        {
            Main.BlockMonitor.BlockAdded -= BlockMonitor_BlockAdded;
        }

        void BlockMonitor_BlockAdded(IMySlimBlock slimBlock)
        {
            var block = slimBlock.FatBlock as IMyTerminalBlock;
            if(block == null)
                return;

            // HACK: CustomActionGetter event doesn't get triggered for groups, making them undetectable until you see that action for a single block.
            // HACK: GetActions()'s collect function gets called before the toolbar check, allowing to get all actions.
            block.GetActions(null, CollectActionFunc);
        }

        bool CollectAction(ITerminalAction a)
        {
            var action = (IMyTerminalAction)a;

            if(!ActionWrappers.ContainsKey(action))
            {
                ActionWrappers.Add(action, new ActionWrapper(action));

                // HACK: giving an icon for some iconless actions
                if(string.IsNullOrEmpty(action.Icon))
                {
                    switch(action.Id)
                    {
                        case "Attach": action.Icon = @"Textures\GUI\Icons\Lock.png"; break;
                        case "Detach": action.Icon = @"Textures\GUI\Icons\DisconnectedPlayerIcon.png"; break;
                        default: Log.Info($"Action id '{action.Id}' has no icon, this mod could give it one... tell author :P"); break;
                    }
                }
            }

            return false; // null list, never add to it.
        }
    }
}
