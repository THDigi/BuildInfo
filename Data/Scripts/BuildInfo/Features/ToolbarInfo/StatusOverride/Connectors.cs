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

            processor.AddStatus(type, LockState, "SwitchLock", "Lock", "Unlock");

            processor.AddGroupStatus(type, GroupLockState, "SwitchLock", "Lock", "Unlock");
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

        bool GroupLockState(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyShipConnector>())
                return false;

            int connected = 0;
            int ready = 0;
            int disconnected = 0;

            foreach(IMyShipConnector connector in groupData.Blocks)
            {
                switch(connector.Status)
                {
                    case MyShipConnectorStatus.Connected: connected++; break;
                    case MyShipConnectorStatus.Connectable: ready++; break;
                    case MyShipConnectorStatus.Unconnected: disconnected++; break;
                }
            }

            int total = groupData.Blocks.Count;

            if(connected == total)
            {
                sb.Append("All\nLocked");
            }
            else if(ready == total)
            {
                sb.Append("All\nReady");
            }
            else if(disconnected == total)
            {
                sb.Append("All\nUnlocked");
            }
            else
            {
                if(connected > 0)
                    sb.Append(connected).Append(" lock");

                if(ready > 0)
                {
                    if(connected > 0)
                        sb.Append('\n');

                    sb.Append(ready).Append(" rdy");
                }

                if(disconnected > 0)
                {
                    if(ready > 0 || connected > 0)
                        sb.Append('\n');

                    sb.Append(disconnected).Append(" unlk");
                }
            }

            return true;
        }
    }
}
