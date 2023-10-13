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
using VRageMath;

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
        static readonly List<IMyTerminalAction> TempActions = new List<IMyTerminalAction>();

        protected override ListReader<IMyTerminalAction> GetActions(MyToolbarType? toolbarType)
        {
            try
            {
                TempBlocks.Clear();
                Group.GetBlocks(TempBlocks);

                TempBlockTypes.Clear();
                foreach(IMyTerminalBlock item in TempBlocks)
                {
                    TempBlockTypes.Add(item.GetType());
                }

                if(TempBlockTypes.Count == 1)
                    return GetValidActions(TempBlocks[0], TempBlocks);

                if(TempBlockTypes.Count == 0)
                    return ListReader<IMyTerminalAction>.Empty;

                return ListReader<IMyTerminalAction>.Empty;

                // I don't even.
                // code from MyToolbarItemTerminalGroup and attempted to decipher.
                /*
                List<IMyTerminalAction> results = GetValidActions(TempBlocks[0], new List<IMyTerminalBlock> { TempBlocks[0] });

                for(int i = 1; i < TempBlocks.Count; i++)
                {
                    IMyTerminalBlock item2 = TempBlocks[i];

                    List<IMyTerminalAction> list3 = new List<IMyTerminalAction>();
                    List<IMyTerminalAction> list4 = GetValidActions(item2, new List<IMyTerminalBlock> { item2 });

                    foreach(var item3 in results)
                    {
                        foreach(var item4 in list4)
                        {
                            if(item4.Id == item3.Id)
                            {
                                list3.Add(item3);
                                break;
                            }
                        }
                    }

                    results = list3;
                }
                return results;
                */
            }
            finally
            {
                TempBlocks.Clear();
            }
        }

        ListReader<IMyTerminalAction> GetValidActions(IMyTerminalBlock blockForType, ListReader<IMyTerminalBlock> blocks)
        {
            TempActions.Clear();

            // HACK: required like this to get ALL actions, the filled list only gets a specific toolbar type.
            blockForType.GetActions(null, (a) =>
            {
                var action = (IMyTerminalAction)a;
                if(action.ValidForGroups)
                {
                    bool validForAny = false;

                    foreach(IMyTerminalBlock b in blocks)
                    {
                        if(action.IsEnabled(b))
                        {
                            validForAny = true;
                            break;
                        }
                    }

                    if(validForAny)
                        TempActions.Add(action);
                }

                return false;
            });

            return new ListReader<IMyTerminalAction>(TempActions);
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
