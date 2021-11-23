using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class LaserAntenna : SpecializedOverlayBase
    {
        Vector4 ColorPitch = (Color.Red * SolidOverlayAlpha).ToVector4();
        Vector4 ColorPitchLine = (Color.Red * LaserOverlayAlpha).ToVector4();
        Vector4 ColorYaw = (Color.Lime * SolidOverlayAlpha).ToVector4();
        Vector4 ColorYawLine = (Color.Lime * LaserOverlayAlpha).ToVector4();
        const int LimitsLineEveryDegrees = RoundedQualityHigh;
        const float LimitsLineThick = 0.03f;

        Vector4 ColorLaser = (new Color(255, 155, 0) * 1f).ToVector4();
        const float LaserThick = 0.02f;
        const float LaserLength = 15f;

        public LaserAntenna(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_LaserAntenna));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            MyLaserAntennaDefinition antennaDef = def as MyLaserAntennaDefinition;
            if(antennaDef == null)
                return;

            BData_LaserAntenna data = Main.LiveDataHandler.Get<BData_LaserAntenna>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            bool canDrawLabel = drawInstance.LabelRender.CanDrawLabel();

            float radius = (def.Size * drawInstance.CellSizeHalf).AbsMin() + 1f;

            int minPitch = Math.Max(antennaDef.MinElevationDegrees, -90);
            int maxPitch = Math.Min(antennaDef.MaxElevationDegrees, 90);

            int minYaw = antennaDef.MinAzimuthDegrees;
            int maxYaw = antennaDef.MaxAzimuthDegrees;

            {
                MatrixD pitchMatrix = drawMatrix;
                if(block?.FatBlock != null)
                {
                    MyCubeBlock internalBlock = (MyCubeBlock)block.FatBlock;

                    // from MyLaserAntenna.OnModelChange()
                    MyEntitySubpart subpartYaw = internalBlock.Subparts?.GetValueOrDefault("LaserComTurret", null);
                    MyEntitySubpart subpartPitch = subpartYaw?.Subparts?.GetValueOrDefault("LaserCom", null);

                    if(subpartPitch != null)
                    {
                        // NOTE: grid matrix because subpart is parented to grid, see MyLaserAntenna.SetParent()
                        pitchMatrix = subpartPitch.PositionComp.LocalMatrixRef * internalBlock.CubeGrid.WorldMatrix;
                        pitchMatrix.Translation = drawMatrix.Translation;
                    }
                }

                // only yaw rotation
                MatrixD m = MatrixD.CreateWorld(drawMatrix.Translation, Vector3D.Cross(pitchMatrix.Left, drawMatrix.Up), drawMatrix.Up);
                Vector3D rotationPivot = Vector3D.Transform(data.Turret.PitchLocalPos, m);

                // laser visualization
                MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorLaser, rotationPivot, (Vector3)pitchMatrix.Forward, LaserLength, LaserThick, BlendType);

                if(canDrawLabel)
                {
                    Vector3D labelPos = rotationPivot + pitchMatrix.Forward * (LaserLength / 2);
                    drawInstance.LabelRender.DrawLineLabel(LabelType.Laser, labelPos, pitchMatrix.Up, ColorLaser, "Laser");
                }

                // only yaw rotation but for cylinder
                pitchMatrix = MatrixD.CreateWorld(rotationPivot, drawMatrix.Down, pitchMatrix.Left);

                Vector3D firstOuterRimVec, lastOuterRimVec;
                drawInstance.DrawTurretAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                    ref pitchMatrix, radius, minPitch, maxPitch, LimitsLineEveryDegrees,
                    ColorPitch, ColorPitchLine, MaterialSquare, MaterialLaser, LimitsLineThick, BlendType);

                if(canDrawLabel)
                {
                    Vector3D labelDir = Vector3D.Normalize(lastOuterRimVec - pitchMatrix.Translation);
                    drawInstance.LabelRender.DrawLineLabel(LabelType.PitchLimit, lastOuterRimVec, labelDir, ColorPitchLine, "Pitch limit");
                }
            }

            {
                Vector3D rotationPivot = Vector3D.Transform(data.Turret.YawLocalPos, drawMatrix);

                MatrixD yawMatrix = MatrixD.CreateWorld(rotationPivot, drawMatrix.Right, drawMatrix.Down);

                Vector3D firstOuterRimVec, lastOuterRimVec;
                drawInstance.DrawTurretAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                    ref yawMatrix, radius, minYaw, maxYaw, LimitsLineEveryDegrees,
                    ColorYaw, ColorYawLine, MaterialSquare, MaterialLaser, LimitsLineThick, BlendType);

                if(canDrawLabel)
                {
                    Vector3D labelDir = Vector3D.Normalize(firstOuterRimVec - yawMatrix.Translation);
                    drawInstance.LabelRender.DrawLineLabel(LabelType.YawLimit, firstOuterRimVec, labelDir, ColorYawLine, "Yaw limit");
                }
            }
        }
    }
}
