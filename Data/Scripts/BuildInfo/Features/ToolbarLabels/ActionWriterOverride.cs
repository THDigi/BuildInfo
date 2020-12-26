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
        private string actionNameCache;

        private bool originalWriterException = false;

        private bool showBlockName = false;

        private bool customStatusChecked = false;
        private ToolbarActionStatus.StatusDel customStatusFunc = null;

        private static BuildInfoMod Main => BuildInfoMod.Instance;

        const int MAX_NAME_LENGTH = 22; // prints this many characters from the end of the name, will try to avoid splitting word and print from next space.

        // measured from "Argumen" word
        // NOTE: only supports English because MyLanguagesEnum is prohibited and some languages scale the text smaller or larger.
        const int MAX_LINE_SIZE = 126;
        const int SPACE_CHAR_STRETCH = 15; // arbitrary size to guarantee to go from one side to the other
        const int LINE_CHAR_STRETCH = 15;

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
                        Log.Info($"NewWriter called: {Action.Id,-24} {toolbarItem.Index.ToString(),-4} label={toolbarItem.Label}, group={toolbarItem.GroupName}");

                    if(mode == ToolbarActionLabelsMode.AlwaysOn
                    || MyAPIGateway.Gui.IsCursorVisible
                    || Main.ToolbarActionLabels.EnteredCockpitTicks > 0
                    || (mode == ToolbarActionLabelsMode.HudHints && (Main.GameConfig.HudState == HudState.HINTS || MyAPIGateway.Input.IsAnyAltKeyPressed()))
                    || (mode == ToolbarActionLabelsMode.AltKey && MyAPIGateway.Input.IsAnyAltKeyPressed()))
                    {
                        AppendLabel(block, sb, toolbarItem);
                    }

                    sb.Append(' ', SPACE_CHAR_STRETCH).Append('\n'); // left-align everything for consistency

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

            if(!customStatusChecked)
            {
                customStatusChecked = true;
                customStatusFunc = Main.ToolbarActionStatus.GetCustomStatus(block.BlockDefinition.TypeId);
            }

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
            if(toolbarItem.Label != null)
            {
                sb.Append(toolbarItem.Label);
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

            bool isGroup = (toolbarItem.GroupName != null);
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
                var parsedGroupName = Main.ToolbarParsedDataCache.GetGroupNameCache(toolbarItem.Index);
                if(parsedGroupName != null)
                {
                    sb.Append(parsedGroupName);
                }
                else
                {
                    int startIndex = Math.Max(0, sb.Length);

                    AppendWordWrapped(sb, toolbarItem.GroupName, MAX_NAME_LENGTH);
                    sb.Append('\n').Append('¯', LINE_CHAR_STRETCH).Append('\n');

                    parsedGroupName = sb.ToString(startIndex, sb.Length - startIndex);

                    Main.ToolbarParsedDataCache.SetGroupNameCache(toolbarItem.Index, parsedGroupName);
                }
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

                AppendWordWrapped(sb, block.CustomName, MAX_NAME_LENGTH);
                sb.Append('\n').Append('¯', LINE_CHAR_STRETCH).Append('\n');

                parsedCustomName = sb.ToString(startIndex, sb.Length - startIndex);

                Main.ToolbarParsedDataCache.SetBlockNameCache(block, parsedCustomName);
            }
        }

        private void AppendActionName(IMyTerminalBlock block, StringBuilder sb, ToolbarItemData toolbarItem)
        {
            // NOTE: the block condition's cases use 'return' to avoid caching because those actions are shared throughout multiple block types so can't be cached per action.

            if(block is IMyShipGrinder)
            {
                switch(Action.Id)
                {
                    case "OnOff": AppendWordWrapped(sb, "Grind On/Off"); return;
                    case "OnOff_On": AppendWordWrapped(sb, "Start grinding"); return;
                    case "OnOff_Off": AppendWordWrapped(sb, "Stop grinding"); return;
                }
            }

            if(block is IMyShipWelder)
            {
                switch(Action.Id)
                {
                    case "OnOff": AppendWordWrapped(sb, "Weld On/Off"); return;
                    case "OnOff_On": AppendWordWrapped(sb, "Start welding"); return;
                    case "OnOff_Off": AppendWordWrapped(sb, "Stop welding"); return;
                }
            }

            if(block is IMyShipDrill)
            {
                switch(Action.Id)
                {
                    case "OnOff": AppendWordWrapped(sb, "Drill On/Off"); return;
                    case "OnOff_On": AppendWordWrapped(sb, "Start drilling"); return;
                    case "OnOff_Off": AppendWordWrapped(sb, "Stop drilling"); return;
                }
            }

            if(block is IMyRemoteControl)
            {
                switch(Action.Id)
                {
                    case "Forward": AppendWordWrapped(sb, "Set Direction Forward"); return;
                    case "Backward": AppendWordWrapped(sb, "Set Direction Backward"); return;
                    case "Left": AppendWordWrapped(sb, "Set Direction Left"); return;
                    case "Right": AppendWordWrapped(sb, "Set Direction Right"); return;
                    case "Up": AppendWordWrapped(sb, "Set Direction Up"); return;
                    case "Down": AppendWordWrapped(sb, "Set Direction Down"); return;
                }
            }

            if(block is IMyProgrammableBlock && toolbarItem.PBRunArgument != null && Action.Id == "Run")
            {
                sb.Append(toolbarItem.PBRunArgument);
                return; // no cache
            }

            if(actionNameCache == null)
            {
                int startIndex = Math.Max(0, sb.Length);

                // shortened some action names
                switch(Action.Id)
                {
                    // generic functional
                    case "OnOff": AppendWordWrapped(sb, "On/Off"); break;
                    case "OnOff_On": AppendWordWrapped(sb, "Turn On"); break;
                    case "OnOff_Off": AppendWordWrapped(sb, "Turn Off"); break;

                    // generic terminal
                    case "ShowOnHUD": AppendWordWrapped(sb, "Toggle on HUD"); break;
                    case "ShowOnHUD_On": AppendWordWrapped(sb, "Show on HUD"); break;
                    case "ShowOnHUD_Off": AppendWordWrapped(sb, "Hide from HUD"); break;

                    // weapons
                    case "Shoot": AppendWordWrapped(sb, "Shoot On/Off"); break;
                    case "Shoot_On": AppendWordWrapped(sb, "Start shooting"); break;
                    case "Shoot_Off": AppendWordWrapped(sb, "Stop shooting"); break;

                    // doors, parachute
                    case "Open": AppendWordWrapped(sb, "Open/Close"); break;
                    case "Open_On": AppendWordWrapped(sb, "Open"); break;
                    case "Open_Off": AppendWordWrapped(sb, "Close"); break;

                    // PB
                    case "RunWithDefaultArgument": AppendWordWrapped(sb, "Run\n(NoArgs)"); break;

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

                        AppendWordWrapped(sb, name);
                        break;
                    }
                }

                // cache name for later use
                actionNameCache = sb.ToString(startIndex, sb.Length - startIndex);
            }
            else
            {
                sb.Append(actionNameCache);
            }
        }

        public static void AppendWordWrapped(StringBuilder sb, string text, int maxLength = 0)
        {
            int textLength = text.Length;
            int lineSize = 0;
            int startIndex = 0;

            if(maxLength > 0 && textLength > maxLength)
            {
                startIndex = (textLength - maxLength);

                // avoid splitting word by jumping to next space
                var nextSpace = text.IndexOf(' ', startIndex);
                if(nextSpace != -1)
                    startIndex = nextSpace + 1;

                sb.Append('…');
                lineSize += GetCharSize('…');
            }

            for(int i = startIndex; i < textLength; ++i)
            {
                var chr = text[i];

                if(chr == '\n')
                {
                    lineSize = 0;
                    sb.Append('\n');
                    continue;
                }

                int chrSize = GetCharSize(chr);
                lineSize += chrSize;

                if(chr == ' ')
                {
                    // find next space and determine if adding the word would go over the limit
                    int nextSpaceIndex = (textLength - 1);
                    int nextSizeAdd = 0;

                    for(int x = i + 1; x < textLength; ++x)
                    {
                        var chr2 = text[x];
                        nextSizeAdd += GetCharSize(chr2);

                        if(chr2 == ' ')
                        {
                            nextSpaceIndex = x;
                            break;
                        }
                    }

                    if((lineSize + nextSizeAdd) >= MAX_LINE_SIZE)
                    {
                        lineSize = 0;
                        sb.Append('\n');
                        continue;
                    }
                }

                if(lineSize >= MAX_LINE_SIZE)
                {
                    lineSize = chrSize;
                    sb.Append('\n');
                }

                sb.Append(chr);
            }
        }

        private static int GetCharSize(char chr)
        {
            var charSizeDict = Main.Constants.CharSize;
            int chrSize;
            if(!charSizeDict.TryGetValue(chr, out chrSize))
            {
                // only show this error for local mod version
                if(Log.WorkshopId == 0)
                    Log.Error($"Couldn't find character size for character: '{chr.ToString()}' ({((int)chr).ToString()}; {char.GetUnicodeCategory(chr).ToString()})", Log.PRINT_MESSAGE);

                chrSize = 8; // space's size
            }
            return chrSize;
        }
    }
}
