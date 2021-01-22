using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using MyShipConnectorStatus = Sandbox.ModAPI.Ingame.MyShipConnectorStatus;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Connectors : StatusOverrideBase
    {
        public Connectors(ToolbarStatusProcessor processor) : base(processor)
        {
            var type = typeof(MyObjectBuilder_ShipConnector);

            processor.AddStatus(type, LockState, "Run", "RunWithDefaultArgument");

            // TODO: group support
            //processor.AddGroupStatus(type, StatusGroup, "Run", "RunWithDefaultArgument");
        }

        bool LockState(StringBuilder sb, ToolbarItem item)
        {
            var connector = (IMyShipConnector)item.Block;

            if(Processor.AnimFlip && !connector.IsWorking)
                sb.Append("OFF!\n");

            switch(connector.Status)
            {
                case MyShipConnectorStatus.Connected: sb.Append("Locked"); break;
                case MyShipConnectorStatus.Connectable: sb.Append("Ready"); break;
                case MyShipConnectorStatus.Unconnected: sb.Append("Unlocked"); break;
            }

            return true;
        }

        //bool StatusGroup(StringBuilder sb, ToolbarItem item, GroupData groupData)
        //{
        //    if(!groupData.GetGroupBlocks<CHANGE_ME>())
        //        return false;

        //    return true;
        //}
    }
}
