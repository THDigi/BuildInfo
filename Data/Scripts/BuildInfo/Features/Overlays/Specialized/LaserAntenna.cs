using System;
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

            MatrixD pitchMatrix = drawMatrix;
            if(block?.FatBlock != null)
            {
                MyEntity subpartBase1;
                MyEntity subpartBase2;
                MyEntity barrelPart;
                if(!data.GetTurretParts((MyCubeBlock)block.FatBlock, out subpartBase1, out subpartBase2, out barrelPart))
                    return;

                // HACK: grid matrix because subpart is parented to grid, see MyLaserAntenna.SetParent()
                pitchMatrix = subpartBase2.PositionComp.LocalMatrixRef * block.CubeGrid.WorldMatrix;
                pitchMatrix.Translation = drawMatrix.Translation;
            }

            drawInstance.DrawTurretLimits(ref drawMatrix, ref pitchMatrix, data.TurretInfo, radius, minPitch, maxPitch, minYaw, maxYaw, canDrawLabel);

            // laser visualization
            {
                MatrixD m = MatrixD.CreateWorld(drawMatrix.Translation, Vector3D.Cross(pitchMatrix.Left, drawMatrix.Up), drawMatrix.Up);
                Vector3D rotationPivot = Vector3D.Transform(data.TurretInfo.PitchLocalPos, m);

                MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorLaser, rotationPivot, (Vector3)pitchMatrix.Forward, LaserLength, LaserThick, BlendType);

                if(canDrawLabel)
                {
                    Vector3D labelPos = rotationPivot + pitchMatrix.Forward * (LaserLength / 2);
                    drawInstance.LabelRender.DrawLineLabel(LabelType.Laser, labelPos, pitchMatrix.Up, ColorLaser, "Laser");
                }
            }
        }
    }
}
