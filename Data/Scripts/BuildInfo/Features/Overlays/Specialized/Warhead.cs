using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
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
    public class Warhead : SpecializedOverlayBase
    {
        static Color ColorExplosion = new Color(255, 100, 0);
        static Color ColorExplosionLines = ColorExplosion * LaserOverlayAlpha;

        static Color ColorScan = Color.Blue;
        static Color ColorScanLines = ColorScan * LaserOverlayAlpha;

        const int LineEveryDeg = RoundedQualityLow;
        float LineThickness = 0.03f;

        // reminder that this class is a singleton, Draw() from the same instance called on multiple blocks.

        readonly List<MyEntity> TempEntities = new List<MyEntity>();

        readonly Dictionary<OverlayDrawInstance, ScanResults> ScanDataPerInstance = new Dictionary<OverlayDrawInstance, ScanResults>();

        class ScanResults
        {
            public List<IMyWarhead> WarheadsInRadius = new List<IMyWarhead>();
        }

        public Warhead(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_Warhead));

            Main.Overlays.DrawStopped += OverlaysDrawStopped;
        }

        void OverlaysDrawStopped()
        {
            ScanDataPerInstance.Clear();
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            MyWarheadDefinition warheadDef = def as MyWarheadDefinition;
            if(warheadDef == null)
                return;

            bool drawLabel = drawInstance.LabelRender.CanDrawLabel();

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            // HACK: hardcoded from MyWarhead.MarkForExplosion()
            #region Compute
            Vector3D warheadPos = blockWorldMatrix.Translation;
            float gridRadius = drawInstance.CellSize * Hardcoded.WarheadScanRadiusGridMul;
            double searchRadius = gridRadius * Hardcoded.WarheadScanRadiusShrink;
            double searchRadiusSq = searchRadius * searchRadius;

            BoundingSphereD searchSphere = new BoundingSphereD(warheadPos, searchRadius);
            BoundingSphereD particleSphere = BoundingSphereD.CreateInvalid();

            ScanResults scanData = ScanDataPerInstance.GetValueOrNew(drawInstance);

            // less frequent nearby entity scan because it's not exactly fast (because of ALL entities part it scans for child entities too)
            if(Main.Tick % (Constants.TicksPerSecond / 4) == 0)
            {
                TempEntities.Clear();
                MyGamePruningStructure.GetAllEntitiesInSphere(ref searchSphere, TempEntities);

                scanData.WarheadsInRadius.Clear();

                foreach(MyEntity ent in TempEntities)
                {
                    IMyWarhead otherWarhead = ent as IMyWarhead;
                    if(otherWarhead?.CubeGrid?.Physics == null)
                        continue;

                    MyCubeGrid otherGrid = (MyCubeGrid)otherWarhead.CubeGrid;
                    if(otherGrid.Projector != null)
                        continue;

                    if(Vector3D.DistanceSquared(warheadPos, otherWarhead.GetPosition()) < searchRadiusSq)
                    {
                        scanData.WarheadsInRadius.Add(otherWarhead);
                    }
                }

                TempEntities.Clear();
            }

            int warheadsInside = 0;

            // include ghost or the math fails hard
            if(block == null)
            {
                warheadsInside++;
                float radius = gridRadius + drawInstance.CellSize;
                particleSphere = particleSphere.Include(new BoundingSphereD(warheadPos, radius));
            }

            foreach(IMyWarhead otherWarhead in scanData.WarheadsInRadius)
            {
                warheadsInside++;

                float radius = gridRadius + otherWarhead.CubeGrid.GridSize;
                particleSphere = particleSphere.Include(new BoundingSphereD(otherWarhead.GetPosition(), radius));
            }

            float radiusCapped = Math.Min(Hardcoded.WarheadMaxRadius, (1f + Hardcoded.WarheadRadiusRatioPerOther * warheadsInside) * warheadDef.ExplosionRadius);

            // particleSphere.Radius can't realistically go past the cap here so ignoring it in the text info
            BoundingSphereD explosionSphere = new BoundingSphereD(particleSphere.Center, Math.Max(radiusCapped, particleSphere.Radius));
            #endregion

            #region Drawing
            DrawSphere(ColorScanLines, drawMatrix, searchSphere);
            DrawSphere(ColorExplosionLines, drawMatrix, explosionSphere);

            MyTransparentGeometry.AddPointBillboard(MaterialDot, ColorExplosion, explosionSphere.Center, 0.2f, 0, blendType: BlendType);

            Vector3D onTopPos = explosionSphere.Center;
            float depthMul = OverlayDrawInstance.ConvertToAlwaysOnTop(ref onTopPos);
            MyTransparentGeometry.AddPointBillboard(MaterialDot, ColorExplosion, onTopPos, 0.05f * depthMul, 0, blendType: BlendType);

            if(drawLabel)
            {
                drawInstance.LabelRender.DynamicLabel.Clear().Append("Damage center here\nRadius: ").DistanceFormat((float)explosionSphere.Radius, 2)
                    .Append("\nWarheads within sphere: ").Append(warheadsInside);

                Vector3D labelDir = drawMatrix.Left + drawMatrix.Up + drawMatrix.Backward;
                drawInstance.LabelRender.DrawLineLabel(LabelType.DynamicLabel, explosionSphere.Center, labelDir, ColorExplosion, alwaysOnTop: true);

                labelDir = drawMatrix.Down;
                Vector3D sphereEdge = searchSphere.Center + (labelDir * searchSphere.Radius);
                drawInstance.LabelRender.DrawLineLabel(LabelType.SensorRadius, sphereEdge, labelDir, ColorScanLines, "Entity detection radius");
            }
            #endregion
        }

        void DrawSphere(Color color, MatrixD m, BoundingSphereD sphere)
        {
            Color colorWire = color * LaserOverlayAlpha;
            Color colorSolid = color * 0.3f;
            m.Translation = sphere.Center;
            float radius = (float)sphere.Radius;
            Utils.DrawTransparentSphere(ref m, radius, ref colorWire, MySimpleObjectRasterizer.Wireframe, (360 / LineEveryDeg), lineThickness: LineThickness, material: MaterialLaser, blendType: BlendType);
            Utils.DrawTransparentSphere(ref m, radius, ref colorSolid, MySimpleObjectRasterizer.Solid, (360 / LineEveryDeg), lineThickness: LineThickness, material: MaterialSquare, blendType: BlendType);
        }
    }
}
