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
            string[] attachIds = new string[]
            {
                "Attach", "Detach",
                "Add Top Part", "Add Small Top Part",
                "AddRotorTopPart", "AddSmallRotorTopPart",
                "AddHingeTopPart", "AddSmallHingeTopPart",
            };

            Processor.AddStatus(type, Attached, attachIds);
            Processor.AddStatus(type, Reverse, "Reverse");

            Processor.AddGroupStatus(type, GroupAttached, attachIds);
            Processor.AddGroupStatus(type, GroupReverse, "Reverse");
        }

        bool Attached(StringBuilder sb, ToolbarItem item)
        {
            IMyMotorBase stator = (IMyMotorBase)item.Block;
            sb.Append(stator.IsAttached ? "Atached" : "Detached");
            return true;
        }

        bool Reverse(StringBuilder sb, ToolbarItem item)
        {
            IMyMotorStator stator = (IMyMotorStator)item.Block;
            float angleRad = stator.Angle;

            if(!Processor.AppendSingleStats(sb, item.Block) && stator.TargetVelocityRPM == 0)
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

        bool GroupAttached(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyMotorBase>())
                return false;

            int attached = 0;

            foreach(IMyMotorBase stator in groupData.Blocks)
            {
                if(stator.IsAttached)
                    attached++;
            }

            int total = groupData.Blocks.Count;

            if(attached == total)
            {
                sb.Append("Attached");
            }
            else if(attached == 0)
            {
                sb.Append("Detached");
            }
            else
            {
                sb.NumberCapped(attached).Append(" att\n");
                sb.NumberCapped(total - attached).Append(" det");
            }

            return true;
        }

        bool GroupReverse(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyMotorStator>())
                return false;

            int broken = 0;
            int off = 0;
            bool allLimited = true;
            bool allCanMove = true;
            float travelAverage = 0;
            float angleMin = float.MaxValue;
            float angleMax = float.MinValue;

            foreach(IMyMotorStator stator in groupData.Blocks)
            {
                if(!stator.IsFunctional)
                    broken++;

                if(!stator.Enabled)
                    off++;

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

            bool shownWarnings = Processor.AppendGroupStats(sb, broken, off);

            if(!shownWarnings && !allCanMove)
                sb.Append("No Vel\n");

            int total = groupData.Blocks.Count;

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
