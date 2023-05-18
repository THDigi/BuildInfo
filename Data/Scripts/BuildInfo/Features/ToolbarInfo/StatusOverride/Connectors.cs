using System;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Library.Utils;
using MyShipConnectorStatus = Sandbox.ModAPI.Ingame.MyShipConnectorStatus;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Connectors : StatusOverrideBase
    {
        public Connectors(ToolbarStatusProcessor processor) : base(processor)
        {
            Type type = typeof(MyObjectBuilder_ShipConnector);

            processor.AddStatus(type, LockState, "SwitchLock", "Lock", "Unlock");

            processor.AddGroupStatus(type, GroupLockState, "SwitchLock", "Lock", "Unlock");
        }

        bool LockState(StringBuilder sb, ToolbarItem item)
        {
            IMyShipConnector connector = (IMyShipConnector)item.Block;

            Processor.AppendSingleStats(sb, item.Block);

            switch(connector.Status)
            {
                case MyShipConnectorStatus.Connected: sb.Append("Locked"); break;
                case MyShipConnectorStatus.Connectable: sb.Append("Proxy"); break;
                case MyShipConnectorStatus.Unconnected: sb.Append("Unlocked"); break;
                default: sb.AppendMaxLength(MyEnum<MyShipConnectorStatus>.GetName(connector.Status), MaxChars, addDots: false); break;
            }

            return true;
        }

        bool GroupLockState(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
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
                    sb.NumberCapped(connected, MaxChars - 4).Append("lock\n");

                if(ready > 0)
                    sb.NumberCapped(ready, MaxChars - 4).Append("prox\n");

                if(disconnected > 0)
                    sb.NumberCapped(disconnected, MaxChars - 4).Append("unlk\n");

                sb.TrimEndWhitespace();
            }

            return true;
        }
    }
}
