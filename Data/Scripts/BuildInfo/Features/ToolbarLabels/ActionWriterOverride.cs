using System;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Systems;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace Digi.BuildInfo.Features.ToolbarLabels
{
    /// <summary>
    /// Override an action's writer.
    /// NOTE: This instance will be unique per action ID but not per block!
    /// </summary>
    public class ActionWriterOverride
    {
        public readonly IMyTerminalAction Action;

        public readonly Action<IMyTerminalBlock, StringBuilder> CustomWriter;
        public readonly Action<IMyTerminalBlock, StringBuilder> OriginalWriter;

        private ToolbarActionLabelsMode mode;

        private readonly string actionName;
        private string editedNameCache;

        private bool originalWriterException = false;

        private bool showBlockName = false;

        private bool customStatusChecked = false;
        private ToolbarActionStatus.StatusDel customStatusFunc = null;

        private static BuildInfoMod Main => BuildInfoMod.Instance;

        const int MaxNameLength = 22; // prints this many characters from the end of the name, will try to avoid splitting word and print from next space.
        const char LeftAlignChar = ' ';
        const int LeftAlignCount = 15; // to align text on left side
        const char SeparatorChar = '¯';
        const int SeparatorCount = 15; // to separate block name from action name

        public ActionWriterOverride(IMyTerminalAction action)
        {
            Action = action;

            OriginalWriter = action.Writer;
            CustomWriter = NewWriter;

            actionName = action.Name.ToString();
            showBlockName = Main.ToolbarActionLabels.IsActionUseful(Action.Id);
            mode = (ToolbarActionLabelsMode)Main.Config.ToolbarActionLabels.Value;

            Action.Writer = CustomWriter;

            // HACK: giving an icon for some iconless actions
            if(string.IsNullOrEmpty(action.Icon))
            {
                if(Action.Id == "Attach")
                {
                    action.Icon = @"Textures\GUI\Icons\Lock.png";
                }
                else if(Action.Id == "Detach")
                {
                    action.Icon = @"Textures\GUI\Icons\DisconnectedPlayerIcon.png";
                }
            }
        }

        public void Cleanup()
        {
            try
            {
                // HACK: can't set to null because reasons
                Action.Writer = (b, s) => { };
            }
            catch(Exception)
            {
                // ignore BS
            }
        }

        public void SettingChanged(ToolbarActionLabelsMode newMode)
        {
            mode = newMode;

            // FIXME: uncomment once CustomWriter getter is no longer throwing exceptions for PB Run action
            //if(mode == ToolbarActionLabelsMode.Off)
            //{
            //    Action.Writer = OriginalWriter;
            //    Log.Info($"Reset writer to original for action {Action.Name}"); // DEBUG
            //}
            //else
            //{
            //    Action.Writer = CustomWriter;
            //    Log.Info($"Reset writer to custom for action {Action.Name}"); // DEBUG
            //}
        }

        void NewWriter(IMyTerminalBlock block, StringBuilder sb)
        {
            try
            {
                if(block == null || block.MarkedForClose || sb == null)
                    return;

                bool overrideCustomStatus = false;

                // controller HUD not supported right now
                if(mode != ToolbarActionLabelsMode.Off && !MyAPIGateway.Input.IsJoystickLastUsed)
                {
                    // HACK: accuracy of this relies on the order of this writer being the same order as the items in the toolbar
                    // this method gets the current one and automatically switches to next.
                    var toolbarItem = Main.ToolbarCustomNames.GetToolbarItem();

                    if(ToolbarActionLabels.TOOLBAR_DEBUG_LOGGING)
                        Log.Info($"NewWriter called: {Action.Id,-24} {toolbarItem.Index.ToString(),-4} label={toolbarItem.LabelWrapped}, group={toolbarItem.GroupName}");

                    if(mode == ToolbarActionLabelsMode.AlwaysOn
                    || MyAPIGateway.Gui.IsCursorVisible
                    || Main.ToolbarActionLabels.EnteredCockpitTicks > 0
                    || (mode == ToolbarActionLabelsMode.HudHints && (Main.GameConfig.HudState == HudState.HINTS || MyAPIGateway.Input.IsAnyAltKeyPressed()))
                    || (mode == ToolbarActionLabelsMode.AltKey && MyAPIGateway.Input.IsAnyAltKeyPressed()))
                    {
                        AppendLabel(block, sb, toolbarItem);
                    }

                    sb.Append(LeftAlignChar, LeftAlignCount).Append('\n'); // left-align everything for consistency

                    // elevate the label above the icon so it's readable
                    // also required here to have some empty lines to remove for multi-line custom statuses
                    sb.Append('\n');
                    sb.Append('\n');
                    sb.Append('\n');

                    overrideCustomStatus = AppendCustomStatus(block, sb, toolbarItem);
                }

                if(!overrideCustomStatus)
                {
                    AppendOriginalStatus(block, sb);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void AppendOriginalStatus(IMyTerminalBlock block, StringBuilder sb)
        {
            if(originalWriterException)
                return;

            // HACK invoking original Writer on any action that has no writer throws NRE inside the game code, undetectable in a graceful way.
            try
            {
                OriginalWriter?.Invoke(block, sb);
            }
            catch(Exception)
            {
                originalWriterException = true;
                //Log.Error($"Error calling original writer on {block.GetType().Name} with action {Action.Id}\n{e}", Log.PRINT_MESSAGE);
            }
        }

        bool AppendCustomStatus(IMyTerminalBlock block, StringBuilder sb, ToolbarItemData toolbarItem)
        {
            //if(customStatusChecked && customStatusFunc == null)
            //    return false;

            if(!Main.Config.ToolbarActionStatus.Value)
                return false;

            // TODO: needs proper cache to be aware of different block types using same actionId e.g. parachute vs doors
            //if(!customStatusChecked)
            //{
            //    customStatusChecked = true;
            //    customStatusFunc = Main.ToolbarActionStatus.GetCustomStatus(block.BlockDefinition.TypeId);
            //}

            customStatusFunc = Main.ToolbarActionStatus.GetCustomStatus(block.BlockDefinition.TypeId);

            var tempSB = Main.Caches.SB;
            tempSB.Clear();

            bool overrideStatus = false;

            if(customStatusFunc != null)
                overrideStatus = customStatusFunc.Invoke(this, block, toolbarItem, tempSB);

            if(!overrideStatus)
                overrideStatus = Main.ToolbarActionStatus.Status_GenericFallback(this, block, toolbarItem, tempSB);

            if(tempSB.Length > 0)
            {
                // remove trailing newlines
                if(overrideStatus)
                {
                    while(tempSB.Length > 0 && tempSB[tempSB.Length - 1] == '\n')
                    {
                        tempSB.Length -= 1;
                    }
                }

                // remove newlines from `sb` for every newline added by status' `tempSB`
                for(int i = 0; i < tempSB.Length; ++i)
                {
                    if(tempSB[i] == '\n')
                        sb.Length -= 1;
                }

                sb.AppendStringBuilder(tempSB);
                tempSB.Clear();
            }

            return overrideStatus;
        }

        void AppendLabel(IMyTerminalBlock block, StringBuilder sb, ToolbarItemData toolbarItem)
        {
            // custom label from cockpit's CustomData replaces name and action
            if(toolbarItem.LabelWrapped != null)
            {
                sb.Append(toolbarItem.LabelWrapped);
            }
            else
            {
                AppendBlockName(block, sb, toolbarItem);
                AppendActionName(block, sb, toolbarItem);
            }

            // over the 2nd inventory bar if shown
            if(Main.ShipToolInventoryBar.Shown)
            {
                sb.Append('\n');
                sb.Append('\n');
                sb.Append('\n');
            }

            // over the vanilla inventory bar (collides with bar and selected tool text and "Toolbar" label in G menu)
            sb.Append('\n');
            sb.Append('\n');
            sb.Append('\n');
        }

        private void AppendBlockName(IMyTerminalBlock block, StringBuilder sb, ToolbarItemData toolbarItem)
        {
            // TODO: toggleable per block somehow (maybe use Show In Toolbar Config?)

            var blockNameMode = (ToolbarActionBlockNameMode)Main.Config.ToolbarActionBlockNames.Value;
            if(blockNameMode == ToolbarActionBlockNameMode.Off)
                return;

            bool isGroup = (toolbarItem.GroupNameWrapped != null);
            bool show = false;

            if(blockNameMode == ToolbarActionBlockNameMode.All)
            {
                show = true;
            }
            else if(blockNameMode == ToolbarActionBlockNameMode.OffExceptGUI)
            {
                show = MyAPIGateway.Gui.IsCursorVisible;
            }
            else if(blockNameMode == ToolbarActionBlockNameMode.Useful)
            {
                show = isGroup || showBlockName || MyAPIGateway.Gui.IsCursorVisible;
            }

            if(!show)
                return;

            if(isGroup)
            {
                sb.Append(toolbarItem.GroupNameWrapped);
                sb.Append('\n').Append(SeparatorChar, SeparatorCount).Append('\n');
                return;
            }

            // this cache expires on its own when block's customname changes.
            var parsedCustomName = Main.ToolbarParsedDataCache.GetBlockNameCache(block.EntityId);
            if(parsedCustomName != null)
            {
                sb.Append(parsedCustomName);
            }
            else
            {
                int startIndex = Math.Max(0, sb.Length);

                ToolbarActionLabels.AppendWordWrapped(sb, block.CustomName, MaxNameLength);
                sb.Append('\n').Append(SeparatorChar, SeparatorCount).Append('\n');

                parsedCustomName = sb.ToString(startIndex, sb.Length - startIndex);

                Main.ToolbarParsedDataCache.SetBlockNameCache(block, parsedCustomName);
            }
        }

        void AppendActionName(IMyTerminalBlock block, StringBuilder sb, ToolbarItemData toolbarItem)
        {
            // these are per-block conditions for actions used in multiple types of blocks so they can't be cached per action
            // instead they're cached per slot
            var parsedActionName = Main.ToolbarParsedDataCache.GetActionNameCache(toolbarItem.Index);
            if(parsedActionName != null)
            {
                sb.Append(parsedActionName);
                return;
            }
            else
            {
                int startIndex = Math.Max(0, sb.Length);
                if(AppendActionNameForSlot(block, sb, toolbarItem))
                {
                    Main.ToolbarParsedDataCache.SetActionNameCache(toolbarItem.Index, sb.ToString(startIndex, sb.Length - startIndex));
                    return;
                }
            }

            // and this is the name for this action regardless of block type, so it can be cached locally.
            if(editedNameCache != null)
            {
                sb.Append(editedNameCache);
            }
            else
            {
                int startIndex = Math.Max(0, sb.Length);

                // shortened some action names
                switch(Action.Id)
                {
                    // generic functional
                    case "OnOff": ToolbarActionLabels.AppendWordWrapped(sb, "On/Off"); break;
                    case "OnOff_On": ToolbarActionLabels.AppendWordWrapped(sb, "Turn On"); break;
                    case "OnOff_Off": ToolbarActionLabels.AppendWordWrapped(sb, "Turn Off"); break;

                    // generic terminal
                    case "ShowOnHUD": ToolbarActionLabels.AppendWordWrapped(sb, "Toggle on HUD", "Tgl HUD"); break;
                    case "ShowOnHUD_On": ToolbarActionLabels.AppendWordWrapped(sb, "Show on HUD", "HUD ON"); break;
                    case "ShowOnHUD_Off": ToolbarActionLabels.AppendWordWrapped(sb, "Hide from HUD", "HUD Off"); break;

                    // weapons
                    case "Shoot": ToolbarActionLabels.AppendWordWrapped(sb, "Shoot On/Off", "Tgl Shoot"); break;
                    case "Shoot_On": ToolbarActionLabels.AppendWordWrapped(sb, "Start shooting", "Shoot ON"); break;
                    case "Shoot_Off": ToolbarActionLabels.AppendWordWrapped(sb, "Stop shooting", "Shoot Off"); break;

                    // doors, parachute
                    case "Open": ToolbarActionLabels.AppendWordWrapped(sb, "Open/Close", "Tgl Open"); break;
                    case "Open_On": ToolbarActionLabels.AppendWordWrapped(sb, "Open"); break;
                    case "Open_Off": ToolbarActionLabels.AppendWordWrapped(sb, "Close"); break;

                    // parachute
                    case "AutoDeploy": ToolbarActionLabels.AppendWordWrapped(sb, "Togle Auto-\nDeploy", "Tgl Auto"); break;

                    case "RunWithDefaultArgument": ToolbarActionLabels.AppendWordWrapped(sb, "Run\n(NoArgs)", "Run"); break;

                    case "UseConveyor": ToolbarActionLabels.AppendWordWrapped(sb, "Toggle\nConveyors", "Conveyor"); break;

                    // lights
                    //case "IncreaseBlink Lenght": AppendWordWrapped(sb, "+ Blink Length"); break;
                    //case "DecreaseBlink Lenght": AppendWordWrapped(sb, "+ Blink Length"); break;

                    default:
                    {
                        string name = actionName;

                        //if(Action.Id.StartsWith("Increase"))
                        //    name = "+ " + Action.Id.Substring("Increase".Length);
                        //else if(Action.Id.StartsWith("Decrease"))
                        //    name = "- " + Action.Id.Substring("Decrease".Length);

                        ToolbarActionLabels.AppendWordWrapped(sb, name);
                        break;
                    }
                }

                // cache name for later use
                editedNameCache = sb.ToString(startIndex, sb.Length - startIndex);
            }
        }

        bool AppendActionNameForSlot(IMyTerminalBlock block, StringBuilder sb, ToolbarItemData toolbarItem)
        {
            if(block is IMyShipGrinder)
            {
                switch(Action.Id)
                {
                    case "OnOff": ToolbarActionLabels.AppendWordWrapped(sb, "Grind On/Off", "Tgl Grind"); return true;
                    case "OnOff_On": ToolbarActionLabels.AppendWordWrapped(sb, "Start grinding", "Grind ON"); return true;
                    case "OnOff_Off": ToolbarActionLabels.AppendWordWrapped(sb, "Stop grinding", "Grind Off"); return true;
                }
            }

            if(block is IMyShipWelder)
            {
                switch(Action.Id)
                {
                    case "OnOff": ToolbarActionLabels.AppendWordWrapped(sb, "Weld On/Off", "Tgl Weld"); return true;
                    case "OnOff_On": ToolbarActionLabels.AppendWordWrapped(sb, "Start welding", "Weld ON"); return true;
                    case "OnOff_Off": ToolbarActionLabels.AppendWordWrapped(sb, "Stop welding", "Weld Off"); return true;
                }
            }

            if(block is IMyShipDrill)
            {
                switch(Action.Id)
                {
                    case "OnOff": ToolbarActionLabels.AppendWordWrapped(sb, "Drill On/Off", "Tgl Drill"); return true;
                    case "OnOff_On": ToolbarActionLabels.AppendWordWrapped(sb, "Start drilling", "Drill ON"); return true;
                    case "OnOff_Off": ToolbarActionLabels.AppendWordWrapped(sb, "Stop drilling", "Drill Off"); return true;
                }
            }

            if(block is IMyRemoteControl)
            {
                switch(Action.Id)
                {
                    case "Forward": ToolbarActionLabels.AppendWordWrapped(sb, "Set Direction Forward", "D: Fwd"); return true;
                    case "Backward": ToolbarActionLabels.AppendWordWrapped(sb, "Set Direction Backward", "D: Back"); return true;
                    case "Left": ToolbarActionLabels.AppendWordWrapped(sb, "Set Direction Left", "D: Left"); return true;
                    case "Right": ToolbarActionLabels.AppendWordWrapped(sb, "Set Direction Right", "D: Right"); return true;
                    case "Up": ToolbarActionLabels.AppendWordWrapped(sb, "Set Direction Up", "D: Up"); return true;
                    case "Down": ToolbarActionLabels.AppendWordWrapped(sb, "Set Direction Down", "D: Down"); return true;
                }
            }

            if(block is IMyProgrammableBlock && toolbarItem.PBRunArgumentWrapped != null && Action.Id == "Run")
            {
                sb.Append("Run:\n").Append(toolbarItem.PBRunArgumentWrapped);
                return true;
            }

            return false;
        }
    }
}
