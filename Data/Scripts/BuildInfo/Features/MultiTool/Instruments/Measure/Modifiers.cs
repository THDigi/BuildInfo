using System;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;
using static VRageRender.MyBillboard;

namespace Digi.BuildInfo.Features.MultiTool.Instruments.Measure
{
    class Modifiers
    {
        readonly Measure Measure;

        public Modifiers(Measure host)
        {
            Measure = host;
        }

        IMeasureVertex OtherVertex;
        Vector3D HintPos;
        internal string SnapDir;
        internal string SnapAngle;

        internal void Reset()
        {
            OtherVertex = null;
            SnapDir = null;
            SnapAngle = null;
        }

        internal void Apply(IMeasureVertex otherVertex)
        {
            try
            {
                SnapDir = null;
                SnapAngle = null;
                OtherVertex = otherVertex;

                MatrixD cm = MyAPIGateway.Session.Camera.WorldMatrix;
                HintPos = cm.Translation + cm.Forward * 0.3f + cm.Down * 0.1f;

                bool snapAxis = MyAPIGateway.Input.IsAnyShiftKeyPressed();
                bool snapAngle = MyAPIGateway.Input.IsAnyCtrlKeyPressed();
                bool snapLength = Measure.MultiTool.ControlAlignDefault.IsPressed(); // MyAPIGateway.Input.IsAnyAltKeyPressed();

                Vector3D thisPos = Measure.Aim.WorldPosition;
                Vector3D otherPos = otherVertex.GetWorldPosition();

                Vector3D dirScaled = thisPos - otherPos;
                Vector3D dirNormalized = dirScaled;
                double distance = dirNormalized.Normalize();

                Vector3D lockedDir = dirNormalized;

                Vector3D? rotatingAroundAxis = null;

                VertexAnchoredVertex otherVertexAnchor = otherVertex as VertexAnchoredVertex;
                VertexAnchoredLine otherLineAnchor = otherVertex as VertexAnchoredLine;
                MeasurementLine anchoredToline = otherVertexAnchor?.AnchoredVertex?.HostLine ?? otherLineAnchor?.AnchoredLine;

                // TODO what about multiple lines connected to that vertex... ugh =)
                MeasurementLine thirdLine = null;
                if(otherVertexAnchor != null)
                {
                    foreach(var m in Measure.Measurements)
                    {
                        if(m == anchoredToline)
                            continue;

                        var ml = m as MeasurementLine;
                        if(ml == null)
                            continue;

                        if(Vector3D.DistanceSquared(ml.A.GetWorldPosition(), otherPos) < 0.0001
                        || Vector3D.DistanceSquared(ml.B.GetWorldPosition(), otherPos) < 0.0001)
                        {
                            thirdLine = ml;
                            break;
                        }
                    }
                }

                const float LineLength = 0.1f;
                const float LineThick = 0.005f;
                const BlendTypeEnum Blend = BlendTypeEnum.PostPP;
                const float AxisRadius = 0.05f;

                const float DeselectedDarken = 0.5f;
                const float DeselectedDarkenRadial = 0.2f;
                const float SelectedDarken = 0f;
                const float LineAlpha = 1f;
                const float RadialAlpha = 0.25f;

                if(snapAxis)
                {
                    const double SnapNear = 0.9;

                    float naturalInterferrence;
                    Vector3 gravityDir = MyAPIGateway.Physics.CalculateNaturalGravityAt(otherPos, out naturalInterferrence)
                                    + MyAPIGateway.Physics.CalculateArtificialGravityAt(otherPos, naturalInterferrence);
                    float gravityAcceleration = gravityDir.LengthSquared() > 0 ? gravityDir.Normalize() : 0; // normalizes gravity too

                    Vector3D anchoredLineDirN = default(Vector3D);
                    if(anchoredToline != null)
                    {
                        anchoredLineDirN = anchoredToline.B.GetWorldPosition() - anchoredToline.A.GetWorldPosition();
                        anchoredLineDirN.Normalize();
                    }

                    Vector3? worldNormal = (otherVertex as VertexWorld)?.WorldNormal;

                    IMyEntity anchoredEntity = (otherVertex as VertexAnchoredEntity)?.Entity
                                            ?? (otherVertexAnchor?.AnchoredVertex as VertexAnchoredEntity)?.Entity;

                    // align to anchored line's directions
                    if(anchoredToline != null)
                    {
                        double dot = Vector3D.Dot(anchoredLineDirN, dirNormalized);
                        float darken = DeselectedDarken;

                        if(SnapDir == null && Math.Abs(dot) >= SnapNear)
                        {
                            lockedDir = anchoredLineDirN;

                            SnapDir = "anchored line's direction";
                            darken = SelectedDarken;
                        }

                        MyTransparentGeometry.AddLineBillboard(Constants.Mat_LaserGradient, Color.Darken(Color.Yellow * LineAlpha, darken), HintPos, anchoredLineDirN, LineLength, LineThick, blendType: Blend);
                    }

                    // align to 3rd line
                    if(thirdLine != null)
                    {
                        Vector3D thirdLineDir = Vector3D.Normalize(thirdLine.A.GetWorldPosition() - thirdLine.B.GetWorldPosition());

                        double dot = Vector3D.Dot(thirdLineDir, dirNormalized);
                        float darken = DeselectedDarken;

                        if(SnapDir == null && Math.Abs(dot) >= SnapNear)
                        {
                            lockedDir = thirdLineDir;

                            SnapDir = "anchored line's direction";
                            darken = SelectedDarken;
                        }

                        MyTransparentGeometry.AddLineBillboard(Constants.Mat_LaserGradient, Color.Darken(Color.Yellow * LineAlpha, darken), HintPos, thirdLineDir, LineLength, LineThick, blendType: Blend);
                    }

                    // align to normal direction
                    if(worldNormal.HasValue)
                    {
                        double dot = Vector3D.Dot(dirNormalized, worldNormal.Value);
                        float darken = DeselectedDarken;

                        if(SnapDir == null && Math.Abs(dot) > SnapNear)
                        {
                            lockedDir = worldNormal.Value;

                            SnapDir = "surface normal's direction";
                            darken = SelectedDarken;
                        }

                        MyTransparentGeometry.AddLineBillboard(Constants.Mat_LaserGradient, Color.Darken(new Color(100, 255, 200) * LineAlpha, darken), HintPos, worldNormal.Value, LineLength, LineThick, blendType: Blend);
                    }

                    // align to one of entity's axis
                    if(anchoredEntity != null)
                    {
                        MatrixD entityMatrix = anchoredEntity.WorldMatrix;

                        Base6Directions.Direction closestDir = entityMatrix.GetClosestDirection(dirNormalized);
                        Vector3D closestDirVec = entityMatrix.GetDirectionVector(closestDir);

                        double dot = Vector3D.Dot(closestDirVec, dirNormalized);
                        float darkenRight = DeselectedDarken;
                        float darkenUp = DeselectedDarken;
                        float darkenBack = DeselectedDarken;

                        if(SnapDir == null && Math.Abs(dot) >= SnapNear)
                        {
                            lockedDir = closestDirVec;
                            SnapDir = "entity's axis";

                            Base6Directions.Axis axis = Base6Directions.GetAxis(closestDir);
                            switch(axis)
                            {
                                case Base6Directions.Axis.LeftRight: darkenRight = SelectedDarken; break;
                                case Base6Directions.Axis.UpDown: darkenUp = SelectedDarken; break;
                                case Base6Directions.Axis.ForwardBackward: darkenBack = SelectedDarken; break;
                            }
                        }

                        MyTransparentGeometry.AddLineBillboard(Constants.Mat_LaserGradient, Color.Darken(Color.Red * LineAlpha, darkenRight), HintPos, entityMatrix.Right, LineLength, LineThick, blendType: Blend);
                        MyTransparentGeometry.AddLineBillboard(Constants.Mat_LaserGradient, Color.Darken(Color.Lime * LineAlpha, darkenUp), HintPos, entityMatrix.Up, LineLength, LineThick, blendType: Blend);
                        MyTransparentGeometry.AddLineBillboard(Constants.Mat_LaserGradient, Color.Darken(Color.Blue * LineAlpha, darkenBack), HintPos, entityMatrix.Backward, LineLength, LineThick, blendType: Blend);
                    }

                    // align to gravity direction
                    if(gravityAcceleration > 0)
                    {
                        double dot = Vector3D.Dot(dirNormalized, gravityDir);
                        float darken = DeselectedDarken;

                        if(SnapDir == null && Math.Abs(dot) >= SnapNear)
                        {
                            lockedDir = gravityDir;

                            SnapDir = "gravity direction";
                            darken = SelectedDarken;
                        }

                        MyTransparentGeometry.AddLineBillboard(Constants.Mat_LaserGradient, Color.Darken(Color.Orange * LineAlpha, darken), HintPos, gravityDir, LineLength, LineThick, blendType: Blend);
                    }

                    // rotate around anchored line's axis
                    if(anchoredToline != null)
                    {
                        Vector3D horizon = Vector3D.Cross(anchoredLineDirN, dirNormalized);
                        horizon = Vector3D.Cross(anchoredLineDirN, horizon);
                        horizon.Normalize();

                        double dot = Vector3D.Dot(horizon, dirNormalized);
                        float darken = DeselectedDarkenRadial;

                        if(SnapDir == null && dot <= -SnapNear)
                        {
                            lockedDir = horizon;
                            rotatingAroundAxis = anchoredLineDirN;

                            SnapDir = "anchored line's plane";
                            darken = SelectedDarken;
                        }

                        MatrixD matrix = MatrixD.CreateWorld(HintPos, horizon, dirNormalized);
                        Utils.DrawCircle(ref matrix, AxisRadius, Color.Darken(Color.Magenta * RadialAlpha, darken), 360 / 15,
                            Constants.Mat_GradientSRGB, Constants.Mat_Laser,
                            drawSpokes: false, flipSolidUV: true, lineThickness: LineThick, blendType: Blend);
                    }

                    // rotate around normal line
                    if(worldNormal.HasValue)
                    {
                        Vector3D axis = worldNormal.Value;

                        Vector3D horizon = Vector3D.Cross(axis, dirNormalized);
                        horizon = Vector3D.Cross(axis, horizon);
                        horizon.Normalize();

                        double dot = Vector3D.Dot(horizon, dirNormalized);
                        float darken = DeselectedDarkenRadial;

                        if(SnapDir == null && dot <= -SnapNear)
                        {
                            lockedDir = horizon;
                            rotatingAroundAxis = axis;

                            SnapDir = "surface normal's plane";
                            darken = SelectedDarken;
                        }

                        MatrixD matrix = MatrixD.CreateWorld(HintPos, horizon, -(Vector3D)axis);
                        Utils.DrawCircle(ref matrix, AxisRadius, Color.Darken(Color.Wheat * RadialAlpha, darken), 360 / 15,
                            Constants.Mat_GradientSRGB, Constants.Mat_Laser,
                            drawSpokes: false, flipSolidUV: true, lineThickness: LineThick, blendType: Blend);
                    }

                    // rotate around gravity axis
                    if(gravityAcceleration > 0)
                    {
                        Vector3D axis = gravityDir;

                        Vector3D horizon = Vector3D.Cross(axis, dirNormalized);
                        horizon = Vector3D.Cross(axis, horizon);
                        horizon.Normalize();

                        double dot = Vector3D.Dot(horizon, dirNormalized);
                        float darken = DeselectedDarkenRadial;

                        if(SnapDir == null && dot <= -SnapNear)
                        {
                            lockedDir = horizon;
                            rotatingAroundAxis = axis;

                            SnapDir = "gravity plane";
                            darken = SelectedDarken;
                        }

                        MatrixD matrix = MatrixD.CreateWorld(HintPos, horizon, -(Vector3D)axis);
                        Utils.DrawCircle(ref matrix, AxisRadius, Color.Darken(Color.Wheat * RadialAlpha, darken), 360 / 15,
                            Constants.Mat_GradientSRGB, Constants.Mat_Laser,
                            drawSpokes: false, flipSolidUV: true, lineThickness: LineThick, blendType: Blend);
                    }
                }

                double distanceModifier = Vector3D.Dot(dirScaled, lockedDir);

                if(snapLength)
                {
                    int cells = (int)(distanceModifier / Measure.RulerSizeMeters);
                    distanceModifier = cells * Measure.RulerSizeMeters;
                }

                if(snapAxis)
                {
                    Measure.Aim.WorldPosition = otherPos + lockedDir * distanceModifier;

                    // probably can't work without a second static reference
                    if(snapAngle && rotatingAroundAxis.HasValue && thirdLine != null)
                    {
                        Vector3D thirdLineDir = Vector3D.Normalize(thirdLine.A.GetWorldPosition() - thirdLine.B.GetWorldPosition());
                        Vector3D axis = rotatingAroundAxis.Value;
                        Vector3D dirA = thirdLineDir;
                        Vector3D dirB = lockedDir;

                        dirA.Normalize();
                        //dirB.Normalize();

                        double angleRad = Math.Acos(MathHelper.Clamp(dirA.Dot(dirB), -1.0, 1.0));

                        if(angleRad >= 0 && angleRad <= Math.PI)
                        {
                            double nearestAngle = Math.Round(angleRad / Measure.AngleSnapRad) * Measure.AngleSnapRad;

                            QuaternionD rotate = QuaternionD.CreateFromAxisAngle(axis, nearestAngle);

                            Measure.Aim.WorldPosition = otherPos + Vector3D.Normalize(Vector3D.Transform(dirA, rotate)) * distanceModifier;

                            SnapAngle = $"cross angle {MathHelper.ToDegrees(nearestAngle)}°";
                        }
                        else
                        {
                            Log.Error("Wrong angle?!? at snapAxis+snapAngle and has rotatingAroundAxis");
                        }
                    }
                }
                else if(snapLength && !Measure.HasTarget)
                {
                    Measure.Aim.WorldPosition = otherPos + dirNormalized * distanceModifier;
                }

                if(snapAngle && !snapAxis)
                {
                    //if(otherLineAnchor != null || otherVertexAnchor != null)
                    if(anchoredToline != null)
                    {
                        Vector3D dirA, dirB;

                        dirB = Measure.Aim.WorldPosition - otherPos;
                        dirA = anchoredToline.A.GetWorldPosition() - anchoredToline.B.GetWorldPosition();

                        /*
                        if(otherLineAnchor != null)
                        {
                            //Vector3D common = otherLineAnchor.GetWorldPosition();
                            dirA = anchoredToline.A.GetWorldPosition() - anchoredToline.B.GetWorldPosition();
                        }
                        else
                        {
                            //Vector3D common = otherVertexAnchor.AnchoredVertex.GetWorldPosition();
                            //Vector3D a = anchoredToline.GetOther(otherVertexAnchor.AnchoredVertex).GetWorldPosition();
                            //Vector3D b = Aim.WorldPosition;
                            //dirA = (a - common);
                            //dirB = (b - common);

                            dirA = otherVertexAnchor.HostLine.A.GetWorldPosition() - otherVertexAnchor.HostLine.B.GetWorldPosition();
                        }
                        */

                        double lenA = dirA.Normalize();
                        double lenB = dirB.Normalize();

                        double angleRad = Math.Acos(MathHelper.Clamp(dirA.Dot(dirB), -1.0, 1.0));

                        // FIXME: wrong direction when going from a single line

                        if(angleRad >= 0 && angleRad <= Math.PI)
                        {
                            Vector3D axis = Vector3D.Cross(dirA, dirB);

                            double nearestAngle = Math.Round(angleRad / Measure.AngleSnapRad) * Measure.AngleSnapRad;

                            QuaternionD rotate = QuaternionD.CreateFromAxisAngle(axis, nearestAngle);

                            Measure.Aim.WorldPosition = otherPos + Vector3D.Normalize(Vector3D.Transform(dirA, rotate)) * distanceModifier;

                            SnapAngle = $"angle {MathHelper.ToDegrees(nearestAngle)}°";
                        }
                        else
                        {
                            Log.Error("Wrong angle?!? at snapAngle only");
                        }
                    }
                    else
                    {
                        Measure.Notify(0, "Nothing relative to snap angle to", 100, FontsHandler.RedSh);
                    }
                }
            }
            finally
            {
                OtherVertex = null;
            }
        }
    }
}
