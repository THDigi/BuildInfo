﻿using Digi.BuildInfo.Features.LiveData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class Merge : SpecializedOverlayBase
    {
        Color Color = new Color(20, 255, 100);
        Color ColorLines = new Color(20, 255, 100) * LaserOverlayAlpha;

        public Merge(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_MergeBlock));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BData_Merge data = Main.LiveDataHandler.Get<BData_Merge>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            MatrixD m = data.SensorMatrix * blockWorldMatrix;

            MySimpleObjectDraw.DrawTransparentBox(ref m, ref OverlayDrawInstance.UnitBB, ref ColorLines, MySimpleObjectRasterizer.Wireframe, 2, lineWidth: 0.03f, lineMaterial: MaterialLaser, blendType: BlendType);

            if(drawInstance.LabelRender.CanDrawLabel())
            {
                Vector3D labelDir = drawMatrix.Down;
                Vector3D labelLineStart = m.Translation + (m.Down * OverlayDrawInstance.UnitBB.HalfExtents.Y) + (m.Backward * OverlayDrawInstance.UnitBB.HalfExtents.Z) + (m.Left * OverlayDrawInstance.UnitBB.HalfExtents.X);
                drawInstance.LabelRender.DrawLineLabel(LabelType.MagnetizedArea, labelLineStart, labelDir, Color, "Magnetized area");
            }
        }
    }
}
