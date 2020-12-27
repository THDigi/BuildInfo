using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.Game;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarLabels
{
    // TODO: optimize writer callbacks by ignoring ones that aren't visible, need page tracking with hotkeys.

    public class ToolbarActionLabels : ModComponent
    {
        public static readonly bool ToolbarDebugLogging = false;

        public const int CockpitLabelsForTicks = Constants.TICKS_PER_SECOND * 3;

        public int EnteredCockpitTicks { get; private set; } = CockpitLabelsForTicks;

        public bool IsActionUseful(string actionId) => UsefulActions.Contains(actionId);
        readonly HashSet<string> UsefulActions = new HashSet<string>() // shows block name for these actions if the "useful" setting is used
        {
            "View", // camera
            "Open", "Open_On", "Open_Off", // doors and parachute
            "ShootOnce", "Shoot", "Shoot_On", "Shoot_Off", // weapons
            "Control", // RC and turrets
            "DrainAll", // conveyor sorter
            "MainCockpit, MainRemoteControl", // cockpit/RC
            "SwitchLock", // connector
            "Reverse", "Extend", "Retract", // pistons/rotors
            "Run", "RunWithDefaultArgument", // PB
            "TriggerNow", "Start", "Stop", // timers
            "Jump", // jumpdrive
        };

        readonly Dictionary<IMyTerminalAction, ActionWriterOverride> OverriddenActions = new Dictionary<IMyTerminalAction, ActionWriterOverride>(16);

        readonly Func<ITerminalAction, bool> CollectActionFunc;

        public ToolbarActionLabels(BuildInfoMod main) : base(main)
        {
            CollectActionFunc = new Func<ITerminalAction, bool>(CollectAction);

            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;

            Main.BlockMonitor.BlockAdded += BlockMonitor_BlockAdded;
        }

        protected override void RegisterComponent()
        {
            // ActionWriterOverride can't read the config that early so all are updated here.
            UpdateOverridesMode((ToolbarActionLabelsMode)Main.Config.ToolbarActionLabels.Value);

            MyVisualScriptLogicProvider.PlayerEnteredCockpit += EnteredCockpit;

            Main.Config.ToolbarActionLabels.ValueAssigned += ToolbarActionLabelModeChanged;
        }

        protected override void UnregisterComponent()
        {
            MyVisualScriptLogicProvider.PlayerEnteredCockpit -= EnteredCockpit;

            Main.Config.ToolbarActionLabels.ValueAssigned -= ToolbarActionLabelModeChanged;

            Main.BlockMonitor.BlockAdded -= BlockMonitor_BlockAdded;

            foreach(var actionOverride in OverriddenActions.Values)
            {
                actionOverride?.Cleanup();
            }

            OverriddenActions.Clear();
        }

        void ToolbarActionLabelModeChanged(int oldValue, int newValue, SettingBase<int> setting)
        {
            if(oldValue != newValue)
            {
                UpdateOverridesMode((ToolbarActionLabelsMode)newValue);
            }
        }

        void UpdateOverridesMode(ToolbarActionLabelsMode mode)
        {
            foreach(var overriddenAction in OverriddenActions.Values)
            {
                overriddenAction.SettingChanged(mode);
            }
        }

        void EnteredCockpit(string entityName, long playerId, string gridName)
        {
            try
            {
                var player = MyAPIGateway.Session?.Player;
                if(player != null && player.IdentityId == playerId)
                {
                    EnteredCockpitTicks = CockpitLabelsForTicks;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
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

            if(!OverriddenActions.ContainsKey(action))
            {
                if(ToolbarDebugLogging)
                    Log.Info($"Added action: {action.Id}");

                OverriddenActions.Add(action, new ActionWriterOverride(action));
            }

            return false; // null list, never add to it.
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(ToolbarDebugLogging)
                Log.Info("---- AFTER SIM -----------------------------------");

            if(EnteredCockpitTicks > 0)
            {
                EnteredCockpitTicks--;
            }
        }

        // TODO needs various changes to be changeable in realtime
        public const bool SingleLineNames = false;

        // NOTE: this is not reliable in any way to prevent the next lines from vanishing, but it'll do for now
        // measured from "Argumen" word
        // NOTE: only supports English because MyLanguagesEnum is prohibited and some languages scale the text smaller or larger.
        public const int MaxLineSize = 120;
        const char MaxLengthCutoff = '-';
        const int MaxLengthCutoffSize = 10; // must match size from ComputeCharacterSizes()
        const char NoWrapCutOff = '-';
        const int NoWrapCutOffSize = 10; // must match size from ComputeCharacterSizes()

        public static bool AppendOnlyShort(StringBuilder sb, string shortText)
        {
            if(SingleLineNames)
            {
                AppendWordWrapped(sb, null, shortText);
                return true;
            }

            return false;
        }

        public static void AppendWordWrapped(StringBuilder sb, string text, string shortText)
        {
            if(SingleLineNames)
            {
                //int lineSize = 0;
                //for(int i = 0; i < shortText.Length; ++i)
                //{
                //    var chr = shortText[i];
                //    int chrSize = BuildInfoMod.Instance.FontsHandler.GetCharSize(chr);
                //    lineSize += chrSize;
                //
                //    if(lineSize >= MaxLineSize)
                //    {
                //        Log.Error($"Shorthand text is too wide: text='{shortText}'; char='{chr.ToString()}'; index={i.ToString()}, lineSize={lineSize.ToString()} / {MaxLineSize.ToString()}", Log.PRINT_MESSAGE);
                //        sb.Append("#####");
                //        return;
                //    }
                //}

                sb.Append(shortText);
            }
            else
            {
                AppendWordWrapped(sb, text);
            }
        }

        public static void AppendWordWrapped(StringBuilder sb, string text, int maxLength = 0)
        {
            int textLength = text.Length;
            int lineSize = 0;
            int startIndex = 0;

            if(!SingleLineNames && maxLength > 0 && textLength > maxLength)
            {
                startIndex = (textLength - maxLength);

                // avoid splitting word by jumping to next space
                var nextSpace = text.IndexOf(' ', startIndex);
                if(nextSpace != -1)
                    startIndex = nextSpace + 1;

                sb.Append(MaxLengthCutoff);
                lineSize += MaxLengthCutoffSize;
            }

            for(int i = startIndex; i < textLength; ++i)
            {
                var chr = text[i];
                if(chr == '\n')
                {
                    if(SingleLineNames)
                        break;

                    lineSize = 0;
                    sb.Append('\n');
                    continue;
                }

                int chrSize = BuildInfoMod.Instance.FontsHandler.GetCharSize(chr);
                lineSize += chrSize;

                if(!SingleLineNames && chr == ' ')
                {
                    // find next space and determine if adding the word would go over the limit
                    int nextSpaceIndex = (textLength - 1);
                    int nextSizeAdd = 0;

                    for(int x = i + 1; x < textLength; ++x)
                    {
                        var chr2 = text[x];
                        nextSizeAdd += BuildInfoMod.Instance.FontsHandler.GetCharSize(chr2);

                        if(chr2 == ' ')
                        {
                            nextSpaceIndex = x;
                            break;
                        }
                    }

                    if((lineSize + nextSizeAdd) >= MaxLineSize)
                    {
                        lineSize = 0;
                        sb.Append('\n');
                        continue;
                    }
                }

                if(lineSize >= MaxLineSize)
                {
                    if(SingleLineNames)
                    {
                        if(((lineSize - chrSize) + NoWrapCutOffSize) >= MaxLineSize)
                            sb.Length -= 1;

                        sb.Append(NoWrapCutOff);
                        break;
                    }

                    lineSize = chrSize;
                    sb.Append('\n');
                }

                sb.Append(chr);
            }
        }
    }
}
