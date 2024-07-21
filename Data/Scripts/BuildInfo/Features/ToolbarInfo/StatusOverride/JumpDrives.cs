using System;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRageMath;
using MyJumpDriveStatus = Sandbox.ModAPI.Ingame.MyJumpDriveStatus;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class JumpDrives : StatusOverrideBase
    {
        public JumpDrives(ToolbarStatusProcessor processor) : base(processor)
        {
            Type type = typeof(MyObjectBuilder_JumpDrive);

            processor.AddStatus(type, Jump, "Jump");
            processor.AddStatus(type, Recharge, "Recharge", "Recharge_On", "Recharge_Off");
            processor.AddStatus(type, JumpDistance, "IncreaseJumpDistance", "DecreaseJumpDistance");

            processor.AddGroupStatus(type, GroupRecharge, "Recharge", "Recharge_On", "Recharge_Off");
        }

        bool Jump(StringBuilder sb, ToolbarItem item)
        {
            IMyJumpDrive jd = (IMyJumpDrive)item.Block;

            float countdown = BuildInfoMod.Instance.JumpDriveMonitor.GetJumpCountdown(jd.CubeGrid.EntityId);
            if(countdown > 0)
            {
                sb.Append("Jumping\n");
                sb.TimeFormat(countdown);
                return true;
            }

            Processor.AppendSingleStats(sb, item.Block);

            switch(jd.Status)
            {
                case MyJumpDriveStatus.Charging:
                {
                    if(jd.Recharge)
                        AppendPercentage(sb, jd);
                    else
                        sb.Append("Stop");
                    break;
                }
                case MyJumpDriveStatus.Ready:
                {
                    do
                    {
                        // checks from MyGridJumpDriveSystem.RequestJump(string, Vector3D, long)
                        float artificialMultiplier;
                        Vector3 naturalGravity = MyAPIGateway.Physics.CalculateNaturalGravityAt(jd.CubeGrid.WorldMatrix.Translation, out artificialMultiplier);

                        if(!Vector3.IsZero(naturalGravity))
                        {
                            sb.Append("Grav!");
                            break;
                        }

                        // TODO: enable when we can get destination
                        //Vector3 naturalGravityAtDestination = MyAPIGateway.Physics.CalculateNaturalGravityAt(destinationVector, out artificialMultiplier);
                        //if(!Vector3.IsZero(naturalGravity))
                        //{
                        //    sb.Append("Grav!");
                        //    break;
                        //}
                        //if(MySession.Static.Settings.WorldSizeKm > 0 && destination.Length() > (double)(MySession.Static.Settings.WorldSizeKm * 500))
                        //{
                        //    sb.Append("TooFar!");
                        //    break;
                        //}

                        //double min = jd.CubeGrid.JumpSystem.GetMinJumpDistance(jd.OwnerId);
                        //double max = jd.CubeGrid.JumpSystem.GetMaxJumpDistance(jd.OwnerId);
                        //if(max < min)
                        //{
                        //    sb.Append("Short!");
                        //    break;
                        //}

                        // TODO: enable when there's a way to access all that stuff
                        //if(!IsJumpValid(userId, out MyJumpFailReason reason))
                        //{
                        //    sb.Append("Grav!");
                        //    break;
                        //}

                        sb.Append("Ready");
                    }
                    while(false);
                    break;
                }
                case MyJumpDriveStatus.Jumping: sb.Append("Jump..."); break;
                default: return false;
            }

            return true;
        }

        bool Recharge(StringBuilder sb, ToolbarItem item)
        {
            IMyJumpDrive jd = (IMyJumpDrive)item.Block;

            if(!Processor.AppendSingleStats(sb, jd))
            {
                AppendPercentage(sb, jd);
                sb.Append('\n');
            }

            sb.Append("Charge:").Append(jd.Recharge ? IconPowerOn : IconPowerOff);
            return true;
        }

        void AppendPercentage(StringBuilder sb, IMyJumpDrive jd)
        {
            int filledPercent = (int)((jd.CurrentStoredPower / jd.MaxStoredPower) * 100);
            sb.Append(filledPercent).Append("%");

            MyResourceSinkComponent sink = jd.Components.Get<MyResourceSinkComponent>();
            if(sink != null)
            {
                const float RatioOfMaxForDoubleArrows = 0.9f;

                float input = sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
                float maxInput = sink.MaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId);
                bool highFlow = (input > (maxInput * RatioOfMaxForDoubleArrows));

                if(input > 0)
                    sb.Append(highFlow ? "++" : "+");
                //else
                //    sb.Append("");
            }
        }

        bool JumpDistance(StringBuilder sb, ToolbarItem item)
        {
            IMyJumpDrive jd = (IMyJumpDrive)item.Block;

            // warn that the distance adjustment is pointless as jumpdrive is targeting a GPS.
            if(Processor.AnimFlip)
            {
                // HACK: JumpDistance slider is disabled if a GPS target is selected.
                IMyTerminalControlSlider prop = jd.GetProperty("JumpDistance") as IMyTerminalControlSlider;
                if(prop?.Enabled != null && !prop.Enabled.Invoke(jd))
                {
                    sb.Append("GPS\n");
                }
            }

            const float LightYear = 9.460730473E+15f;
            const float LightSecond = 299792458.00013667f;

            float distance = jd.JumpDistanceMeters;

            if(distance > LightYear)
                sb.NumberCapped((int)(distance / LightYear), MaxChars - 2).Append("ly");
            else if(distance > LightSecond)
                sb.NumberCapped((int)(distance / LightSecond), MaxChars - 2).Append("ls");
            else if(distance > 999)
                sb.NumberCapped((int)(distance / 1000), MaxChars - 2).Append("km");
            else
                sb.NumberCapped((int)distance, MaxChars - 1).Append("m");

            return true;
        }

        bool GroupRecharge(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyJumpDrive>())
                return false;

            int broken = 0;
            int off = 0;
            float averageFilled = 0f;
            float input = 0f;
            float maxInput = 0f;
            int ready = 0;
            int charge = 0;

            foreach(IMyJumpDrive jd in groupData.Blocks)
            {
                if(!jd.IsFunctional)
                    broken++;

                if(!jd.Enabled)
                    off++;

                averageFilled += (jd.CurrentStoredPower / jd.MaxStoredPower);

                MyResourceSinkComponent sink = jd.Components.Get<MyResourceSinkComponent>();
                if(sink != null)
                {
                    input += sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
                    maxInput += sink.MaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId);
                }

                switch(jd.Status)
                {
                    case MyJumpDriveStatus.Ready: ready++; break;
                    case MyJumpDriveStatus.Charging: charge++; break;
                }
            }

            int total = groupData.Blocks.Count;

            Processor.AppendGroupStats(sb, broken, off);

            if(averageFilled > 0)
            {
                averageFilled /= total;
                averageFilled *= 100;
            }

            //float averageInput = 0;
            //if(input > 0)
            //    averageInput = input / total;

            if(ready == total)
                sb.Append("Ready\n");
            else if(charge == total)
                sb.Append("Charge\n");
            else
                sb.Append("(Mixed)\n");

            sb.Append(MathHelper.Clamp((int)averageFilled, 0, 100)).Append("%");

            const float RatioOfMaxForDoubleArrows = 0.9f;
            bool highFlow = (input > (maxInput * RatioOfMaxForDoubleArrows));

            if(input > 0)
                sb.Append(highFlow ? "++" : "+");
            //else
            //    sb.Append("  ");

            return true;
        }
    }
}
