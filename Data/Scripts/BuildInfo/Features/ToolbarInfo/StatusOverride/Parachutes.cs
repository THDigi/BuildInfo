using System;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using SpaceEngineers.Game.ModAPI;
using DoorStatus = Sandbox.ModAPI.Ingame.DoorStatus;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Parachutes : StatusOverrideBase
    {
        public Parachutes(ToolbarStatusProcessor processor) : base(processor)
        {
            Type type = typeof(MyObjectBuilder_Parachute);

            processor.AddStatus(type, Deploy, "Open", "Open_On", "Open_Off");
            processor.AddStatus(type, AutoDeploy, "AutoDeploy"); // vanilla status is borked

            processor.AddGroupStatus(type, GroupDeploy, "Open", "Open_On", "Open_Off");
            processor.AddGroupStatus(type, GroupAutoDeploy, "AutoDeploy");
        }

        bool Deploy(StringBuilder sb, ToolbarItem item)
        {
            IMyParachute parachute = (IMyParachute)item.Block;
            bool hasAmmo = true;

            MyInventory inv = parachute.GetInventory() as MyInventory;
            if(inv != null)
            {
                MyParachuteDefinition def = (MyParachuteDefinition)item.Block.SlimBlock.BlockDefinition;
                float foundItems = (float)inv.GetItemAmount(def.MaterialDefinitionId);
                hasAmmo = (foundItems >= def.MaterialDeployCost);
            }

            Processor.AppendSingleStats(sb, item.Block);

            if(!Processor.AnimFlip && !hasAmmo && parachute.Status != DoorStatus.Open)
                sb.Append("Empty!\n");

            switch(parachute.Status)
            {
                case DoorStatus.Opening: sb.Append("Opening"); break;
                case DoorStatus.Closing: sb.Append("Closing"); break;
                case DoorStatus.Open: sb.Append("Deployed"); break;
                case DoorStatus.Closed: sb.Append("Ready"); break;
                default: return false;
            }

            return true;
        }

        bool AutoDeploy(StringBuilder sb, ToolbarItem item)
        {
            IMyParachute parachute = (IMyParachute)item.Block;

            if(parachute.AutoDeploy)
            {
                if(!Processor.AppendSingleStats(sb, item.Block))
                    sb.Append("Auto\n");

                sb.DistanceFormat(parachute.AutoDeployHeight, 2);
            }
            else
            {
                sb.Append("Manual");
            }

            return true;
        }

        bool GroupDeploy(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyParachute>())
                return false;

            int broken = 0;
            int off = 0;
            int open = 0;
            int ready = 0;
            bool allAmmo = true;

            foreach(IMyParachute parachute in groupData.Blocks)
            {
                if(!parachute.IsFunctional)
                    broken++;

                if(!parachute.Enabled)
                    off++;

                if(parachute.Status != DoorStatus.Open)
                {
                    MyInventory inv = parachute.GetInventory() as MyInventory;
                    if(inv != null)
                    {
                        MyParachuteDefinition def = (MyParachuteDefinition)parachute.SlimBlock.BlockDefinition;
                        float foundItems = (float)inv.GetItemAmount(def.MaterialDefinitionId);
                        if(foundItems < def.MaterialDeployCost)
                            allAmmo = false;
                    }
                }

                switch(parachute.Status)
                {
                    case DoorStatus.Open: open++; break;
                    case DoorStatus.Closed: ready++; break;
                }
            }

            Processor.AppendGroupStats(sb, broken, off);

            if(!Processor.AnimFlip && !allAmmo)
                sb.Append("Empty!\n");

            int total = groupData.Blocks.Count;

            if(open == total)
            {
                sb.Append("AllOpen");
            }
            else if(open == 0)
            {
                sb.Append("AllReady");
            }
            else
            {
                sb.NumberCappedSpaced(open, MaxChars - 4).Append("open\n");
                sb.NumberCappedSpaced(ready, MaxChars - 3).Append("rdy");
            }

            return true;
        }

        bool GroupAutoDeploy(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyParachute>())
                return false;

            int broken = 0;
            int off = 0;
            int auto = 0;

            bool hasSmallerDeploy = false;
            float highestDeploy = 0;

            foreach(IMyParachute parachute in groupData.Blocks)
            {
                if(!parachute.IsFunctional)
                    broken++;

                if(!parachute.Enabled)
                    off++;

                if(parachute.AutoDeploy)
                {
                    auto++;

                    float deployHeight = parachute.AutoDeployHeight;
                    if(deployHeight > highestDeploy)
                    {
                        if(highestDeploy != 0)
                            hasSmallerDeploy = true;

                        highestDeploy = deployHeight;
                    }
                }
            }

            if(auto > 0)
                Processor.AppendGroupStats(sb, broken, off);

            int total = groupData.Blocks.Count;

            if(auto == 0)
            {
                sb.Append("Manual\n");
            }
            else if(auto == total)
            {
                sb.Append("Auto\n");
            }
            else
            {
                sb.Append("(Mixed)\n");
            }

            if(hasSmallerDeploy)
                sb.Append('<');

            sb.DistanceFormat(highestDeploy, 1);

            return true;
        }
    }
}
