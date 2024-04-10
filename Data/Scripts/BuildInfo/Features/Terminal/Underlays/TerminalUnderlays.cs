using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.Overlays;
using Digi.BuildInfo.Features.Overlays.Specialized;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRageMath;
using PB_FunctionalBlock = Sandbox.ModAPI.Ingame.IMyFunctionalBlock;

namespace Digi.BuildInfo.Features.Terminal.Underlays
{
    /// <summary>
    /// Block overlays while in terminal for that block to aid with a few things.
    /// </summary>
    public class TerminalUnderlays : ModComponent
    {
        public bool ShowSpecializedOverlays = false;

        OverlayDrawInstance DrawInstance;
        BlockSelectInfo BlockSelectInfo = new BlockSelectInfo();

        int NextLabelIdx = 0;
        List<HudAPIv2.SpaceMessage> Labels = new List<HudAPIv2.SpaceMessage>();

        public TerminalUnderlays(BuildInfoMod main) : base(main)
        {
            // TODO: not working... would need to draw textAPI stuff myself
            // could even change them all to space messages and update positions myself too, to fix the clipping...
            UpdateOrder = -400; // for Draw() mainly to render under fake GUI
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
            {
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
                HideLabels();
            }
        }

        public override void UpdateDraw()
        {
            HideLabels();

            List<IMyTerminalBlock> blocks = Main.TerminalInfo.SelectedInTerminal;
            if(blocks.Count <= 0)
                return;

            if(blocks.Count > 30)
                return;

            if(Main.EventToolbarInfo.DrawingOverlays)
                return;

            const double MaxDistanceSq = 100 * 100;
            Vector3 camPos = MyAPIGateway.Session.Camera.Position;

            Color blockBBcolor = new Color(100, 255, 155);
            Color subBlockColor = new Color(200, 155, 55);

            bool oneSelection = blocks.Count == 1;
            bool fewSelected = blocks.Count <= 6;

            foreach(IMyTerminalBlock block in blocks)
            {
                if(!oneSelection)
                {
                    BoundingBoxD blockBB = block.WorldAABB;
                    double distSq = Vector3D.DistanceSquared(camPos, blockBB.Center);
                    if(distSq > MaxDistanceSq || !MyAPIGateway.Session.Camera.IsInFrustum(ref blockBB))
                        continue;
                }

                if(!block.HasLocalPlayerAccess())
                {
                    DrawSelectionBox(block, Color.Red);
                    continue;
                }

                DrawSelectionBox(block, block.IsFunctional ? blockBBcolor : Color.Yellow);

                BoundingBoxD localBB = BlockSelectInfo.ModelBB ?? BlockSelectInfo.Boundaries;
                MyOrientedBoundingBoxD obb = new MyOrientedBoundingBoxD(localBB, BlockSelectInfo.BlockMatrix);

                bool isAlreadyDrawingOverlay = Main.Overlays.ActiveOnBlocks.Contains(block.SlimBlock);

                if(!isAlreadyDrawingOverlay && ShowSpecializedOverlays)
                {
                    SpecializedOverlayBase overlay = Main.SpecializedOverlays.Get(block.BlockDefinition.TypeId);
                    if(overlay != null)
                    {
                        MatrixD drawMatrix = Utils.GetBlockCenteredWorldMatrix(block.SlimBlock);
                        overlay.Draw(ref drawMatrix, DrawInstance, (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition, block.SlimBlock);
                    }
                }

                if(!ShowSpecializedOverlays && oneSelection && !isAlreadyDrawingOverlay)
                {
                    IMyButtonPanel button = block as IMyButtonPanel;
                    if(button != null)
                    {
                        SpecializedOverlayBase overlay = Main.SpecializedOverlays.Get(block.BlockDefinition.TypeId);
                        if(overlay != null)
                        {
                            MatrixD drawMatrix = Utils.GetBlockCenteredWorldMatrix(block.SlimBlock);
                            overlay.Draw(ref drawMatrix, DrawInstance, (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition, block.SlimBlock);
                        }
                        continue;
                    }
                }

                // TODO: show screenareas when material identification can be done via IMyModel
            }
        }

        void HideLabels()
        {
            foreach(HudAPIv2.SpaceMessage label in Labels)
            {
                label.Visible = false;
            }
        }

        Vector3D[] SourceCorners = new Vector3D[8];
        Vector3D[] TargetCorners = new Vector3D[8];

        void DrawRelatedBlock(IMyTerminalBlock sourceBlock, MyOrientedBoundingBoxD sourceOBB, IMyTerminalBlock targetBlock, Color color, string labelText)
        {
            HudAPIv2.SpaceMessage label;

            if(NextLabelIdx >= Labels.Count)
            {
                label = new HudAPIv2.SpaceMessage();
                label.Message = new StringBuilder(128);
                label.SkipLinearRGB = true;
                label.Font = FontsHandler.TextAPI_OutlinedFont;
                label.Scale = 0.4 * OverlayDrawInstance.DepthRatio;
                Labels.Add(label);
            }
            else
            {
                label = Labels[NextLabelIdx];
            }

            NextLabelIdx++;

            MatrixD camWM = MyAPIGateway.Session.Camera.WorldMatrix;

            MatrixD closeWM = camWM;
            OverlayDrawInstance.ConvertToAlwaysOnTop(ref closeWM);

            label.Visible = true;
            label.Left = camWM.Left;
            label.Up = camWM.Up;
            label.Message.Clear().Color(color).Append(labelText);

            DrawSelectionBox(targetBlock, color);

            var localBB = BlockSelectInfo.ModelBB ?? BlockSelectInfo.Boundaries;
            var targetOBB = new MyOrientedBoundingBoxD(localBB, BlockSelectInfo.BlockMatrix);

            Vector3D centerClose = targetOBB.Center;
            OverlayDrawInstance.ConvertToAlwaysOnTop(ref centerClose);
            label.WorldPosition = centerClose;

            sourceOBB.GetCorners(SourceCorners, 0);
            targetOBB.GetCorners(TargetCorners, 0);

            Vector3D centerBetweenOBBs = sourceOBB.Center + (targetOBB.Center - sourceOBB.Center) * 0.5;

            Vector3D sourceCorner = GetClosestCorner(SourceCorners, centerBetweenOBBs);
            Vector3D targetCorner = GetClosestCorner(TargetCorners, centerBetweenOBBs);

            bool isLarge = (sourceBlock.CubeGrid.GridSizeEnum == MyCubeSize.Large);
            float distance = (float)Vector3D.Distance(MyAPIGateway.Session.Camera.Position, centerBetweenOBBs);
            float distRatio = MathHelper.Lerp(1f, lineThickMulMax, MathHelper.Clamp(distance / distRatioMax, distRatioMin, distRatioMax));
            float thick = (isLarge ? 0.02f : 0.016f) * distRatio;

            MyTransparentGeometry.AddLineBillboard(OverlayDrawInstance.MaterialLaser, color, sourceCorner, (targetCorner - sourceCorner), 1f, thick, VRageRender.MyBillboard.BlendTypeEnum.PostPP);

            OverlayDrawInstance.ConvertToAlwaysOnTop(ref sourceCorner);
            OverlayDrawInstance.ConvertToAlwaysOnTop(ref targetCorner);
            thick *= OverlayDrawInstance.DepthRatioF;

            MyTransparentGeometry.AddLineBillboard(OverlayDrawInstance.MaterialLaser, color * 0.25f, sourceCorner, (targetCorner - sourceCorner), 1f, thick, VRageRender.MyBillboard.BlendTypeEnum.PostPP);
        }

        static Vector3D GetClosestCorner(Vector3D[] corners, Vector3D point)
        {
            double closestDistSq = double.MaxValue;
            Vector3D closestCorner = Vector3D.Zero;

            for(int i = 0; i < corners.Length; i++)
            {
                Vector3D corner = corners[i];
                double distSq = Vector3D.DistanceSquared(point, corner);

                if(distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestCorner = corner;
                }
            }

            return closestCorner;
        }

        const float distRatioMin = 0;
        const float distRatioMax = 100f; // (float)Dev.GetValueScroll("distRatioMax", 100, MyKeys.D1);
        const float lineThickMulMax = 30f; // (float)Dev.GetValueScroll("lineThickMulMax", 30, MyKeys.D2);

        void DrawSelectionBox(IMyTerminalBlock block, Color color)
        {
            bool isLarge = (block.CubeGrid.GridSizeEnum == MyCubeSize.Large);

            float distance = (float)Vector3D.Distance(MyAPIGateway.Session.Camera.Position, block.WorldAABB.Center);
            float distRatio = MathHelper.Lerp(1f, lineThickMulMax, MathHelper.Clamp(distance / distRatioMax, distRatioMin, distRatioMax));
            float lineWidth = (isLarge ? 0.02f : 0.016f) * distRatio;

            BlockSelectInfo.ClearCaches();
            Main.OverrideToolSelectionDraw.GetBlockModelBB(block.SlimBlock, BlockSelectInfo, 0f);
            Main.OverrideToolSelectionDraw.DrawSelection(BlockSelectInfo.ModelMatrix, BlockSelectInfo.ModelBB ?? BlockSelectInfo.Boundaries, color, lineWidth);
        }
    }
}
