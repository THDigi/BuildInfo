using System;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Rotors : StatusOverrideBase
    {
        public Rotors(ToolbarStatusProcessor processor) : base(processor)
        {
            RegisterFor(typeof(MyObjectBuilder_MotorStator));
            RegisterFor(typeof(MyObjectBuilder_MotorAdvancedStator));

            processor.AddStatus(typeof(MyObjectBuilder_MotorSuspension), Attached, "Add Top Part");

            processor.AddGroupStatus(typeof(MyObjectBuilder_MotorSuspension), GroupAttached, "Add Top Part");
        }

        void RegisterFor(MyObjectBuilderType type)
        {
            Processor.AddStatus(type, Attached, "Attach", "Detach", "Add Top Part", "Add Small Top Part");
            Processor.AddStatus(type, Reverse, "Reverse");

            Processor.AddGroupStatus(type, GroupAttached, "Attach", "Detach", "Add Top Part", "Add Small Top Part");
            Processor.AddGroupStatus(type, GroupReverse, "Reverse");
        }

        bool Attached(StringBuilder sb, ToolbarItem item)
        {
            var stator = (IMyMotorStator)item.Block;
            sb.Append(stator.IsAttached ? "Atached" : "Detached");
            return true;
        }

        bool Reverse(StringBuilder sb, ToolbarItem item)
        {
            var stator = (IMyMotorStator)item.Block;
            float angleRad = stator.Angle;

            if(Processor.AnimFlip && !stator.IsWorking)
                sb.Append("OFF!\n");
            else if(!Processor.AnimFlip && stator.TargetVelocityRPM == 0)
                sb.Append("No Vel\n");

            float minRad = stator.LowerLimitRad;
            float maxRad = stator.UpperLimitRad;

            // is rotor limited in both directions
            if(minRad >= -MathHelper.TwoPi && maxRad <= MathHelper.TwoPi)
            {
                float progress = (angleRad - minRad) / (maxRad - minRad);
                sb.ProportionToPercent(progress);
            }
            else
            {
                sb.AngleFormat(angleRad, 0);
            }
            return true;
        }

        bool GroupAttached(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyMotorStator>())
                return false;

            int attached = 0;

            foreach(IMyMotorStator stator in groupData.Blocks)
            {
                if(stator.IsAttached)
                    attached++;
            }

            int total = groupData.Blocks.Count;
            int detached = (total - attached);

            if(attached == total)
            {
                sb.Append("All\nAttached");
            }
            else if(detached == total)
            {
                sb.Append("All\nDetached");
            }
            else
            {
                sb.Append("Detached:\n").Append(detached);
            }

            return true;
        }

        bool GroupReverse(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyMotorStator>())
                return false;

            bool allLimited = true;
            bool allOn = true;
            bool allCanMove = true;
            float travelAverage = 0;
            float angleMin = float.MaxValue;
            float angleMax = float.MinValue;

            foreach(IMyMotorStator stator in groupData.Blocks)
            {
                if(allOn && !stator.IsWorking)
                    allOn = false;

                if(allCanMove && stator.TargetVelocityRPM == 0)
                    allCanMove = false;

                float angle = stator.Angle;

                angleMin = Math.Min(angleMin, angle);
                angleMax = Math.Max(angleMax, angle);

                if(allLimited)
                {
                    float minRad = stator.LowerLimitRad;
                    float maxRad = stator.UpperLimitRad;

                    // is rotor limited in both directions
                    if(minRad >= -MathHelper.TwoPi && maxRad <= MathHelper.TwoPi)
                        travelAverage += (angle - minRad) / (maxRad - minRad);
                    else
                        allLimited = false;
                }
            }

            int total = groupData.Blocks.Count;

            if(Processor.AnimFlip && !allOn)
                sb.Append("OFF!\n");

            if(!Processor.AnimFlip && !allCanMove)
                sb.Append("No Vel\n");

            if(allLimited)
            {
                if(travelAverage > 0)
                    travelAverage /= total;

                sb.ProportionToPercent(travelAverage);
            }
            else
            {
                if(Math.Abs(angleMin - angleMax) <= 0.1f)
                {
                    sb.AngleFormat(angleMin, 0);
                }
                else
                {
                    sb.AngleFormat(angleMin, 0).Append("~").AngleFormat(angleMax);
                }
            }

            return true;
        }
    }
}
