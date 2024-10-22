using System;
using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Features.BlockLogic
{
    public class BlockAttachedLogic : ModComponent
    {
        const bool DebugMode = false;

        internal readonly Dictionary<IMyCubeBlock, LogicBase> Tracked = new Dictionary<IMyCubeBlock, LogicBase>();

        List<LogicBase> Update1 = new List<LogicBase>();
        List<LogicBase> Update10 = new List<LogicBase>();
        List<LogicBase> Update100 = new List<LogicBase>();

        public BlockAttachedLogic(BuildInfoMod main) : base(main)
        {
            Register<MergeFailDetector>(typeof(MyObjectBuilder_MergeBlock));
            Register<UpgradeModuleIndicator>(typeof(MyObjectBuilder_UpgradeModule));
        }

        void Register<T>(MyObjectBuilderType blockType, params string[] subtypes) where T : LogicBase, new()
        {
            Main.BlockMonitor.MonitorType(blockType, new RegisteredLogic<T>(this, subtypes));
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        [Flags]
        internal enum BlockUpdate
        {
            None = 0,
            Update1 = (1 << 0),
            Update10 = (1 << 1),
            Update100 = (1 << 2),
        }

        internal abstract class LogicBase
        {
            public IMyCubeBlock Block { get; internal set; }
            public BlockAttachedLogic Host { get; internal set; }
            public BlockUpdate AssignedUpdates { get; private set; }

            public abstract void Added();

            public virtual void ReAdded()
            {
            }

            public virtual void Removed()
            {
            }

            public virtual void Update1()
            {
            }

            public virtual void Update10()
            {
            }

            public virtual void Update100()
            {
            }

            protected void SetUpdate(BlockUpdate flag, bool enabled)
            {
                if(flag == BlockUpdate.None)
                    throw new Exception("Can't set flag None to anything, use one of the actual flags");

                bool exists = (AssignedUpdates & flag) != 0;
                if(exists == enabled)
                    return; // no changes, ignore

                if(enabled)
                {
                    AssignedUpdates |= flag;

                    if((flag & BlockUpdate.Update1) != 0)
                        Host.Update1.Add(this);

                    if((flag & BlockUpdate.Update10) != 0)
                        Host.Update10.Add(this);

                    if((flag & BlockUpdate.Update100) != 0)
                        Host.Update100.Add(this);
                }
                else
                {
                    AssignedUpdates &= ~flag;

                    if((flag & BlockUpdate.Update1) != 0)
                        Host.Update1.Remove(this);

                    if((flag & BlockUpdate.Update10) != 0)
                        Host.Update10.Remove(this);

                    if((flag & BlockUpdate.Update100) != 0)
                        Host.Update100.Remove(this);
                }

                Host.RecheckUpdates();

                if(DebugMode)
                    DebugLog.PrintHUD(this, $"{flag} = {enabled}", log: true);
            }
        }

        class RegisteredLogic<T> : BlockMonitor.ICallback where T : LogicBase, new()
        {
            readonly BlockAttachedLogic Host;
            readonly HashSet<string> SubtypeFilter;

            public RegisteredLogic(BlockAttachedLogic host, string[] subtypes = null)
            {
                Host = host;

                if(subtypes != null && subtypes.Length > 0)
                    SubtypeFilter = new HashSet<string>(subtypes);
                else
                    SubtypeFilter = null;
            }

            void BlockMonitor.ICallback.BlockSpawned(IMySlimBlock slim)
            {
                if(slim == null)
                    throw new Exception("got null slimblock!");

                if(slim?.CubeGrid?.Physics == null)
                {
                    if(DebugMode)
                        DebugLog.PrintHUD(this, $"{slim.BlockDefinition.Id} ignored, ghost grid", log: true);

                    return; // skip ghost and projected grids
                }

                IMyCubeBlock block = slim.FatBlock;
                if(block == null)
                    throw new Exception($"Block has no FatBlock! DefId={slim.BlockDefinition.Id}");

                if(SubtypeFilter != null && !SubtypeFilter.Contains(slim.BlockDefinition.Id.SubtypeName))
                    return;

                LogicBase logic;
                if(Host.Tracked.TryGetValue(block, out logic))
                {
                    if(DebugMode)
                        DebugLog.PrintHUD(this, $"{block.BlockDefinition} <color=red>re-added!", log: true);

                    logic.ReAdded();
                    return;
                }

                logic = new T();
                logic.Host = Host;
                logic.Block = block;
                logic.Block.OnMarkForClose += BlockMarkedForClose;
                logic.Added();
                Host.Tracked.Add(block, logic);

                if(DebugMode)
                    DebugLog.PrintHUD(this, $"{block.BlockDefinition} added!", log: true);
            }

            void BlockMarkedForClose(IMyEntity ent)
            {
                try
                {
                    IMyCubeBlock block = (IMyCubeBlock)ent;

                    block.OnMarkForClose -= BlockMarkedForClose;

                    LogicBase logic;
                    if(!Host.Tracked.TryGetValue(block, out logic))
                        return;

                    try
                    {
                        if((logic.AssignedUpdates & BlockUpdate.Update1) != 0)
                            Host.Update1.Remove(logic);

                        if((logic.AssignedUpdates & BlockUpdate.Update10) != 0)
                            Host.Update10.Remove(logic);

                        if((logic.AssignedUpdates & BlockUpdate.Update100) != 0)
                            Host.Update100.Remove(logic);

                        logic.Removed();
                    }
                    finally
                    {
                        Host.Tracked.Remove(block);
                        Host.RecheckUpdates();

                        if(DebugMode)
                            DebugLog.PrintHUD(this, $"{block.BlockDefinition} removed!", log: true);
                    }
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        bool Updating = false;

        void RecheckUpdates()
        {
            bool hasUpdates = Update1.Count > 0 || Update10.Count > 0 || Update100.Count > 0;
            if(hasUpdates != Updating)
            {
                Updating = hasUpdates;
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, hasUpdates);

                if(DebugMode)
                    DebugLog.PrintHUD(this, $"BlockAttachedLogic {(hasUpdates ? "started updates" : "stopped updates")}", log: true);
            }
        }

        int IndexUpdate10;
        int IndexUpdate100;

        public override void UpdateAfterSim(int tick)
        {
            //if(DebugMode)
            //{
            //    MyAPIGateway.Utilities.ShowNotification($"{GetType().Name} updating all comps; update1={Update1.Count}; update10={Update10.Count}; update100={Update100.Count}", 16);
            //
            //    foreach(var logic in Update10)
            //    {
            //        MyAPIGateway.Utilities.ShowNotification($"- {((IMyTerminalBlock)logic.Block).CustomName}", 16);
            //    }
            //}

            for(int i = Update1.Count - 1; i >= 0; i--)
            {
                Update1[i].Update1();
            }

            {
                const int interval = 10;
                List<LogicBase> list = Update10;
                IndexUpdate10 = ((IndexUpdate10 + 1) % interval);

                for(int i = IndexUpdate10; i < list.Count; i += interval)
                {
                    list[i].Update10();
                }
            }

            {
                const int interval = 100;
                List<LogicBase> list = Update100;
                IndexUpdate100 = ((IndexUpdate100 + 1) % interval);

                for(int i = IndexUpdate100; i < list.Count; i += interval)
                {
                    list[i].Update100();
                }
            }
        }
    }
}
