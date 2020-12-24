using System;
using System.Collections.Generic;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.Game;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarLabels
{
    public class ToolbarActionLabels : ModComponent
    {
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

        private readonly Dictionary<IMyTerminalAction, ActionWriterOverride> overriddenActions = new Dictionary<IMyTerminalAction, ActionWriterOverride>(16);

        public ToolbarActionLabels(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;
        }

        protected override void RegisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;

            MyVisualScriptLogicProvider.PlayerEnteredCockpit += EnteredCockpit;

            Main.Config.ToolbarActionLabels.ValueAssigned += ToolbarActionLabelModeChanged;
        }

        protected override void UnregisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;

            MyVisualScriptLogicProvider.PlayerEnteredCockpit -= EnteredCockpit;

            Main.Config.ToolbarActionLabels.ValueAssigned -= ToolbarActionLabelModeChanged;
        }

        void ToolbarActionLabelModeChanged(int oldValue, int newValue, SettingBase<int> setting)
        {
            if(oldValue != newValue)
            {
                foreach(var actionLabel in overriddenActions.Values)
                {
                    actionLabel.SettingChanged(newValue);
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

        void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            try
            {
                if(Main.Config.ToolbarActionLabels.Value == 0)
                    return;

                for(int i = 0; i < actions.Count; i++)
                {
                    IMyTerminalAction action = actions[i];

                    if(!overriddenActions.ContainsKey(action))
                    {
                        overriddenActions.Add(action, new ActionWriterOverride(action));
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(EnteredCockpitTicks > 0)
            {
                EnteredCockpitTicks--;
            }
        }

        #region Custom name caching
        private readonly Dictionary<long, string> cachedCustomName = new Dictionary<long, string>();

        public string GetCustomName(long entityId)
        {
            return cachedCustomName.GetValueOrDefault(entityId, null);
        }

        public void AddCustomNameCache(IMyTerminalBlock block, string parsedName)
        {
            cachedCustomName[block.EntityId] = parsedName;

            block.CustomNameChanged += Block_CustomNameChanged;
            block.OnMarkForClose += Block_OnMarkForClose;
        }

        private void Block_CustomNameChanged(IMyTerminalBlock block)
        {
            cachedCustomName.Remove(block.EntityId);
        }

        private void Block_OnMarkForClose(IMyEntity ent)
        {
            var block = (IMyTerminalBlock)ent;
            block.CustomNameChanged -= Block_CustomNameChanged;
            block.OnMarkForClose -= Block_OnMarkForClose;
            cachedCustomName.Remove(block.EntityId);
        }
        #endregion
    }
}
