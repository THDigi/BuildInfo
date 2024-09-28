using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;

namespace Digi.BuildInfo.Features.Toolbars.FakeAPI.Items
{
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

        static readonly HashSet<Type> TempBlockTypes = new HashSet<Type>();
        static readonly List<IMyTerminalBlock> TempBlocks = new List<IMyTerminalBlock>();

        protected override ListReader<IMyTerminalAction> GetActions(MyToolbarType? toolbarType)
        {
            try
            {
                TempBlocks.Clear();
                Group.GetBlocks(TempBlocks);

                TempBlockTypes.Clear();

                // HACK: clone of MyToolbarItemTerminalGroup.GetActions()...
                bool genericType;
                bool flag = true;

                foreach(var tb in TempBlocks)
                {
                    flag = (flag && tb is IMyFunctionalBlock);
                    TempBlockTypes.Add(tb.GetType());
                }

                if(TempBlockTypes.Count == 1)
                {
                    genericType = false;
                    return GetValidActions(TempBlocks[0], TempBlocks);
                }

                if(TempBlockTypes.Count == 0 || !flag)
                {
                    genericType = true;
                    return ListReader<IMyTerminalAction>.Empty;
                }

                genericType = true;

                ListReader<IMyTerminalAction> results = GetValidActions(TempBlocks[0]);

                // start from index 1
                for(int i = 1; i < TempBlocks.Count; i++)
                {
                    IMyTerminalBlock tb = TempBlocks[i];

                    List<IMyTerminalAction> filterMore = new List<IMyTerminalAction>(32);
                    ListReader<IMyTerminalAction> actions2 = GetValidActions(tb);

                    foreach(IMyTerminalAction actionA in results)
                    {
                        foreach(IMyTerminalAction actionB in actions2)
                        {
                            if(actionB.Id == actionA.Id)
                            {
                                filterMore.Add(actionA);
                                break;
                            }
                        }
                    }

                    results = filterMore;
                }

                return results;
            }
            finally
            {
                TempBlocks.Clear();
            }
        }

        static ListReader<IMyTerminalAction> GetValidActions(IMyTerminalBlock blockForType, ListReader<IMyTerminalBlock> blocks)
        {
            var actions = new List<IMyTerminalAction>(32); // not worth optimizing, risky

            // HACK: required like this to get ALL actions, the filled list only gets a specific toolbar type.
            blockForType.GetActions(null, (a) =>
            {
                var action = (IMyTerminalAction)a;
                if(action.ValidForGroups)
                {
                    foreach(IMyTerminalBlock b in blocks)
                    {
                        if(action.IsEnabled(b))
                        {
                            actions.Add(action);
                            break;
                        }
                    }
                }

                return false;
            });

            return actions;
        }

        static ListReader<IMyTerminalAction> GetValidActions(IMyTerminalBlock block)
        {
            var actions = new List<IMyTerminalAction>(32); // not worth optimizing, risky

            // HACK: required like this to get ALL actions, the filled list only gets a specific toolbar type.
            block.GetActions(null, (a) =>
            {
                var action = (IMyTerminalAction)a;
                if(action.ValidForGroups && action.IsEnabled(block))
                {
                    actions.Add(action);
                }

                return false;
            });

            return actions;
        }

        public override string ToString()
        {
            return $"{GetType().Name}(''{(Group?.Name ?? "null")}'' Action={Action?.Id})";
        }

        public override void AppendFancyRender(StringBuilder sb, float opacity)
        {
            if(Group == null) throw new ArgumentNullException("Group");

            sb.ColorA(ToolbarRender.GroupColor * opacity).Append('*');

            int maxNameLength = (PBArg != null ? ToolbarRender.MaxNameLengthIfPbArg : ToolbarRender.MaxNameLength);
            sb.AppendMaxLength(Group.Name, maxNameLength).ResetFormatting();

            sb.ColorA(ToolbarRender.GroupColor * opacity).Append('*');

            AppendActionName(sb, opacity);
        }
    }
}
