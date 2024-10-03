using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Entity;

namespace Digi.BuildInfo.Features.Toolbars.FakeAPI.Items
{
    public class ActionCount
    {
        public int Count;
        public IMyTerminalAction Action;
    }

    public class ToolbarItemTerminalGroup : ToolbarItemWithAction
    {
        public IMyBlockGroup Group { get; protected set; }

        protected override bool Init(MyObjectBuilder_ToolbarItem data)
        {
            if(!base.Init(data))
                return false;

            var ob = (MyObjectBuilder_ToolbarItemTerminalGroup)data;
            if(ob.BlockEntityId == 0)
                return false;

            MyEntity ent;
            if(!MyEntities.TryGetEntityById(ob.BlockEntityId, out ent))
                return false;

            var block = ent as IMyTerminalBlock;
            if(block?.CubeGrid == null)
                return false;

            IMyGridTerminalSystem gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(block.CubeGrid);
            if(gts == null)
                return false;

            Group = gts.GetBlockGroupWithName(ob.GroupName);
            if(Group == null)
                return false;

            SetAction(ob._Action);
            return true;
        }

        static readonly Dictionary<Type, IMyTerminalBlock> TempBlockTypes = new Dictionary<Type, IMyTerminalBlock>(16);
        static readonly Dictionary<string, ActionCount> TempActionsById = new Dictionary<string, ActionCount>(Caches.ExpectedActions);

        protected override void GetActions(MyToolbarType? toolbarType, List<IMyTerminalAction> results)
        {
            try
            {
                results.Clear();

                TempBlocks.Clear();
                Group.GetBlocks(TempBlocks);

                TempBlockTypes.Clear();

                // HACK: clone of MyToolbarItemTerminalGroup.GetActions(), with optimizations
                //bool genericType;
                bool allFunctional = true;

                foreach(IMyTerminalBlock block in TempBlocks)
                {
                    allFunctional = (allFunctional && block is IMyFunctionalBlock);
                    TempBlockTypes[block.GetType()] = block;
                }

                if(TempBlockTypes.Count == 1)
                {
                    GetValidActions(TempBlocks[0], results);

                    // TODO is this method really required?
                    //GetValidActions(TempBlocks[0], TempBlocks, results);
                    return;
                }

                if(TempBlockTypes.Count == 0 || !allFunctional)
                {
                    //genericType = true;
                    return;
                }

                //genericType = true;

                #region Actions that exist on all types
                TempActionsById.Clear();
                int blockTypes = TempBlockTypes.Count;
                Stack<ActionCount> pool = BuildInfoMod.Instance.Caches.PoolActionCounted;

                // first, tally actions by Id (not instance because there can be different instances with same Id)
                foreach(IMyTerminalBlock block in TempBlockTypes.Values)
                {
                    // abusing results list as a temporary list
                    results.Clear();
                    GetValidActions(block, results);

                    foreach(IMyTerminalAction action in results)
                    {
                        ActionCount ac;
                        if(!TempActionsById.TryGetValue(action.Id, out ac))
                        {
                            ac = (pool.Count > 0 ? pool.Pop() : new ActionCount());
                            ac.Action = action;
                            ac.Count = 0;
                            TempActionsById[action.Id] = ac;
                        }

                        ac.Count++;
                    }
                }

                // second, return only actions that are as many as the block types
                results.Clear();

                foreach(ActionCount ac in TempActionsById.Values)
                {
                    if(ac.Count == blockTypes)
                    {
                        results.Add(ac.Action);
                    }

                    ac.Action = null;
                    pool.Push(ac);
                }
                #endregion

                // TODO is this necessary to do for all blocks?
                /*
                #region Get only actions that exist for all blocks
                TempActionsById.Clear();
                int totalBlocks = TempBlocks.Count;
                Stack<ActionCounted> pool = BuildInfoMod.Instance.Caches.PoolActionCounted;

                // first, count all actions by Id
                for(int i = 0; i < totalBlocks; i++)
                {
                    IMyTerminalBlock block = TempBlocks[i];

                    // abusing results list as a temporary list
                    results.Clear();
                    GetValidActions(block, results);

                    foreach(IMyTerminalAction action in results)
                    {
                        ActionCounted ac;
                        if(!TempActionsById.TryGetValue(action.Id, out ac))
                        {
                            ac = (pool.Count > 0 ? pool.Pop() : new ActionCounted());
                            ac.Action = action;
                            ac.Count = 0;
                            TempActionsById[action.Id] = ac;
                        }

                        ac.Count++;
                    }
                }

                // secondly, return only actions that are as many as the blocks
                results.Clear();

                foreach(var ac in TempActionsById.Values)
                {
                    if(ac.Count == totalBlocks)
                    {
                        results.Add(ac.Action);
                    }

                    ac.Action = null;
                    pool.Push(ac);
                }
                #endregion
                */
            }
            finally
            {
                TempBlocks.Clear();
                TempActionsById.Clear();
            }
        }

        //static void GetValidActions(IMyTerminalBlock blockForType, ListReader<IMyTerminalBlock> blocks, List<IMyTerminalAction> results)
        //{
        //    // HACK: required to use the lambda because there's an extra condition after it's called: action.IsValidForToolbarType(MyToolbarType.ButtonPanel)
        //    // this is the same for SearchActionsOfName() and GetActionWithName().
        //    blockForType.GetActions(null, (a) =>
        //    {
        //        var action = (IMyTerminalAction)a;
        //        if(!action.ValidForGroups)
        //            return false;
        //
        //        foreach(IMyTerminalBlock b in blocks)
        //        {
        //            if(action.IsEnabled(b))
        //            {
        //                results.Add(action);
        //                break;
        //            }
        //        }
        //
        //        return false;
        //    });
        //}

        static void GetValidActions(IMyTerminalBlock block, List<IMyTerminalAction> results)
        {
            // HACK: required to use the lambda because there's an extra condition after it's called: action.IsValidForToolbarType(MyToolbarType.ButtonPanel)
            // this is the same for SearchActionsOfName() and GetActionWithName().
            block.GetActions(null, (a) =>
            {
                var action = (IMyTerminalAction)a;
                if(action.ValidForGroups && action.IsEnabled(block))
                {
                    results.Add(action);
                }

                return false;
            });
        }

        public override string ToString()
        {
            return $"{GetType().Name}(''{(Group?.Name ?? "null")}'' Action={Action?.Id})";
        }

        public override void AppendFancyRender(StringBuilder sb, float opacity)
        {
            if(Group == null) throw new ArgumentNullException("Group");

            sb.Color(ToolbarRender.GroupColor * opacity).Append('*');

            int maxNameLength = (PBArg != null ? ToolbarRender.MaxNameLengthIfPbArg : ToolbarRender.MaxNameLength);
            sb.AppendMaxLength(Group.Name, maxNameLength).ResetFormatting();

            sb.Color(ToolbarRender.GroupColor * opacity).Append('*');

            AppendActionName(sb, opacity);
        }
    }
}
