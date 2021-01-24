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

            Action.Writer = CustomWriter;

            switch(Action.Id)
            {
                default: DisplayName = Action.Name.ToString(); break;

                    // TODO: rename some actions?
                    //case "OnOff": DisplayName = "On/Off"; break;
                    //case "OnOff_On": DisplayName = "Turn On"; break;
                    //case "OnOff_Off": DisplayName = "Turn Off"; break;

                    //case "ShowOnHUD": DisplayName = "Toggle on HUD"; break;
                    //case "ShowOnHUD_On": DisplayName = "Show on HUD"; break;
                    //case "ShowOnHUD_Off": DisplayName = "Hide from HUD"; break;

                    //case "Shoot": DisplayName = "Shoot On/Off"; break;
                    //case "Shoot_On": DisplayName = "Start shooting"; break;
                    //case "Shoot_Off": DisplayName = "Stop shooting"; break;

                    //case "Open": DisplayName = "Open/Close"; break;
                    //case "Open_On": DisplayName = "Open"; break;
                    //case "Open_Off": DisplayName = "Close"; break;

                    //case "AutoDeploy": DisplayName = "Toggle Auto-Deploy"; break;

                    //case "UseConveyor": DisplayName = "Toggle Use Conveyor"; break;

                    //case "RunWithDefaultArgument": DisplayName = "Run (no args)"; break;
            }
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
