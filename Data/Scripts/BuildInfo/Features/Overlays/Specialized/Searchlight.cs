using System;
using Digi.BuildInfo.Features.LiveData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class Searchlight : SpecializedOverlayBase
    {
        Vector4 ColorLight = (new Color(255, 255, 200) * 0.5f).ToVector4();

        Color ColorCamera = new Color(55, 155, 255);

        public Searchlight(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_Searchlight));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            MySearchlightDefinition lightDef = def as MySearchlightDefinition;
            if(lightDef == null)
                return;

            BData_Searchlight data = Main.LiveDataHandler.Get<BData_Searchlight>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            bool isRealBlock = block?.FatBlock != null;
            bool canDrawLabel = drawInstance.LabelRender.CanDrawLabel();
            bool isCamController = (block?.FatBlock != null ? MyAPIGateway.Session.CameraController == block.FatBlock : false);

            MatrixD pitchMatrix = drawMatrix;
            if(isRealBlock)
            {
                MyEntity subpartBase1;
                MyEntity subpartBase2;
                MyEntity barrelPart;
                if(!data.GetTurretParts((MyCubeBlock)block.FatBlock, out subpartBase1, out subpartBase2, out barrelPart))
                    return;

                pitchMatrix = subpartBase2.PositionComp.WorldMatrixRef;
            }

            if(!isCamController)
            {
                float radius = (def.Size * drawInstance.CellSizeHalf).AbsMin() + 1f;

                int minPitch = Math.Max(lightDef.MinElevationDegrees, -90);
                int maxPitch = Math.Min(lightDef.MaxElevationDegrees, 90);

                int minYaw = lightDef.MinAzimuthDegrees;
                int maxYaw = lightDef.MaxAzimuthDegrees;

                drawInstance.DrawTurretLimits(ref drawMatrix, ref pitchMatrix, data.TurretInfo, radius, minPitch, maxPitch, minYaw, maxYaw, canDrawLabel);
            }

            #region Light
            if(!isCamController)
            {
                // from MySearchLight.UpdateAfterSimulationParallel()
                MatrixD world;
                Matrix? local = (isRealBlock ? data.Light.RelativeSubpart : data.Light.RelativePreview);
                if(local != null)
                    world = local.Value * pitchMatrix;
                else
                    world = pitchMatrix;

                MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorLight, world.Translation, (Vector3)world.Forward, 3, data.LightRadius, BlendType);
            }
            #endregion

            #region Camera
            if(!isCamController)
            {
                MatrixD view;
                Matrix? local = (isRealBlock ? data.Camera.RelativeSubpart : data.Camera.RelativePreview);
                if(local != null)
                {
                    view = local.Value * pitchMatrix;
                }
                else
                {
                    // MySearchLight.GetViewMatrix()
                    view = pitchMatrix;
                    view.Translation += view.Forward * lightDef.ForwardCameraOffset;
                    view.Translation += view.Up * lightDef.UpCameraOffset;
                }

                MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorCamera, view.Translation, (Vector3)view.Forward, 3, 0.01f, BlendType);
                MyTransparentGeometry.AddPointBillboard(MaterialDot, ColorCamera, view.Translation, 0.04f, 0, blendType: BlendType);

                if(canDrawLabel)
                {
                    Vector3D labelDir = view.Right;
                    Vector3D labelLineStart = view.Translation;

                    drawInstance.LabelRender.DrawLineLabel(LabelType.Camera, labelLineStart, labelDir, ColorCamera, "Camera", scale: 0.75f, alwaysOnTop: true);
                }
            }
            #endregion
        }
    }
}
