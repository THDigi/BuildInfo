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
    public class ToolbarItemTerminalBlock : ToolbarItemWithAction
    {
        public IMyTerminalBlock Block { get; private set; }

        //List<TerminalActionParameter> Parameters = new List<TerminalActionParameter>();

        protected override bool Init(MyObjectBuilder_ToolbarItem data)
        {
            Block = null;

            if(!base.Init(data))
                return false;

            var ob = (MyObjectBuilder_ToolbarItemTerminalBlock)data;
            if(ob.BlockEntityId == 0)
                return false;

            MyEntity ent;
            if(MyEntities.TryGetEntityById(ob.BlockEntityId, out ent))
                Block = ent as IMyTerminalBlock;

            if(Block == null)
                return false;

            SetAction(ob._Action);

            if(ob.Parameters != null && ob.Parameters.Count > 0)
            {
                //Parameters.Clear();
                //
                //foreach(MyObjectBuilder_ToolbarItemActionParameter parameter in ob.Parameters)
                //{
                //    Parameters.Add(TerminalActionParameter.Deserialize(parameter.Value, parameter.TypeCode));
                //}

                // HACK: major assumptions here, but there's no other use case and some stuff is prohibited so just w/e
                if(ob._Action == "Run")
                {
                    string arg = ob.Parameters[0]?.Value;
                    if(!string.IsNullOrEmpty(arg))
                        PBArg = arg;
                }
            }

            return true;
        }

        static readonly List<IMyTerminalAction> TempActions = new List<IMyTerminalAction>();
        static readonly HashSet<string> TempExistingActions = new HashSet<string>();

        protected override ListReader<IMyTerminalAction> GetActions(MyToolbarType? toolbarType)
        {
            TempActions.Clear();
            TempExistingActions.Clear();

            // HACK: required like this to get ALL actions, the filled list only gets a specific toolbar type.
            Block.GetActions(null, (a) =>
            {
                var action = (IMyTerminalAction)a;

                if(action.IsEnabled(Block) && (toolbarType == null || action.InvalidToolbarTypes.Contains(toolbarType.Value)))
                {
                    if(TempExistingActions.Add(action.Id))
                        TempActions.Add(action);
                }

                return false;
            });

            return new ListReader<IMyTerminalAction>(TempActions);
        }

        public override string ToString()
        {
            return $"{GetType().Name}(''{Block?.CustomName}'' Action={Action?.Id})";
        }

        public override void AppendFancyRender(StringBuilder sb, float opacity)
        {
            if(Block == null) throw new ArgumentNullException("Block");

            // TODO: support for ToolbarCustomLabels and NamesMode if this ever gets used for HUD

            int maxNameLength = (PBArg != null ? ToolbarRender.MaxNameLengthIfPbArg : ToolbarRender.MaxNameLength);
            sb.AppendMaxLength(Block.CustomName, maxNameLength).ResetFormatting();

            AppendActionName(sb, opacity);
        }
    }
}
