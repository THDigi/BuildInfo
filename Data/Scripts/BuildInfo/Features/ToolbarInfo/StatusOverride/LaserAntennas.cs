using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using VRageMath;
using MyLaserAntennaStatus = Sandbox.ModAPI.Ingame.MyLaserAntennaStatus;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class LaserAntennas : StatusOverrideBase
    {
        public LaserAntennas(ToolbarStatusProcessor processor) : base(processor)
        {
            Type type = typeof(MyObjectBuilder_LaserAntenna);

            processor.AddStatus(type, Status, "Idle", "PasteGpsCoords", "ConnectGPS");
            processor.AddGroupStatus(type, GroupStatus, "Idle", "PasteGpsCoords", "ConnectGPS");
        }

        string[] ConnectingAnim =
        {
            "   ",
            ".  ",
            ".. ",
            "...",
        };
        bool TickTock;
        int ConnectAnimIdx;

        bool Status(StringBuilder sb, ToolbarItem item)
        {
            IMyLaserAntenna laserAntenna = (IMyLaserAntenna)item.Block;

            Processor.AppendSingleStats(sb, item.Block);

            if(TickTock != Processor.AnimFlip)
            {
                TickTock = Processor.AnimFlip;
                ConnectAnimIdx = (ConnectAnimIdx + 1) % ConnectingAnim.Length;
            }

            if(laserAntenna.Other != null)
            {
                float distance = (float)Vector3D.Distance(laserAntenna.GetPosition(), laserAntenna.Other.GetPosition());

                if(distance >= 1000)
                    sb.NumberCapped((int)(distance / 1000f), MaxChars - 2).Append("km");
                else
                    sb.Append((int)distance).Append("m");

                sb.Append('\n');
            }

            switch(laserAntenna.Status)
            {
                case MyLaserAntennaStatus.Connected: sb.Append(IconGood).Append("Online"); break;
                case MyLaserAntennaStatus.Idle: sb.Append(IconBad).Append("Offline"); break;
                case MyLaserAntennaStatus.OutOfRange: sb.Append(IconAlert).Append("Angle!"); break;
                case MyLaserAntennaStatus.Connecting: sb.Append(IconAlert).Append("Con").Append(ConnectingAnim[ConnectAnimIdx]); break;
                case MyLaserAntennaStatus.RotatingToTarget: sb.Append(IconAlert).Append("Rot").Append(ConnectingAnim[ConnectAnimIdx]); break;
                case MyLaserAntennaStatus.SearchingTargetForAntenna: sb.Append(IconAlert).Append("Src").Append(ConnectingAnim[ConnectAnimIdx]); break;
                default: return false;
            }

            return true;
        }

        bool GroupStatus(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyLaserAntenna>())
                return false;

            int broken = 0;
            int off = 0;

            TempUniqueInt.Clear();

            foreach(IMyLaserAntenna la in groupData.Blocks)
            {
                if(!la.IsFunctional)
                    broken++;

                if(!la.Enabled)
                    off++;

                TempUniqueInt.Add((int)la.Status);
            }

            Processor.AppendGroupStats(sb, broken, off);

            if(TempUniqueInt.Count == 0)
            {
                sb.Append("Unknown");
            }
            else if(TempUniqueInt.Count > 1)
            {
                sb.Append(IconAlert).Append("Mixed");
            }
            else // only one in list, meaning all blocks have same mode
            {
                sb.Append("All:\n");

                MyLaserAntennaStatus status = (MyLaserAntennaStatus)TempUniqueInt.FirstElement();
                switch(status)
                {
                    case MyLaserAntennaStatus.Connected: sb.Append(IconGood).Append("Online"); break;
                    case MyLaserAntennaStatus.Idle: sb.Append(IconBad).Append("Offline"); break;
                    case MyLaserAntennaStatus.OutOfRange: sb.Append(IconAlert).Append("Angle!"); break;
                    case MyLaserAntennaStatus.Connecting: sb.Append(IconAlert).Append("Con").Append(ConnectingAnim[ConnectAnimIdx]); break;
                    case MyLaserAntennaStatus.RotatingToTarget: sb.Append(IconAlert).Append("Rot").Append(ConnectingAnim[ConnectAnimIdx]); break;
                    case MyLaserAntennaStatus.SearchingTargetForAntenna: sb.Append(IconAlert).Append("Src").Append(ConnectingAnim[ConnectAnimIdx]); break;
                    default: return false;
                }
            }

            return true;
        }
    }
}
