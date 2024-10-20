using System;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class Motor : SpecializedOverlayBase
    {
        static Vector4 LimitPieFace = Color.Cyan * SolidOverlayAlpha;
        static Vector4 LimitPieLine = Color.Lime * LaserOverlayAlpha;
        static Vector4 LimitRolloverFace = Color.Yellow * SolidOverlayAlpha;
        static Vector4 LimitRolloverLine = Color.Lime * LaserOverlayAlpha;
        static Vector4 LimitOneWayLine = Color.Yellow * LaserOverlayAlpha;
        static Vector4 NoLimitsFaceColor = Color.Lime * SolidOverlayAlpha;
        static Vector4 NoLimitsLineColor = Color.Lime * LaserOverlayAlpha;

        const int LimitsLineEveryDegrees = RoundedQualityHigh;
        const float LimitsLineThick = 0.03f;

        static Color LabelRotationLimits = Color.Yellow;

        static Color CurrentAngleColor = Color.White;
        static Vector4 CurrentAngleLineColor = CurrentAngleColor * LaserOverlayAlpha;

        const float CurrentAngleLineThick = 0.05f;

        public Motor(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_MotorStator));
            Add(typeof(MyObjectBuilder_MotorAdvancedStator));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BData_Motor data = Main.LiveDataHandler.Get<BData_Motor>(def, drawInstance.BDataCache);
            if(data == null || data.StatorDef == null || data.TopDef == null)
                return;

            MyMotorStatorDefinition statorDef = data.StatorDef;

            float radius = data.TopDef.Size.AbsMax() * MyDefinitionManager.Static.GetCubeSize(data.TopDef.CubeSize); // twice the top's largest axis length

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            int minAngle = (statorDef.MinAngleDeg.HasValue ? (int)statorDef.MinAngleDeg.Value : -180);
            int maxAngle = (statorDef.MaxAngleDeg.HasValue ? (int)statorDef.MaxAngleDeg.Value : 180);

            IMyMotorStator stator = block?.FatBlock as IMyMotorStator;
            if(stator != null)
            {
                float min = stator.LowerLimitDeg;
                float max = stator.UpperLimitDeg;

                minAngle = (min >= -360 ? (int)min : -361);
                maxAngle = (max <= 360 ? (int)max : 361);
            }

            MatrixD circleMatrix = blockWorldMatrix;

            Vector3D topDirWorld = circleMatrix.Left;

            // correct orientation by top part's expected usage direction (calculated from biggest mountpoint)
            if(data.TopDir.HasValue)
            {
                topDirWorld = Vector3D.TransformNormal((Vector3D)data.TopDir.Value, circleMatrix);

                // the turret axis draw is 90deg off it seems
                Vector3D right = Vector3D.Cross(topDirWorld, blockWorldMatrix.Up);
                if(Vector3.IsZero(right)) // in case it already points up
                    right = Vector3D.Cross(topDirWorld, blockWorldMatrix.Forward);

                circleMatrix = MatrixD.CreateWorld(circleMatrix.Translation, -right, blockWorldMatrix.Up);
            }

            bool canDrawLabel = drawInstance.LabelRender.CanDrawLabel();

            Vector3D firstOuterRimVec, lastOuterRimVec;

            if(minAngle < -360 && maxAngle > 360) // both limits are unlimited
            {
                OverlayDrawInstance.DrawAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                    ref circleMatrix, radius, -180, 180, LimitsLineEveryDegrees,
                    NoLimitsFaceColor, NoLimitsLineColor, MaterialSquare, MaterialLaser, LimitsLineThick, BlendType);
            }
            else if(minAngle < -360 || maxAngle > 360) // at least one of the limits is unlimited
            {
                const int HookAngleSteps = 6;
                const float HookAngleStep = 5;

                float angleLimit;
                float angleDir;
                if(minAngle >= -360)
                {
                    angleLimit = minAngle;
                    angleDir = HookAngleStep;
                }
                else
                {
                    angleLimit = maxAngle;
                    angleDir = -HookAngleStep;
                }

                // line from center
                float angleRad = MathHelper.ToRadians(angleLimit);
                Vector3D localPos = new Vector3D(radius * Math.Cos(angleRad), 0, radius * Math.Sin(angleRad));
                Vector3D worldPos = Vector3D.Transform(localPos, circleMatrix);

                MyTransparentGeometry.AddLineBillboard(MaterialLaser, LimitOneWayLine, circleMatrix.Translation, (Vector3)(worldPos - circleMatrix.Translation), 1f, LimitsLineThick, BlendType);

                // lines along the rim
                Vector3D prevPos = worldPos;

                for(int i = 1; i <= HookAngleSteps; i++)
                {
                    angleRad = MathHelper.ToRadians(angleLimit + angleDir * i);
                    localPos = new Vector3D(radius * Math.Cos(angleRad), 0, radius * Math.Sin(angleRad));
                    Vector3D pos = Vector3D.Transform(localPos, circleMatrix);

                    MyTransparentGeometry.AddLineBillboard(MaterialLaser, LimitOneWayLine, pos, (Vector3)(prevPos - pos), 1f, LimitsLineThick, BlendType);

                    prevPos = pos;
                }
            }
            else
            {
                // doesn't seem to allow it to have flipped limits, but leaving this here anyway
                if(maxAngle < minAngle)
                {
                    OverlayDrawInstance.DrawAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                        ref circleMatrix, radius, maxAngle, minAngle, LimitsLineEveryDegrees,
                        Color.Red * SolidOverlayAlpha, Color.Red * LaserOverlayAlpha, MaterialSquare, MaterialLaser, LimitsLineThick, BlendType);
                }
                else
                {
                    // if the limits allow more than a full 360deg rotation
                    if((maxAngle - minAngle) > 360)
                    {
                        OverlayDrawInstance.DrawAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                            ref circleMatrix, radius, minAngle, maxAngle, LimitsLineEveryDegrees,
                            LimitRolloverFace, LimitRolloverLine, MaterialSquare, MaterialLaser, LimitsLineThick, BlendType);
                    }
                    else
                    {
                        OverlayDrawInstance.DrawAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                            ref circleMatrix, radius, minAngle, maxAngle, LimitsLineEveryDegrees,
                            LimitPieFace, LimitPieLine, MaterialSquare, MaterialLaser, LimitsLineThick, BlendType);
                    }
                }
            }

            // only draw limits label if not in-hand
            if(block == null && canDrawLabel)
            {
                Vector3D labelPos = circleMatrix.Translation + circleMatrix.Forward * radius;
                Vector3D labelDir = Vector3D.Normalize(labelPos - circleMatrix.Translation);
                drawInstance.LabelRender.DrawLineLabel(LabelType.RotationLimit, labelPos, labelDir, LabelRotationLimits, "Rotation limit");
            }

            // current angle line + label
            if(stator != null)
            {
                float angleRad = stator.Angle;
                Vector3D localPos = new Vector3D(radius * Math.Cos(angleRad), 0, radius * Math.Sin(angleRad));
                Vector3D worldPos = Vector3D.Transform(localPos, circleMatrix);

                MyTransparentGeometry.AddLineBillboard(MaterialLaser, CurrentAngleLineColor, circleMatrix.Translation, (Vector3)(worldPos - circleMatrix.Translation), 1f, CurrentAngleLineThick, BlendType);

                if(canDrawLabel)
                {
                    Vector3D labelPos = circleMatrix.Translation; // + (worldPos - circleMatrix.Translation);
                    Vector3D labelDir = Vector3D.Forward; // Vector3D.Normalize(worldPos - circleMatrix.Translation); // topDirWorld;
                    const float LineHeight = 0f; // 0.5f

                    drawInstance.LabelRender.DynamicLabel.Clear().Append("Current Angle: ").AngleFormat(stator.Angle);
                    drawInstance.LabelRender.DrawLineLabel(LabelType.DynamicLabel, labelPos, labelDir, CurrentAngleColor, alwaysOnTop: true, lineHeight: LineHeight);
                }
            }
        }
    }
}
