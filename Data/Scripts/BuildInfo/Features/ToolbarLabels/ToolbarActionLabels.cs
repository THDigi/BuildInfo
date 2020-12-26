using System;
using System.Collections.Generic;
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
        public static readonly bool TOOLBAR_DEBUG_LOGGING = false;

        public const int CockpitLabelsForTicks = Constants.TICKS_PER_SECOND * 3;

        public int EnteredCockpitTicks { get; private set; } = CockpitLabelsForTicks;

        public bool IsActionUseful(string actionId) => usefulActions.Contains(actionId);
        private readonly HashSet<string> usefulActions = new HashSet<string>() // shows block name for these actions if the "useful" setting is used
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

        private bool preRegister = true;

        private readonly Dictionary<IMyTerminalAction, ActionWriterOverride> overriddenActions = new Dictionary<IMyTerminalAction, ActionWriterOverride>(16);

        public ToolbarActionLabels(BuildInfoMod main) : base(main)
        {
            preRegister = true;

            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;

            Main.BlockMonitor.BlockAdded += BlockMonitor_BlockAdded;
        }

        protected override void RegisterComponent()
        {
            preRegister = false;

            ComputePreRegisterActions();

            MyVisualScriptLogicProvider.PlayerEnteredCockpit += EnteredCockpit;

            Main.Config.ToolbarActionLabels.ValueAssigned += ToolbarActionLabelModeChanged;
        }

        protected override void UnregisterComponent()
        {
            MyVisualScriptLogicProvider.PlayerEnteredCockpit -= EnteredCockpit;

            Main.Config.ToolbarActionLabels.ValueAssigned -= ToolbarActionLabelModeChanged;

            Main.BlockMonitor.BlockAdded -= BlockMonitor_BlockAdded;

            foreach(var actionOverride in overriddenActions.Values)
            {
                actionOverride?.Cleanup();
            }

            overriddenActions.Clear();
        }

        void ToolbarActionLabelModeChanged(int oldValue, int newValue, SettingBase<int> setting)
        {
            if(oldValue != newValue)
            {
                var mode = (ToolbarActionLabelsMode)newValue;

                foreach(var actionLabel in overriddenActions.Values)
                {
                    actionLabel.SettingChanged(mode);
                }
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

            // HACK: GetActions()'s collect function gets called before the toolbar check, allowing to get all actions.
            // HACK #2: CustomActionGetter event doesn't get triggered for groups, making them undetectable until you see that action for a single block.
            block.GetActions(null, CollectAction);
        }

        bool CollectAction(ITerminalAction a)
        {
            var action = (IMyTerminalAction)a;

            if(!overriddenActions.ContainsKey(action))
            {
                if(TOOLBAR_DEBUG_LOGGING)
                    Log.Info($"Added action: {action.Id}");

                // HACK: if it's before Register() then create it later as ActionWriterOverride() can't be created so early.
                if(preRegister)
                    overriddenActions.Add(action, null);
                else
                    overriddenActions.Add(action, new ActionWriterOverride(action));
            }

            return false; // null list, never add to it.
        }

        void ComputePreRegisterActions()
        {
            foreach(var action in new List<IMyTerminalAction>(overriddenActions.Keys))
            {
                if(overriddenActions[action] == null)
                    overriddenActions[action] = new ActionWriterOverride(action);
            }
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(TOOLBAR_DEBUG_LOGGING)
                Log.Info("---- AFTER SIM -----------------------------------");

            if(EnteredCockpitTicks > 0)
            {
                EnteredCockpitTicks--;
            }
        }
    }
}
