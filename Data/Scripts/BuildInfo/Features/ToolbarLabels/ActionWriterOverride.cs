using System;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Systems;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;

// DEBUG TODO Customdata for labelling in order?

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
        private string actionNameWrapped;

        private bool ignoreWriter = false;

        private bool customStatusChecked = false;
        private ToolbarActionLabels.StatusDel customStatusFunc = null;

        const int MAX_NAME_LENGTH = 22; // prints this many characters from the end of the name, will try to avoid splitting word and print from next space.

        public ActionWriterOverride(IMyTerminalAction action)
        {
            Action = action;
            OriginalWriter = action.Writer;
            CustomWriter = NewWriter;

            actionName = action.Name.ToString();

            Action.Writer = CustomWriter;

            mode = (ToolbarActionLabelsMode)BuildInfoMod.Instance.Config.ToolbarActionLabels.Value;

            // HACK giving an icon for some iconless actions
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

        public void SettingChanged(int newMode)
        {
            mode = (ToolbarActionLabelsMode)newMode;

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

                if(mode != ToolbarActionLabelsMode.Off)
                {
                    if(mode == ToolbarActionLabelsMode.AlwaysOn
                    || MyAPIGateway.Gui.IsCursorVisible
                    || BuildInfoMod.Instance.ToolbarActionLabels.EnteredCockpitTicks > 0
                    || (mode == ToolbarActionLabelsMode.HudHints && (BuildInfoMod.Instance.GameConfig.HudState == HudState.HINTS || MyAPIGateway.Input.IsAnyAltKeyPressed()))
                    || (mode == ToolbarActionLabelsMode.AltKey && MyAPIGateway.Input.IsAnyAltKeyPressed()))
                    {
                        AppendLabel(block, sb);
                    }

                    sb.Append(' ', LINE_CHAR_STRETCH).Append('\n'); // left-align everything for consistency

                    // elevate the label above the icon so it's readable
                    // also required here to have some empty lines to remove for multi-line custom statuses
                    sb.Append('\n');
                    sb.Append('\n');
                    sb.Append('\n');

                    overrideCustomStatus = AppendCustomStatus(block, sb);
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
            if(ignoreWriter)
                return;

            // HACK invoking original Writer on any action that has no writer throws NRE inside the game code, undetectable in a graceful way.
            try
            {
                OriginalWriter?.Invoke(block, sb);
            }
            catch(Exception)
            {
                ignoreWriter = true;
                //Log.Error($"Error calling original writer on {block.GetType().Name} with action {Action.Id}\n{e}", Log.PRINT_MSG);
            }
        }

        bool AppendCustomStatus(IMyTerminalBlock block, StringBuilder sb)
        {
            if(customStatusChecked && customStatusFunc == null)
                return false;

            if(!BuildInfoMod.Instance.Config.ToolbarActionStatus.Value)
                return false;

            if(!customStatusChecked)
            {
                customStatusChecked = true;
                customStatusFunc = BuildInfoMod.Instance.ToolbarActionLabels.GetCustomStatus(block.BlockDefinition.TypeId);
            }

            if(customStatusFunc == null)
                return false;

            var tempSB = BuildInfoMod.Instance.Caches.SB;
            tempSB.Clear();
            bool overrideStatus = customStatusFunc.Invoke(this, block, tempSB);

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

        void AppendLabel(IMyTerminalBlock block, StringBuilder sb)
        {
            AppendBlockName(block, sb);
            AppendActionName(sb);

            // over the vanilla inventory bar (collides with bar and selected tool text and "Toolbar" label in G menu)
            sb.Append('\n');
            sb.Append('\n');
            sb.Append('\n');

            // over the 2nd inventory bar if shown
            if(BuildInfoMod.Instance.ShipToolInventoryBar.Shown)
            {
                sb.Append('\n');
                sb.Append('\n');
                sb.Append('\n');
            }
        }

        private void AppendBlockName(IMyTerminalBlock block, StringBuilder sb)
        {
            // issues with printing block CustomName:
            // - can't detect groups
            // - gets kinda large

            // TODO: toggleable per block somehow (maybe use Show In Toolbar Config?)

            var blockNameMode = (ToolbarActionBlockNameMode)BuildInfoMod.Instance.Config.ToolbarActionBlockNames.Value;

            if(blockNameMode == ToolbarActionBlockNameMode.Off)
                return;

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
                // TODO: optimize?
                show = (MyAPIGateway.Gui.IsCursorVisible // always show names when in menu
                || (block is IMyCameraBlock && Action.Id == "View")
                || (block is IMyProgrammableBlock && (Action.Id == "Run" || Action.Id == "RunWithDefaultArgument"))
                || (block is IMyTimerBlock && (Action.Id == "TriggerNow" || Action.Id == "Start" || Action.Id == "Stop"))
                || (block is IMyRemoteControl && Action.Id == "Control")
                || (block is IMyLargeTurretBase && Action.Id == "Control")
                || (block is IMyShipConnector && Action.Id == "SwitchLock")
                || (block is IMyPistonBase && (Action.Id == "Reverse" || Action.Id == "Extend" || Action.Id == "Retract"))
                || (block is IMyMotorStator && Action.Id == "Reverse"));
            }

            if(!show)
                return;

            var parsedCustomName = BuildInfoMod.Instance.ToolbarActionLabels.GetCustomName(block.EntityId);

            if(parsedCustomName != null)
            {
                sb.Append(parsedCustomName);
            }
            else
            {
                int startIndex = Math.Max(0, sb.Length);

                AppendWordWrapped(sb, block.CustomName, MAX_NAME_LENGTH);
                sb.Append('\n');
                sb.Append('—', LINE_CHAR_STRETCH).Append('\n');

                parsedCustomName = sb.ToString(startIndex, sb.Length - startIndex);

                BuildInfoMod.Instance.ToolbarActionLabels.AddCustomNameCache(block, parsedCustomName);
            }
        }

        private void AppendActionName(StringBuilder sb)
        {
            if(actionNameWrapped == null)
            {
                int startIndex = Math.Max(0, sb.Length);

                // shortened some action names
                switch(Action.Id)
                {
                    case "RunWithDefaultArgument": AppendWordWrapped(sb, "Run\n(NoArgs)"); break;
                    case "OnOff": AppendWordWrapped(sb, "On/Off"); break;
                    case "OnOff_On": AppendWordWrapped(sb, "Turn ON"); break;
                    case "OnOff_Off": AppendWordWrapped(sb, "Turn Off"); break;
                    default: AppendWordWrapped(sb, actionName); break;
                }

                actionNameWrapped = sb.ToString(startIndex, sb.Length - startIndex);
            }
            else
            {
                sb.Append(actionNameWrapped);
            }
        }

        // measured from "Argumen" word
        // NOTE: only supports English because MyLanguagesEnum is prohibited and some languages scale the text smaller or larger.
        const int MAX_LINE_SIZE = 126;
        const int LINE_CHAR_STRETCH = 16; // arbitrary size to guarantee to go from one side to the other

        private static void AppendWordWrapped(StringBuilder sb, string text, int maxLength = 0)
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
            var charSizeDict = BuildInfoMod.Instance.Constants.CharSize;
            int chrSize;
            if(!charSizeDict.TryGetValue(chr, out chrSize))
            {
                Log.Error($"Couldn't find character size for character: '{chr.ToString()}' ({((int)chr).ToString()}; {char.GetUnicodeCategory(chr).ToString()})", Log.PRINT_MESSAGE);
                chrSize = charSizeDict[' '];
            }
            return chrSize;
        }
    }
}
