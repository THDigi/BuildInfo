using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    /// <summary>
    /// Grabs all possible actions and wraps them in <see cref="ActionWrapper"/> which takes control over their Writer func.
    /// </summary>
    public class ToolbarOverride : ModComponent
    {
        public readonly Dictionary<IMyTerminalAction, ActionWrapper> ActionWrappers = new Dictionary<IMyTerminalAction, ActionWrapper>(16);

        public event Action<ActionWrapper> ActionCollected;

        readonly Func<ITerminalAction, bool> CollectActionFunc;
        readonly Queue<QueuedActionGet> QueuedTypes = new Queue<QueuedActionGet>();
        HashSet<Type> CheckedTypes = new HashSet<Type>();
        int RehookForSeconds = 10;

        struct QueuedActionGet
        {
            public readonly int ReadAtTick;
            public readonly Type BlockType;

            public QueuedActionGet(int readAtTick, Type blockType)
            {
                ReadAtTick = readAtTick;
                BlockType = blockType;
            }
        }

        public ToolbarOverride(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;

            CollectActionFunc = new Func<ITerminalAction, bool>(CollectAction);

            Main.BlockMonitor.BlockAdded += BlockAdded;

            MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;
        }

        public override void RegisterComponent()
        {
            Main.Config.ToolbarActionIcons.ValueAssigned += ToolbarActionIcons_ValueAssigned;
        }

        public override void UnregisterComponent()
        {
            Main.BlockMonitor.BlockAdded -= BlockAdded;

            MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;

            if(!Main.ComponentsRegistered)
                return;

            Main.Config.ToolbarActionIcons.ValueAssigned -= ToolbarActionIcons_ValueAssigned;
        }

        void BlockAdded(IMySlimBlock slimBlock)
        {
            IMyTerminalBlock block = slimBlock.FatBlock as IMyTerminalBlock;
            if(block == null)
                return;

            if(CheckedTypes.Contains(block.GetType()))
                return;

            QueuedTypes.Enqueue(new QueuedActionGet(Main.Tick + 60, block.GetType()));
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void UpdateAfterSim(int tick)
        {
            if(RehookForSeconds > 0 && tick % Constants.TicksPerSecond == 0)
            {
                // HACK: must register late as possible
                MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;
                MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;
                RehookForSeconds--;
            }

            while(QueuedTypes.Count > 0 && QueuedTypes.Peek().ReadAtTick <= tick)
            {
                QueuedActionGet data = QueuedTypes.Dequeue();

                // no remove from CheckedType, any new real-time-added actions should be caught by the CustomActionGetter... unless it's only used in a group.

                // HACK: CustomActionGetter event doesn't get triggered for groups, making them undetectable until you see that action for a single block.
                // HACK: GetActions()'s collect function gets called before the toolbar check, allowing to get all actions.
                // HACK: can't call it in BlockAdded because it can make some mods' terminal controls vanish...
                MyAPIGateway.TerminalActionsHelper.GetActions(data.BlockType, null, CollectActionFunc);

                // not using MyAPIGateway.TerminalControls.GetActions<T>() because it requires a generic type, can't feed that in
            }

            if(RehookForSeconds <= 0 && QueuedTypes.Count <= 0)
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

            // TODO: make custom icons toggleable
            // needs a way to refresh toolbar...
            //if(MyAPIGateway.Input.IsNewKeyPressed(VRage.Input.MyKeys.L) && MyAPIGateway.Input.IsAnyCtrlKeyPressed())
            //{
            //    foreach(ActionWrapper wrapper in ActionWrappers.Values)
            //    {
            //        if(wrapper.CustomIcon == null)
            //            continue;
            //
            //        if(wrapper.Action.Icon == wrapper.CustomIcon)
            //            wrapper.Action.Icon = wrapper.OriginalIcon;
            //        else
            //            wrapper.Action.Icon = wrapper.CustomIcon;
            //    }
            //
            //    MyAPIGateway.Utilities.ShowNotification("Toggled action icons", 2000, "Debug");
            //}
        }

        void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            // required to catch mod actions that are only added to this event
            for(int i = 0; i < actions.Count; i++)
            {
                CollectAction(actions[i]);
            }
        }

        bool CollectAction(ITerminalAction a)
        {
            IMyTerminalAction action = (IMyTerminalAction)a;

            if(!ActionWrappers.ContainsKey(action))
            {
                ActionWrapper wrapper = new ActionWrapper(action);
                ActionWrappers.Add(action, wrapper);

                ActionCollected?.Invoke(wrapper);
            }

            return false; // null list, never add to it.
        }

        void ToolbarActionIcons_ValueAssigned(int oldValue, int newValue, ConfigLib.SettingBase<int> setting)
        {
            foreach(ActionWrapper wrapper in ActionWrappers.Values)
            {
                wrapper.UpdateIcon();
            }

            // TODO: find a way to refresh them visually ingame too, so it doesn't require fiddling with them...
        }
    }
}
