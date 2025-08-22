using Digi.BuildInfo.Features.LiveData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class Collector : SpecializedOverlayBase
    {
        static Color ColorBox = new Color(255, 140, 0);
        static Color ColorBoxDraw = ColorBox * LaserOverlayAlpha;

        static Color ColorRaycast = new Color(0, 255, 255);
        static Color ColorRaycastDraw = ColorRaycast * LaserOverlayAlpha;

        enum HitState { None, ValidHit, HitSameGrid }

        HitState HitStatus;
        float HitRatio;

        public Collector(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_Collector));

            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
        }

        void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            HitStatus = HitState.None;
            HitRatio = -1;
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BData_Collector data = Main.LiveDataHandler.Get<BData_Collector>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            bool canDrawLabel = drawInstance.LabelRender.CanDrawLabel();

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            MatrixD m = data.BoxLocalMatrix * blockWorldMatrix;

            MySimpleObjectDraw.DrawTransparentBox(ref m, ref OverlayDrawInstance.UnitBB, ref ColorBoxDraw, MySimpleObjectRasterizer.Wireframe, 2, lineWidth: 0.03f, lineMaterial: MaterialLaser, blendType: BlendType);

#if !(VERSION_200 || VERSION_201 || VERSION_202 || VERSION_203 || VERSION_204 || VERSION_205 || VERSION_206) // HACK: backwards compatible
            #region Draw farmable collection laser and raycast to detect hits
            // HACK: from MyCollector.UpdateBeforeSimulation100()
            const float Distance = 2.0f;
            Vector3D from = Vector3D.Transform(data.BoxLocalMatrix.Translation, ref blockWorldMatrix);
            Vector3D direction = Vector3D.TransformNormal(data.DummyForward, ref blockWorldMatrix); // DummyForward is cached normalized instead
            Vector3D to = from + direction * Distance;

            MyTransparentGeometry.AddLineBillboard(MaterialLaser, ColorRaycastDraw, from, direction, 2.0f, 0.1f, BlendType);

            if(Main.Tick % 10 == 0)
            {
                HitStatus = HitState.None;
                HitRatio = -1;

                IHitInfo hit;
                if(MyAPIGateway.Physics.CastRay(from, to, out hit, 29)) // 29=ExplosionRaycastLayer???
                {
                    MyCubeGrid hitGrid = hit.HitEntity as MyCubeGrid;
                    if(hitGrid != null)
                    {
                        if(block?.CubeGrid != hitGrid)
                        {
                            IMySlimBlock hitSlim = hitGrid.GetCubeBlock(hitGrid.WorldToGridInteger(hit.Position + direction * 0.00001));

                            //IMyFarmPlotLogic component;
                            //if(hitSlim?.FatBlock != null && hitSlim.FatBlock.Components.TryGet(out component))
                            if(hitSlim != null)
                            {
                                HitStatus = HitState.ValidHit;
                                HitRatio = hit.Fraction;

                                // component.IsPlantFullyGrown || (component.IsPlantPlanted && !component.IsAlive)
                                //AddEntityToTake(hitSlim.FatBlock);
                            }
                        }
                        else
                        {
                            HitStatus = HitState.HitSameGrid;
                        }
                    }
                }
            }

            Vector3D hitPosition = (HitRatio > -1 ? from + direction * (HitRatio * Distance) : to);

            if(HitStatus != HitState.None && HitRatio > -1)
            {
                MyTransparentGeometry.AddPointBillboard(MaterialDot, ColorRaycastDraw, hitPosition, 0.2f, 0, blendType: BlendType);
            }

            if(canDrawLabel)
            {
                var sb = drawInstance.LabelRender.DynamicLabel.Clear().Append("Farmable Collection Beam\nLaser hits a block: ");
                if(HitStatus == HitState.ValidHit)
                    sb.Append("Yes");
                else if(HitStatus == HitState.HitSameGrid)
                    sb.Append("No (hit same grid)");
                else
                    sb.Append("No");

                Vector3D labelDir = direction;
                Vector3D labelLineStart = hitPosition;
                drawInstance.LabelRender.DrawLineLabel(LabelType.DynamicLabel, labelLineStart, labelDir, ColorRaycast, lineHeight: 0, alwaysOnTop: true);
            }
            #endregion
#endif

            if(canDrawLabel)
            {
                Vector3D labelDir = drawMatrix.Down;
                Vector3D labelLineStart = m.Translation + (m.Down * OverlayDrawInstance.UnitBB.HalfExtents.Y) + (m.Backward * OverlayDrawInstance.UnitBB.HalfExtents.Z) + (m.Left * OverlayDrawInstance.UnitBB.HalfExtents.X);
                drawInstance.LabelRender.DrawLineLabel(LabelType.CollectionArea, labelLineStart, labelDir, ColorBox, "Collection Area");
            }
        }
    }
}
