using System;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    public class ActionWrapper
    {
        public readonly IMyTerminalAction Action;
        public readonly string DisplayName;
        public readonly string OriginalIcon;
        public Action<IMyTerminalBlock, StringBuilder> OriginalWriter { get; private set; }

        public string CustomIcon = null;

        private readonly Action<IMyTerminalBlock, StringBuilder> CustomWriter;

        public ActionWrapper(IMyTerminalAction action)
        {
            Action = action;

            OriginalIcon = action.Icon;

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

                // not really necessary...
                //var controlled = Main.ToolbarMonitor.ControlledBlock;
                //if(controlled == null || !MyAPIGateway.GridGroups.HasConnection(controlled.CubeGrid, block.CubeGrid, GridLinkTypeEnum.Logical))
                //    return;

                // HACK: not overriding status when in GUI because it can be for timers/other toolbars and no idea which is which...
                // TODO: maybe find a way to detect them and maybe even label events slots for airvent and such...
                // Also no status override for gamepad HUD because it doesn't sync with the rest of the system so won't work.
                if((MyAPIGateway.Gui.IsCursorVisible && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
                || (!ToolbarMonitor.EnableGamepadSupport && MyAPIGateway.Input.IsJoystickLastUsed))
                {
                    AppendOriginalStatus(block, sb);
                    return;
                }

                ToolbarMonitor toolbarMonitor = BuildInfoMod.Instance.ToolbarMonitor;

                if(ToolbarMonitor.DebugLogging)
                    Log.Info($"Writer :: action={Action.Id}; block={block.CustomName}; wrapperSlotIndex={toolbarMonitor.WrapperSlotIndex.ToString()}");

                int max = toolbarMonitor.SequencedItems.Count;
                int num = toolbarMonitor.WrapperSlotIndex;
                if(num >= max)
                    return;

                // HACK: find next matching slot, it could not match if a mod adds actions via event which won't have this status override class
                // other issues are PBs or timer blocks calling actions and causing the writer to get triggered, desynchronizing the order.
                ToolbarItem toolbarItem = toolbarMonitor.SequencedItems[num];
                while(toolbarItem.ActionId != Action.Id || toolbarItem.BlockEntId != block.EntityId)
                {
                    num++;
                    if(num >= max)
                        return;

                    toolbarItem = toolbarMonitor.SequencedItems[num];
                }

                // writers get called in sequence that they are in the toolbar so this should pair them exactly
                toolbarMonitor.WrapperSlotIndex = num + 1;

                if(toolbarMonitor.ToolbarPage != (toolbarItem.Index / ToolbarMonitor.SlotsPerPage))
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

                    if(ToolbarMonitor.DebugLogging)
                        Log.Info($" ^-- filled data for slot #{toolbarItem.Index.ToString()}; name={toolbarItem.Name}");

                    // customized action names depending on block+action.

                    if(block is IMyShipGrinder)
                    {
                        switch(Action.Id)
                        {
                            case "OnOff": toolbarItem.ActionName = "Grind On/Off"; break;
                            case "OnOff_On": toolbarItem.ActionName = "Start grinding"; break;
                            case "OnOff_Off": toolbarItem.ActionName = "Stop grinding"; break;
                        }
                    }

                    if(block is IMyShipWelder)
                    {
                        switch(Action.Id)
                        {
                            case "OnOff": toolbarItem.ActionName = "Weld On/Off"; break;
                            case "OnOff_On": toolbarItem.ActionName = "Start welding"; break;
                            case "OnOff_Off": toolbarItem.ActionName = "Stop welding"; break;
                        }
                    }

                    if(block is IMyShipDrill)
                    {
                        switch(Action.Id)
                        {
                            case "OnOff": toolbarItem.ActionName = "Drill On/Off"; break;
                            case "OnOff_On": toolbarItem.ActionName = "Start drilling"; break;
                            case "OnOff_Off": toolbarItem.ActionName = "Stop drilling"; break;
                        }
                    }

                    if(block is IMyRemoteControl)
                    {
                        switch(Action.Id)
                        {
                            case "Forward": toolbarItem.ActionName = "Set Direction Forward"; break;
                            case "Backward": toolbarItem.ActionName = "Set Direction Backward"; break;
                            case "Left": toolbarItem.ActionName = "Set Direction Left"; break;
                            case "Right": toolbarItem.ActionName = "Set Direction Right"; break;
                            case "Up": toolbarItem.ActionName = "Set Direction Up"; break;
                            case "Down": toolbarItem.ActionName = "Set Direction Down"; break;
                        }
                    }

                    //if(block is IMyParachute)
                    //{
                    //    switch(Action.Id)
                    //    {
                    //        case "Open": toolbarItem.ActionName = "Deploy/Close"; break;
                    //        case "Open_On": toolbarItem.ActionName = "Deploy"; break;
                    //        case "Open_Off": toolbarItem.ActionName = "Close"; break;
                    //    }
                    //}
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
