using System;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRageMath;
using ChargeMode = Sandbox.ModAPI.Ingame.ChargeMode;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Batteries : StatusOverrideBase
    {
        public Batteries(ToolbarStatusProcessor processor) : base(processor)
        {
            Type type = typeof(MyObjectBuilder_BatteryBlock);
            string[] actions = { "ChargeMode", "Recharge", "Recharge_On", "Recharge_Off", "Discharge", "Discharge_On", "Discharge_Off", "Auto" };

            processor.AddStatus(type, Charge, actions);
            processor.AddGroupStatus(type, GroupCharge, actions);
        }

        bool Charge(StringBuilder sb, ToolbarItem item)
        {
            IMyBatteryBlock battery = (IMyBatteryBlock)item.Block;

            Processor.AppendSingleStats(sb, item.Block);

            switch(battery.ChargeMode)
            {
                case ChargeMode.Auto: sb.Append("Auto"); break;
                case ChargeMode.Recharge: sb.Append("Charge"); break;
                case ChargeMode.Discharge: sb.Append("Drain"); break;
            }

            sb.Append("\n");

            int filledPercent = MathHelper.Clamp((int)((battery.CurrentStoredPower / battery.MaxStoredPower) * 100), 0, 100);

            sb.Append(filledPercent).Append("%");

            const float RatioOfMaxForDoubleArrows = 0.9f;

            float powerFlow = (battery.CurrentInput - battery.CurrentOutput);
            bool highFlow = false;
            if(powerFlow > 0)
                highFlow = (powerFlow > battery.MaxInput * RatioOfMaxForDoubleArrows);
            else if(powerFlow < 0)
                highFlow = (Math.Abs(powerFlow) > battery.MaxOutput * RatioOfMaxForDoubleArrows);

            if(powerFlow > 0)
                sb.Append(highFlow ? "++" : "+");
            else if(powerFlow < 0)
                sb.Append(highFlow ? "--" : "-");
            //else
            //    sb.Append("  ");

            return true;
        }

        bool GroupCharge(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyBatteryBlock>())
                return false;

            int broken = 0;
            int off = 0;
            float averageFilled = 0f;
            float totalFlow = 0f;
            float avgInput = 0f;
            float avgOutput = 0f;
            int auto = 0;
            int recharge = 0;
            int discharge = 0;

            foreach(IMyBatteryBlock battery in groupData.Blocks)
            {
                if(!battery.IsFunctional)
                    broken++;

                if(!battery.Enabled)
                    off++;

                averageFilled += (battery.CurrentStoredPower / battery.MaxStoredPower);
                totalFlow += (battery.CurrentInput - battery.CurrentOutput);

                avgInput += battery.MaxInput;
                avgOutput += battery.MaxOutput;

                switch(battery.ChargeMode)
                {
                    case ChargeMode.Auto: auto++; break;
                    case ChargeMode.Recharge: recharge++; break;
                    case ChargeMode.Discharge: discharge++; break;
                }
            }

            int total = groupData.Blocks.Count;

            Processor.AppendGroupStats(sb, broken, off);

            if(auto == total)
                sb.Append("Auto\n");
            else if(recharge == total)
                sb.Append("Charge\n");
            else if(discharge == total)
                sb.Append("Drain\n");
            else
                sb.Append("(Mixed)\n");

            if(averageFilled > 0)
            {
                averageFilled /= total;
                averageFilled *= 100;
            }

            sb.NumberCapped((int)averageFilled, 4).Append("%");

            const float RatioOfMaxForDoubleArrows = 0.9f;

            float averageFlow = 0f;
            if(totalFlow != 0) // can be negative too
                averageFlow = totalFlow / total;

            if(avgInput != 0)
                avgInput /= total;

            if(avgOutput != 0)
                avgOutput /= total;

            bool highFlow = false;
            if(averageFlow > 0)
                highFlow = (averageFlow > avgInput * RatioOfMaxForDoubleArrows);
            else if(averageFlow < 0)
                highFlow = (Math.Abs(averageFlow) > avgOutput * RatioOfMaxForDoubleArrows);

            if(averageFlow > 0)
                sb.Append(highFlow ? "++" : "+");
            else if(averageFlow < 0)
                sb.Append(highFlow ? "--" : "-");
            //else
            //    sb.Append("  ");

            return true;
        }
    }
}
