using System;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using ChargeMode = Sandbox.ModAPI.Ingame.ChargeMode;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Batteries : StatusOverrideBase
    {
        public Batteries(ToolbarStatusProcessor processor) : base(processor)
        {
            var type = typeof(MyObjectBuilder_BatteryBlock);

            processor.AddStatus(type, Charge, "ChargeMode", "Recharge", "Discharge", "Auto");

            processor.AddGroupStatus(type, GroupCharge, "ChargeMode", "Recharge", "Discharge", "Auto");
        }

        bool Charge(StringBuilder sb, ToolbarItem item)
        {
            var battery = (IMyBatteryBlock)item.Block;

            if(Processor.AnimFlip && !battery.IsWorking)
            {
                sb.Append("OFF!");
            }
            else
            {
                switch(battery.ChargeMode)
                {
                    case ChargeMode.Auto: sb.Append("Auto"); break;
                    case ChargeMode.Recharge: sb.Append("Charge"); break;
                    case ChargeMode.Discharge: sb.Append("Drain"); break;
                }
            }

            sb.Append("\n");

            int filledPercent = (int)((battery.CurrentStoredPower / battery.MaxStoredPower) * 100);
            sb.Append(filledPercent).Append("% ");

            const float RatioOfMaxForDoubleArrows = 0.9f;

            float powerFlow = (battery.CurrentInput - battery.CurrentOutput);
            bool highFlow = false;
            if(powerFlow > 0)
                highFlow = (powerFlow > battery.MaxInput * RatioOfMaxForDoubleArrows);
            else if(powerFlow < 0)
                highFlow = (Math.Abs(powerFlow) > battery.MaxOutput * RatioOfMaxForDoubleArrows);

            if(Processor.AnimFlip && powerFlow > 0)
                sb.Append(highFlow ? "++" : "+");
            else if(Processor.AnimFlip && powerFlow < 0)
                sb.Append(highFlow ? "--" : "-");

            return true;
        }

        bool GroupCharge(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyBatteryBlock>())
                return false;

            bool allOn = true;
            float averageFilled = 0f;
            float averageFlow = 0f;
            float totalFlow = 0f;
            float maxInput = 0f;
            float maxOutput = 0f;
            int auto = 0;
            int recharge = 0;
            int discharge = 0;

            foreach(IMyBatteryBlock battery in groupData.Blocks)
            {
                if(!battery.IsWorking)
                    allOn = false;

                averageFilled += (battery.CurrentStoredPower / battery.MaxStoredPower);
                totalFlow += (battery.CurrentInput - battery.CurrentOutput);

                maxInput += battery.MaxInput;
                maxOutput += battery.MaxOutput;

                switch(battery.ChargeMode)
                {
                    case ChargeMode.Auto: auto++; break;
                    case ChargeMode.Recharge: recharge++; break;
                    case ChargeMode.Discharge: discharge++; break;
                }
            }

            int total = groupData.Blocks.Count;

            if(averageFilled > 0)
            {
                averageFilled /= total;
                averageFilled *= 100;
            }

            if(totalFlow != 0) // can be negative too
                averageFlow = totalFlow / total;

            if(Processor.AnimFlip && !allOn)
            {
                sb.Append("OFF!\n");
            }
            else
            {
                if(auto == total)
                    sb.Append("Auto\n");
                else if(recharge == total)
                    sb.Append("Charge\n");
                else if(discharge == total)
                    sb.Append("Drain\n");
                else
                    sb.Append("Mixed\n");
            }

            sb.Append((int)averageFilled).Append("% ");

            const float RatioOfMaxForDoubleArrows = 0.9f;

            bool highFlow = false;
            if(averageFlow > 0)
                highFlow = (averageFlow > maxInput * RatioOfMaxForDoubleArrows);
            else if(averageFlow < 0)
                highFlow = (Math.Abs(averageFlow) > maxOutput * RatioOfMaxForDoubleArrows);

            if(Processor.AnimFlip && averageFlow > 0)
                sb.Append(highFlow ? "++" : "+");
            else if(Processor.AnimFlip && averageFlow < 0)
                sb.Append(highFlow ? "--" : "-");

            return true;
        }
    }
}
