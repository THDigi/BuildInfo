using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.Overlays;
using Digi.BuildInfo.Features.Overlays.Specialized;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Input;
using VRageMath;

namespace Digi.BuildInfo.Features.Terminal.Underlays
{
    /// <summary>
    /// Block overlays while in terminal for that block to aid with a few things.
    /// </summary>
    public class TerminalUnderlays : ModComponent
    {
        OverlayDrawInstance DrawInstance;
        BlockSelectInfo BlockSelectInfo = new BlockSelectInfo();

        public TerminalUnderlays(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.TerminalInfo.SelectedChanged += TerminalSelectionChanged;
            Main.GUIMonitor.ScreenRemoved += ScreenRemoved;

            DrawInstance = new OverlayDrawInstance(Main.Overlays, GetType().Name);
            DrawInstance.LabelRender.ForceDrawLabel = true;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.TerminalInfo.SelectedChanged -= TerminalSelectionChanged;
            Main.GUIMonitor.ScreenRemoved -= ScreenRemoved;

            DrawInstance = null;
        }

        void TerminalSelectionChanged()
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);
        }

        void ScreenRemoved(string screenName)
        {
            if(!Main.GUIMonitor.InTerminal)
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
        }

        public override void UpdateDraw()
        {
            float distRatioMin = 0;
            float distRatioMax = (float)Dev.GetValueScroll("distRatioMax", 100, MyKeys.D1);
            float lineThickMulMax = (float)Dev.GetValueScroll("lineThickMulMax", 30, MyKeys.D2);

            List<IMyTerminalBlock> blocks = Main.TerminalInfo.SelectedInTerminal;
            if(blocks.Count <= 0)
                return;

            if(Main.EventToolbarInfo.DrawingOverlays)
                return;

            Color bbColor = new Color(100, 255, 155);

            const double MaxDistanceSq = 100 * 100;
            Vector3 camPos = MyAPIGateway.Session.Camera.Position;

            if(blocks.Count > 30)
                return;

            foreach(IMyTerminalBlock block in blocks)
            {
                BoundingBoxD blockBB = block.WorldAABB;
                double distSq = Vector3D.DistanceSquared(camPos, blockBB.Center);
                if(distSq > MaxDistanceSq || !MyAPIGateway.Session.Camera.IsInFrustum(ref blockBB))
                    continue;

                #region Draw selection box
                {
                    bool isLarge = (block.CubeGrid.GridSizeEnum == MyCubeSize.Large);

                    float dist = (float)Math.Sqrt(distSq);
                    float distRatio = MathHelper.Lerp(1f, lineThickMulMax, MathHelper.Clamp(dist / distRatioMax, distRatioMin, distRatioMax));
                    float lineWidth = (isLarge ? 0.02f : 0.016f) * distRatio;

                    BlockSelectInfo.ClearCaches();
                    Main.OverrideToolSelectionDraw.GetBlockModelBB(block.SlimBlock, BlockSelectInfo, 0f);
                    Main.OverrideToolSelectionDraw.DrawSelection(BlockSelectInfo.ModelMatrix, BlockSelectInfo.ModelBB ?? BlockSelectInfo.Boundaries, bbColor, lineWidth);
                }
                #endregion

                if(!block.HasLocalPlayerAccess())
                    continue;

                IMyButtonPanel button = block as IMyButtonPanel;
                if(button != null)
                {
                    SpecializedOverlayBase overlay = Main.SpecializedOverlays.Get(block.BlockDefinition.TypeId);
                    if(overlay != null)
                    {
                        MatrixD drawMatrix = block.WorldMatrix;
                        overlay.Draw(ref drawMatrix, DrawInstance, (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition, block.SlimBlock);
                    }
                    continue;
                }
            }
        }
    }
}
