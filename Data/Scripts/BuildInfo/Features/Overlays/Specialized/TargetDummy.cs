using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class TargetDummy : SpecializedOverlayBase
    {
        Vector4 ColorCrit = new Color(255, 180, 55);
        Vector4 ColorLimb = new Color(55, 100, 155);
        const float PointRadius = 0.05f;

        public TargetDummy(SpecializedOverlays processor) : base(processor)
        {
            Add(Hardcoded.TargetDummyType);
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            MyTargetDummyBlockDefinition dummyDef = def as MyTargetDummyBlockDefinition;
            if(dummyDef == null)
                return;

            BData_TargetDummy data = Main.LiveDataHandler.Get<BData_TargetDummy>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            if(data.ShootableParts.Count > 0)
            {
                bool drawLabel = drawInstance.LabelRender.CanDrawLabel();

                foreach(KeyValuePair<string, BData_TargetDummy.PartInfo> kv in data.ShootableParts)
                {
                    string subpartName = kv.Key;
                    BData_TargetDummy.PartInfo subpartInfo = kv.Value;
                    MatrixD matrix = subpartInfo.LocalMatrix * drawMatrix;

                    Vector4 color = (subpartInfo.IsCritical ? ColorCrit : ColorLimb);
                    MyTransparentGeometry.AddPointBillboard(MaterialDot, color, matrix.Translation, PointRadius, 0, blendType: BlendType);

                    if(drawLabel)
                    {
                        Vector3D labelDir = (subpartName.ContainsIgnoreCase("head") ? drawMatrix.Up : (subpartName.ContainsIgnoreCase("right") ? drawMatrix.Left : drawMatrix.Right)); // bad xD
                        Vector3D labelPos = matrix.Translation;

                        drawInstance.LabelRender.DynamicLabel.Clear().Append(subpartName).Append("\n").Append(subpartInfo.Health).Append(" hp").Append(subpartInfo.IsCritical ? " (critical)" : "");
                        drawInstance.LabelRender.DrawLineLabel(LabelType.DynamicLabel, labelPos, labelDir, color, scale: 0.5f, lineHeight: 1f);
                    }
                }
            }
        }
    }
}
