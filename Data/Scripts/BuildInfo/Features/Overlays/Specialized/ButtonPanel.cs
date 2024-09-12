using Digi.BuildInfo.Features.LiveData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class ButtonPanel : SpecializedOverlayBase
    {
        Color Color = new Color(20, 255, 100);
        Color ColorLines = new Color(20, 255, 100) * SolidOverlayAlpha;

        public ButtonPanel(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_ButtonPanel));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BData_ButtonPanel data = Main.LiveDataHandler.Get<BData_ButtonPanel>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            if(data.Buttons.Count > 0)
            {
                bool drawLabel = drawInstance.LabelRender.CanDrawLabel();

                foreach(BData_ButtonPanel.ButtonInfo buttonInfo in data.Buttons)
                {
                    MatrixD matrix = buttonInfo.LocalMatrix * blockWorldMatrix;

                    MySimpleObjectDraw.DrawTransparentBox(ref matrix, ref OverlayDrawInstance.UnitBB, ref ColorLines, MySimpleObjectRasterizer.Wireframe, 1, lineWidth: 0.01f, lineMaterial: MaterialLaser, blendType: BlendType);

                    if(drawLabel)
                    {
                        Vector3D labelPos = matrix.Translation;

                        // direction towards camera
                        var dir = Vector3D.Normalize(MyAPIGateway.Session.Camera.Position - labelPos);
                        // direction from block center
                        //var dir = Vector3D.Normalize(labelPos - drawMatrix.Translation);
                        Vector3D labelDir = blockWorldMatrix.GetDirectionVector(blockWorldMatrix.GetClosestDirection(dir));

                        drawInstance.LabelRender.DynamicLabel.Clear().Append("Button ").Append(buttonInfo.Index + 1);
                        drawInstance.LabelRender.DrawLineLabel(LabelType.DynamicLabel, labelPos, labelDir, Color, scale: 1f, lineHeight: 0f, alwaysOnTop: true, align: Draygo.API.HudAPIv2.TextOrientation.center);
                    }
                }
            }
        }
    }
}
