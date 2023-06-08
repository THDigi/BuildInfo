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
    public class Piston : SpecializedOverlayBase
    {
        static Color ColorLines = new Color(20, 255, 155) * LaserOverlayAlpha;
        static Color ColorCylinder = new Color(20 / 2, 255 / 2, 155 / 2) * LaserOverlayAlpha;
        static Color ColorWarnLines = new Color(255, 55, 0) * LaserOverlayAlpha;

        const int LinePerDeg = RoundedQualityLow * 2;
        const float LineWidth = 0.03f;
        const float BoxLineWidth = 0.01f;

        public Piston(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_PistonBase));
            Add(typeof(MyObjectBuilder_ExtendedPistonBase));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BData_Piston data = Main.LiveDataHandler.Get<BData_Piston>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            MyPistonBaseDefinition pistonDef = def as MyPistonBaseDefinition;
            if(pistonDef == null)
                return;

            float cellSize;

            // definition limits
            float min = pistonDef.Minimum;
            float max = pistonDef.Maximum;

            // use it's terminal limits for real block
            IMyPistonBase piston = block?.FatBlock as IMyPistonBase;
            if(piston != null)
            {
                min = piston.MinLimit;
                max = piston.MaxLimit;
                cellSize = piston.CubeGrid.GridSize;
            }
            else
            {
                cellSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
            }

            float cellSizeHalf = cellSize / 2f;
            float length = max - min;

            MatrixD boxMatrix = data.TopLocalMatrix * drawMatrix;
            boxMatrix.Translation += boxMatrix.Up * min;

            MatrixD travelMatrix = boxMatrix;
            travelMatrix.Translation += travelMatrix.Up * cellSizeHalf; // start at edge

            Color colorCylinder = ColorCylinder;
            Color colorLines = ColorLines;
            if(max < min)
            {
                colorCylinder = ColorWarnLines;
                colorLines = ColorWarnLines;
            }

            // cylinder connecting both
            MatrixD cylinderMatrix = travelMatrix;
            cylinderMatrix.Translation += cylinderMatrix.Up * (length / 2); // cylinder has its pivot in center
            Utils.DrawTransparentCylinder(ref cylinderMatrix, cellSizeHalf, length, ref colorCylinder, MySimpleObjectRasterizer.Wireframe, (360 / LinePerDeg), MaterialSquare, MaterialLaser, LineWidth, drawCaps: false, blendType: BlendType);

            // wire box at bottom
            BoundingBoxD boxLocalBB = new BoundingBoxD(new Vector3D(-cellSizeHalf), new Vector3D(cellSizeHalf));
            MySimpleObjectDraw.DrawTransparentBox(ref boxMatrix, ref boxLocalBB, ref colorLines, MySimpleObjectRasterizer.Wireframe, 1, BoxLineWidth, MaterialSquare, MaterialLaser, blendType: BlendType);

            // wire box at top
            boxMatrix.Translation += boxMatrix.Up * length;
            MySimpleObjectDraw.DrawTransparentBox(ref boxMatrix, ref boxLocalBB, ref colorLines, MySimpleObjectRasterizer.Wireframe, 1, BoxLineWidth, MaterialSquare, MaterialLaser, blendType: BlendType);


            // simple squares

            //Vector3D centerBottom = travelMatrix.Translation;
            //MyTransparentGeometry.AddLineBillboard(MaterialLaser, Color, centerBottom + travelMatrix.Left * cellSizeHalf + travelMatrix.Backward * cellSizeHalf, travelMatrix.Forward, cellSize, LineWidth, BlendType);
            //MyTransparentGeometry.AddLineBillboard(MaterialLaser, Color, centerBottom + travelMatrix.Left * cellSizeHalf + travelMatrix.Forward * cellSizeHalf, travelMatrix.Right, cellSize, LineWidth, BlendType);
            //MyTransparentGeometry.AddLineBillboard(MaterialLaser, Color, centerBottom + travelMatrix.Right * cellSizeHalf + travelMatrix.Forward * cellSizeHalf, travelMatrix.Backward, cellSize, LineWidth, BlendType);
            //MyTransparentGeometry.AddLineBillboard(MaterialLaser, Color, centerBottom + travelMatrix.Right * cellSizeHalf + travelMatrix.Backward * cellSizeHalf, travelMatrix.Left, cellSize, LineWidth, BlendType);

            //Vector3D centerTop = travelMatrix.Translation + travelMatrix.Up * length;
            //MyTransparentGeometry.AddLineBillboard(MaterialLaser, Color, centerTop + travelMatrix.Left * cellSizeHalf + travelMatrix.Backward * cellSizeHalf, travelMatrix.Forward, cellSize, LineWidth, BlendType);
            //MyTransparentGeometry.AddLineBillboard(MaterialLaser, Color, centerTop + travelMatrix.Left * cellSizeHalf + travelMatrix.Forward * cellSizeHalf, travelMatrix.Right, cellSize, LineWidth, BlendType);
            //MyTransparentGeometry.AddLineBillboard(MaterialLaser, Color, centerTop + travelMatrix.Right * cellSizeHalf + travelMatrix.Forward * cellSizeHalf, travelMatrix.Backward, cellSize, LineWidth, BlendType);
            //MyTransparentGeometry.AddLineBillboard(MaterialLaser, Color, centerTop + travelMatrix.Right * cellSizeHalf + travelMatrix.Backward * cellSizeHalf, travelMatrix.Left, cellSize, LineWidth, BlendType);
        }
    }
}
