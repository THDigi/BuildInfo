using System;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    public class ActionWrapper
    {
        public readonly IMyTerminalAction Action;
        public readonly string DisplayName;
        public Action<IMyTerminalBlock, StringBuilder> OriginalWriter { get; private set; }

        private readonly Action<IMyTerminalBlock, StringBuilder> CustomWriter;

        private static BuildInfoMod Main => BuildInfoMod.Instance;

        public ActionWrapper(IMyTerminalAction action)
        {
            Action = action;

            OriginalWriter = Action.Writer;
            CustomWriter = NewWriter;

            DisplayName = Action.Name.ToString();
            Action.Writer = CustomWriter;
        }

        void NewWriter(IMyTerminalBlock block, StringBuilder sb)
        {
            try
            {
                if(block == null || block.MarkedForClose || sb == null)
                    return;

                if(!ToolbarMonitor.EnableGamepadSupport && MyAPIGateway.Input.IsJoystickLastUsed)
                {
                    AppendOriginalStatus(block, sb);
                    return;
                }

                int num = Main.ToolbarMonitor.WrapperSlotIndex;
                if(num >= Main.ToolbarMonitor.SequencedItems.Count)
                    return;

                var toolbarItem = Main.ToolbarMonitor.SequencedItems[num];
                if(toolbarItem.ActionId != Action.Id)
                    return;

                // writers get called in sequence that they are in the toolbar so this should pair them exactly
                Main.ToolbarMonitor.WrapperSlotIndex++;

                if(Main.ToolbarMonitor.ToolbarPage != (toolbarItem.Index / 9))
                    return;

                // update some properties that are easily accessible in this context.
                if(toolbarItem.ActionWrapper == null)
                {
                    toolbarItem.ActionWrapper = this;
                    toolbarItem.ActionName = DisplayName;
                    toolbarItem.Block = block;

                    if(toolbarItem.CustomLabel == null)
                        toolbarItem.Name = block.CustomName;
                    else
                        toolbarItem.Name = "<ERROR Should See CustomLabel>"; // required non-null to simplify checks in other classes
                }

                sb.AppendStringBuilder(toolbarItem.StatusSB);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public void AppendOriginalStatus(IMyTerminalBlock block, StringBuilder sb)
        {
            try
            {
                OriginalWriter?.Invoke(block, sb);
            }
            catch(Exception)
            {
                // HACK invoking original Writer on any action that has no writer throws NRE inside the game code, undetectable in a graceful way.
                OriginalWriter = null;
            }
        }
    }
}
