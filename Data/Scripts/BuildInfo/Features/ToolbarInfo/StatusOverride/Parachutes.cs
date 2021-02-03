using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using DoorStatus = Sandbox.ModAPI.Ingame.DoorStatus;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Parachutes : StatusOverrideBase
    {
        public Parachutes(ToolbarStatusProcessor processor) : base(processor)
        {
            var type = typeof(MyObjectBuilder_Parachute);

            processor.AddStatus(type, Deploy, "Open", "Open_On", "Open_Off");
            processor.AddStatus(type, AutoDeploy, "AutoDeploy"); // vanilla status is borked

            processor.AddGroupStatus(type, GroupDeploy, "Open", "Open_On", "Open_Off");
            processor.AddGroupStatus(type, GroupAutoDeploy, "AutoDeploy");
        }

        bool Deploy(StringBuilder sb, ToolbarItem item)
        {
            var parachute = (IMyParachute)item.Block;
            bool hasAmmo = true;

            var inv = parachute.GetInventory();
            if(inv != null)
            {
                var def = (MyParachuteDefinition)item.Block.SlimBlock.BlockDefinition;
                var foundItems = (float)inv.GetItemAmount(def.MaterialDefinitionId);
                hasAmmo = (foundItems >= def.MaterialDeployCost);
            }

            Processor.AppendSingleStats(sb, item.Block);

            if(!Processor.AnimFlip && !hasAmmo && parachute.Status != DoorStatus.Open)
                sb.Append("Empty!\n");

            switch(parachute.Status)
            {
                case DoorStatus.Opening: sb.Append("Deploying..."); break;
                case DoorStatus.Closing: sb.Append("Closing..."); break;
                case DoorStatus.Open: sb.Append("Deployed"); break;
                case DoorStatus.Closed: sb.Append("Ready"); break;
                default: return false;
            }

            return true;
        }

        bool AutoDeploy(StringBuilder sb, ToolbarItem item)
        {
            var parachute = (IMyParachute)item.Block;
            bool autoDeploy = parachute.GetValue<bool>("AutoDeploy"); // HACK: no interface members for this

            if(autoDeploy)
            {
                if(!Processor.AppendSingleStats(sb, item.Block))
                    sb.Append("Auto\n");

                float deployHeight = parachute.GetValue<float>("AutoDeployHeight");
                sb.DistanceFormat(deployHeight, 1);
            }
            else
            {
                sb.Append("Manual");
            }
            return true;
        }

        bool GroupDeploy(StringBuilder sb, ToolbarItem item, GroupData groupData)
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
                    var inv = parachute.GetInventory();
                    if(inv != null)
                    {
                        // HACK: block cast needed because modAPI IMyParachute implements ingame interfaces instead of modAPI ones.
                        var def = (MyParachuteDefinition)((IMyTerminalBlock)parachute).SlimBlock.BlockDefinition;
                        var foundItems = (float)inv.GetItemAmount(def.MaterialDefinitionId);
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
                sb.Append("All open");
            }
            else if(open == 0)
            {
                sb.Append("All ready");
            }
            else
            {
                sb.NumberCapped(open).Append(" open\n");
                sb.NumberCapped(ready).Append(" rdy");
            }

            return true;
        }

        bool GroupAutoDeploy(StringBuilder sb, ToolbarItem item, GroupData groupData)
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

                bool autoDeploy = parachute.GetValue<bool>("AutoDeploy");
                if(autoDeploy)
                {
                    auto++;

                    float deployHeight = parachute.GetValue<float>("AutoDeployHeight");
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
