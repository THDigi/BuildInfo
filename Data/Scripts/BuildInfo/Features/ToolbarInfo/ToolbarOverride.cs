using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    /// <summary>
    /// Grabs all possible actions and wraps them in <see cref="ActionWrapper"/> which takes control over their Writer func.
    /// </summary>
    public class ToolbarOverride : ModComponent
    {
        public readonly Dictionary<IMyTerminalAction, ActionWrapper> ActionWrappers = new Dictionary<IMyTerminalAction, ActionWrapper>(16);
        FastResourceLock ActionWrappersLock = new FastResourceLock();

        public event Action<ActionWrapper> ActionCollected;

        readonly Queue<QueuedActionGet> QueuedTypes = new Queue<QueuedActionGet>();
        HashSet<Type> CheckedTypes = new HashSet<Type>();
        int RehookForSeconds = 10;

        Type CollectType;
        Func<ITerminalAction, bool> CollectFunc;

        // to check if the same id is added multiple times per block type
        Dictionary<MyTuple<string, string>, int> TypeActionIdPairs = new Dictionary<MyTuple<string, string>, int>(new MyTupleComparer<string, string>());
        const int TypeActionIdMaxTriggers = 5;

        const int TooManyActions = 50000; // as a last restort because the above can find repeats.
        bool AlertedForTooMany = false;

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

            CollectFunc = new Func<ITerminalAction, bool>(CollectAction);

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
                CollectType = data.BlockType;
                MyAPIGateway.TerminalActionsHelper.GetActions(data.BlockType, null, CollectFunc);

                // not using MyAPIGateway.TerminalControls.GetActions<T>() because it requires a generic type, can't feed that in
            }

            CheckInfiniteActions();

            if(RehookForSeconds <= 0 && QueuedTypes.Count <= 0)
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
        }

        bool CollectAction(ITerminalAction a)
        {
            IMyTerminalAction action = (IMyTerminalAction)a;
            if(!ActionWrappers.ContainsKey(action))
            {
                ActionWrapper wrapper = new ActionWrapper(action, CollectType?.Name ?? "type null!");
                ActionWrappers.Add(action, wrapper);
                ActionCollected?.Invoke(wrapper);
            }

            return false; // null list, never add to it.
        }

        // NOTE: can be called from a thread
        void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            try
            {
                int wrappersBefore = ActionWrappers.Count;

                // required to catch mod actions that are only added to this event
                for(int i = 0; i < actions.Count; i++)
                {
                    IMyTerminalAction action = actions[i];

                    using(ActionWrappersLock.AcquireExclusiveUsing())
                    {
                        if(ActionWrappers.ContainsKey(action))
                            continue;

                        if(block != null)
                        {
                            MyTuple<string, string> key = MyTuple.Create(block.GetType().Name, action.Id);
                            int triggered;
                            if(TypeActionIdPairs.TryGetValue(key, out triggered))
                            {
                                triggered++;
                                if(triggered == TypeActionIdMaxTriggers)
                                    Log.Error($"Action {action.Id} was added {TypeActionIdMaxTriggers} times before for type {key.Item1}, is a mod creating new instances in CustomActionGetter?");
                            }
                            else
                            {
                                triggered = 1;
                            }

                            TypeActionIdPairs[key] = triggered;
                        }

                        ActionWrapper wrapper = new ActionWrapper(action, block?.BlockDefinition.ToString() ?? "block null!");
                        ActionWrappers.Add(action, wrapper);
                        ActionCollected?.Invoke(wrapper);
                    }
                }

                if(BuildInfoMod.IsDevMod && wrappersBefore != ActionWrappers.Count)
                {
                    Log.Info($"CustomActionGetter(): Found {ActionWrappers.Count - wrappersBefore} new actions; total={ActionWrappers.Count}");
                }

                CheckInfiniteActions();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void CheckInfiniteActions()
        {
            if(!AlertedForTooMany && ActionWrappers.Count > TooManyActions)
            {
                const string File = "Error_TooManyActions.txt";

                Log.Error($"There's over {TooManyActions} actions! some mod must be creating them in realtime. Created {File} in storage folder with their list (next to the log).", Log.PRINT_MESSAGE);
                AlertedForTooMany = true;

                using(TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(File, typeof(ToolbarOverride)))
                {
                    writer.WriteLine("Grouped by ActionId and groups sorted descending by elements");
                    writer.WriteLine("<ActionId> | <ActionName> | <OriginalIcon> | <BlockSource>");

                    foreach(IGrouping<string, ActionWrapper> group in ActionWrappers.Values.GroupBy(a => a.Action.Id).OrderByDescending(g => g.Count()))
                    {
                        foreach(ActionWrapper wrapper in group)
                        {
                            writer.WriteLine($"{wrapper.Action.Id} | {wrapper.Action.Name} | {wrapper.OriginalIcon} | {wrapper.DebugSource}");
                        }
                    }

                    writer.WriteLine("fin.");
                }
            }
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
