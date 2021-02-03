using System.Text;
using Digi.BuildInfo.Utilities;
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

            Processor.AppendSingleStats(sb, item.Block);

            switch(connector.Status)
            {
                case MyShipConnectorStatus.Connected: sb.Append("Locked"); break;
                case MyShipConnectorStatus.Connectable: sb.Append("Proximity"); break;
                case MyShipConnectorStatus.Unconnected: sb.Append("Unlocked"); break;
            }

            return true;
        }

        bool GroupLockState(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyShipConnector>())
                return false;

            int broken = 0;
            int off = 0;
            int connected = 0;
            int ready = 0;
            int disconnected = 0;

            foreach(IMyShipConnector connector in groupData.Blocks)
            {
                if(!connector.IsFunctional)
                    broken++;

                if(!connector.Enabled)
                    off++;

                switch(connector.Status)
                {
                    case MyShipConnectorStatus.Connected: connected++; break;
                    case MyShipConnectorStatus.Connectable: ready++; break;
                    case MyShipConnectorStatus.Unconnected: disconnected++; break;
                }
            }

            int total = groupData.Blocks.Count;

            Processor.AppendGroupStats(sb, broken, off);

            if(connected == total)
            {
                sb.Append("All lock");
            }
            else if(ready == total)
            {
                sb.Append("All prox");
            }
            else if(disconnected == total)
            {
                sb.Append("All unlk");
            }
            else
            {
                if(connected > 0)
                    sb.NumberCapped(connected).Append(" lock");

                if(ready > 0)
                {
                    if(connected > 0)
                        sb.Append('\n');

                    sb.NumberCapped(ready).Append(" prox");
                }

                if(disconnected > 0)
                {
                    if(ready > 0 || connected > 0)
                        sb.Append('\n');

                    sb.NumberCapped(disconnected).Append(" unlk");
                }
            }

            return true;
        }
    }
}
