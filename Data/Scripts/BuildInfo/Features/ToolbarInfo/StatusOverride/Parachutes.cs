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

            if(Processor.AnimFlip && !parachute.IsWorking)
                sb.Append("OFF!\n");

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
                if(Processor.AnimFlip && !parachute.IsWorking)
                    sb.Append("OFF!\n");
                else
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

            int open = 0;
            int ready = 0;
            bool allAmmo = true;
            bool allOn = true;

            foreach(IMyParachute parachute in groupData.Blocks)
            {
                if(parachute.Status != DoorStatus.Open)
                {
                    var inv = parachute.GetInventory();
                    if(inv != null)
                    {
                        // HACK block cast needed because modAPI IMyParachute implements ingame interfaces instead of modAPI ones.
                        var def = (MyParachuteDefinition)((IMyTerminalBlock)parachute).SlimBlock.BlockDefinition;
                        var foundItems = (float)inv.GetItemAmount(def.MaterialDefinitionId);
                        if(foundItems < def.MaterialDeployCost)
                            allAmmo = false;
                    }
                }

                if(!parachute.IsWorking)
                    allOn = false;

                switch(parachute.Status)
                {
                    case DoorStatus.Open: open++; break;
                    case DoorStatus.Closed: ready++; break;
                }
            }

            if(Processor.AnimFlip && !allOn)
                sb.Append("OFF!\n");

            if(!Processor.AnimFlip && !allAmmo)
                sb.Append("Empty!\n");

            if(open > 0 && ready > 0)
                sb.Append("Mixed");
            else if(open > 0)
                sb.Append("Deployed");
            else
                sb.Append("Ready");

            return true;
        }

        bool GroupAutoDeploy(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyParachute>())
                return false;

            bool allOn = true;
            int auto = 0;
            int manual = 0;

            bool hasSmallerDeploy = false;
            float highestDeploy = 0;

            foreach(IMyParachute parachute in groupData.Blocks)
            {
                if(!parachute.IsWorking)
                    allOn = false;

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
                else
                {
                    manual++;
                }
            }

            if(auto > 0)
            {
                if(Processor.AnimFlip && !allOn)
                    sb.Append("OFF!\n");
                else if(manual > 0)
                    sb.Append("Mixed\n");
                else
                    sb.Append("Auto\n");

                if(hasSmallerDeploy)
                    sb.Append('<');

                sb.DistanceFormat(highestDeploy, 1);
            }
            else if(manual > 0)
            {
                sb.Append("Manual");
            }

            return true;
        }
    }
}
