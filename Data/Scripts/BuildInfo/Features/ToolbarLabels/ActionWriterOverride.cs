using System;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarLabels
{
    public class ActionWriterOverride
    {
        public readonly IMyTerminalAction Action;

        public readonly Action<IMyTerminalBlock, StringBuilder> CustomWriter;
        public readonly Action<IMyTerminalBlock, StringBuilder> OriginalWriter;

        private ToolbarActionLabelsMode mode;
        private string actionNameCache;
        private bool ignoreWriter = false;
        private bool customStatusChecked = false;
        private ToolbarActionLabels.StatusDel customStatusFunc = null;

        public ActionWriterOverride(IMyTerminalAction action)
        {
            Action = action;
            OriginalWriter = action.Writer;
            CustomWriter = NewWriter;

            Action.Writer = CustomWriter;

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

            mode = (ToolbarActionLabelsMode)BuildInfoMod.Instance.Config.ToolbarActionLabelMode.Value;
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
                bool customizedStatus = false;

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

                    // TODO: add toggle for custom status too?
                    if(!customStatusChecked)
                    {
                        customStatusChecked = true;
                        customStatusFunc = BuildInfoMod.Instance.ToolbarActionLabels.GetCustomStatus(block.BlockDefinition.TypeId);
                    }

                    if(customStatusFunc != null)
                    {
                        var tempSB = BuildInfoMod.Instance.Caches.SB;
                        tempSB.Clear();

                        customizedStatus = customStatusFunc.Invoke(this, block, tempSB);

                        // remove trailing newlines
                        while(tempSB.Length > 0 && tempSB[tempSB.Length - 1] == '\n')
                        {
                            tempSB.Length -= 1;
                        }

                        // remove newlines from `sb` for every newline added by status' `tempSB`
                        for(int i = 0; i < tempSB.Length; ++i)
                        {
                            if(tempSB[i] == '\n')
                                sb.Length -= 1;
                        }

                        sb.AppendSB(tempSB);
                        tempSB.Clear();
                    }
                }

                if(!customizedStatus && !ignoreWriter)
                {
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
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void AppendLabel(IMyTerminalBlock block, StringBuilder sb)
        {
            // issues:
            // - can't detect groups
            // - gets kinda large

            // TODO per-block name cache?
            // TODO toggleable per block somehow (maybe use Show In Toolbar Config?) or toggle names globally

            if(MyAPIGateway.Gui.IsCursorVisible // always show names when in menu
            || (block is IMyCameraBlock && Action.Id == "View")
            || (block is IMyProgrammableBlock && (Action.Id == "Run" || Action.Id == "RunWithDefaultArgument"))
            || (block is IMyTimerBlock && (Action.Id == "TriggerNow" || Action.Id == "Start" || Action.Id == "Stop"))
            || (block is IMyRemoteControl && Action.Id == "Control")
            || (block is IMyLargeTurretBase && Action.Id == "Control")
            || (block is IMyShipConnector && Action.Id == "SwitchLock")
            || (block is IMyPistonBase && (Action.Id == "Reverse" || Action.Id == "Extend" || Action.Id == "Retract"))
            || (block is IMyMotorStator && Action.Id == "Reverse"))
            {
                AppendWordWrapped(sb, block.CustomName);
                sb.Append('\n');
                sb.Append('—', LINE_CHAR_STRETCH).Append('\n');
            }

            if(actionNameCache == null)
            {
                int startIndex = Math.Max(0, sb.Length);

                AppendWordWrapped(sb, Action.Name);

                actionNameCache = sb.ToString(startIndex, sb.Length - startIndex);
            }
            else
            {
                sb.Append(actionNameCache);
            }

            // over the vanilla inventory bar (collides with bar and selected tool text and "Toolbar" label in G menu)
            sb.Append('\n');
            sb.Append('\n');
            sb.Append('\n');

            if(BuildInfoMod.Instance.ShipToolInventoryBar.Shown)
            {
                sb.Append('\n');
                sb.Append('\n');
                sb.Append('\n');
            }
        }

        // measured from "Argumen" word
        // NOTE: only supports English because MyLanguagesEnum is prohibited and some languages scale the text smaller or larger.
        const int MAX_LINE_SIZE = 126;
        const int LINE_CHAR_STRETCH = 16; // arbitrary size to guarantee to go from one side to the other

        private void AppendWordWrapped(StringBuilder sb, StringBuilder text)
        {
            int textLength = text.Length;
            int lineSize = 0;

            for(int i = 0; i < textLength; ++i)
            {
                var chr = text[i];

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

                    if((lineSize + nextSizeAdd) > MAX_LINE_SIZE)
                    {
                        lineSize = 0;
                        sb.Append('\n');
                        continue;
                    }
                }

                if(lineSize > MAX_LINE_SIZE)
                {
                    lineSize = 0;
                    sb.Append('\n');
                }

                sb.Append(chr);
            }
        }

        private void AppendWordWrapped(StringBuilder sb, string text)
        {
            int textLength = text.Length;
            int lineSize = 0;

            for(int i = 0; i < textLength; ++i)
            {
                var chr = text[i];

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

                    if((lineSize + nextSizeAdd) > MAX_LINE_SIZE)
                    {
                        lineSize = 0;
                        sb.Append('\n');
                        continue;
                    }
                }

                if(lineSize > MAX_LINE_SIZE)
                {
                    lineSize = 0;
                    sb.Append('\n');
                }

                sb.Append(chr);
            }
        }

        private int GetCharSize(char chr)
        {
            var charSizeDict = BuildInfoMod.Instance.Constants.CharSize;
            int chrSize;
            if(!charSizeDict.TryGetValue(chr, out chrSize))
            {
                Log.Error($"Couldn't find character size for character: '{chr.ToString()}'", Log.PRINT_MSG);
                chrSize = charSizeDict[' '];
            }
            return chrSize;
        }
    }
}
