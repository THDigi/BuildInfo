using System;
using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using BlendType = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features
{
    public class BlockSelectInfo
    {
        public BoundingBoxD? ModelBB = null;
        public MatrixD ModelMatrix;

        public BoundingBoxD Boundaries;
        public MatrixD BlockMatrix;

        public void ClearCaches()
        {
            ModelBB = null;
        }
    }

    public class OverrideToolSelectionDraw : ModComponent
    {
        readonly MyStringId SelectionLineMaterial = Constants.Mat_Laser;
        readonly MyStringId SelectionCornerMaterial = Constants.Mat_LaserDot;
        const BlendType SelectionBlendType = BlendType.PostPP;

        bool eventHooked;

        BlockSelectInfo BlockSelectInfo = new BlockSelectInfo();
        Vector4? ColorCache = null;
        readonly Vector3D[] Corners = new Vector3D[8];

        public OverrideToolSelectionDraw(BuildInfoMod main) : base(main)
        {
            UpdateOrder = -490; // for Draw() mainly, to always render first (and therefore, under)

            MyEntities.OnEntityCreate += EntityCreated;
            eventHooked = true;
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
            Main.Config.OverrideToolSelectionDraw.ValueAssigned += ConfigValueSet;
        }

        public override void UnregisterComponent()
        {
            MyEntities.OnEntityCreate -= EntityCreated;

            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
            Main.Config.OverrideToolSelectionDraw.ValueAssigned -= ConfigValueSet;
        }

        void ConfigValueSet(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, (newValue && Main.EquipmentMonitor.AimedBlock != null));

            if(eventHooked && !newValue)
            {
                MyEntities.OnEntityCreate -= EntityCreated;
                eventHooked = false;
            }
            else if(!eventHooked && newValue)
            {
                MyEntities.OnEntityCreate += EntityCreated;
                eventHooked = true;
            }
        }

        void EntityCreated(MyEntity ent)
        {
            try
            {
                // disable vanilla selection box
                if(ent is IMyEngineerToolBase && ent.Render.GetType().Name == "MyRenderComponentEngineerTool")
                {
                    ent.Components.Remove<MyRenderComponentBase>();
                    ent.Render = new MyRenderComponent();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            ColorCache = null;
            BlockSelectInfo.ClearCaches();
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, (block != null && Main.Config.OverrideToolSelectionDraw.Value));
        }

        public override void UpdateDraw()
        {
            if(Main.GameConfig.HudState == HudState.OFF)
                return;

            IMySlimBlock aimedBlock = Main.EquipmentMonitor.AimedBlock;
            if(aimedBlock == null)
                return;

            MyCubeBlockDefinition def = Main.EquipmentMonitor.BlockDef;
            if(def == null)
                return;

            MyCubeGrid grid = (MyCubeGrid)aimedBlock.CubeGrid;
            IMyProjector projector = Main.EquipmentMonitor.AimedProjectedBy;

            if(projector != null && !Main.Config.SelectAllProjectedBlocks.Value)
                return;

            Vector4 color;
            #region Compute color
            if(!ColorCache.HasValue || Main.Tick % 30 == 0)
            {
                if(projector != null)
                {
                    if(Main.EquipmentMonitor.IsAnyGrinder)
                    {
                        color = Color.DarkRed;
                    }
                    else
                    {
                        switch(Main.EquipmentMonitor.AimedProjectedCanBuild)
                        {
                            case BuildCheckResult.OK: color = Color.Lime; break;
                            case BuildCheckResult.AlreadyBuilt: color = Color.White; break;
                            case BuildCheckResult.NotWeldable: color = Color.DarkRed; break;
                            case BuildCheckResult.NotConnected:
                            case BuildCheckResult.IntersectedWithGrid:
                            case BuildCheckResult.IntersectedWithSomethingElse: color = Color.Yellow; break;
                            default: color = new Color(255, 0, 255); break;
                        }
                    }
                }
                else
                {
                    if(Main.EquipmentMonitor.IsAnyGrinder)
                    {
                        if(grid.Immune || !grid.Editable || !Utils.CheckSafezoneAction(aimedBlock, SafeZoneAction.Grinding) || !Utils.CheckSafezoneAction(aimedBlock.CubeGrid, SafeZoneAction.Grinding))
                        {
                            color = Color.DarkRed;
                        }
                        else
                        {
                            SplitFlags splitInfo = Main.SplitChecking.GetSplitInfo(aimedBlock);

                            color = new Color(255, 200, 75);

                            if(splitInfo != SplitFlags.None)
                            {
                                if((splitInfo & (SplitFlags.BlockLoss | SplitFlags.Split)) != 0)
                                {
                                    color = new Color(255, 10, 10);
                                }
                                else if((splitInfo & SplitFlags.Disconnect) != 0)
                                {
                                    color = new Color(255, 70, 20);
                                }
                            }
                        }
                    }
                    else
                    {
                        if(!Utils.CheckSafezoneAction(aimedBlock, SafeZoneAction.Welding) || !Utils.CheckSafezoneAction(aimedBlock.CubeGrid, SafeZoneAction.Welding))
                            color = Color.DarkRed;
                        else if(aimedBlock.IsFullIntegrity && !aimedBlock.HasDeformation)
                            color = Color.White;
                        else
                            color = new Color(55, 255, 75);
                    }
                }

                ColorCache = color;
            }
            else
            {
                color = ColorCache.Value;
            }

            float lineWidth = (grid.GridSizeEnum == MyCubeSize.Large ? 0.02f : 0.016f);
            double inflate = (grid.GridSizeEnum == MyCubeSize.Large ? 0.1 : 0.03);

            GetBlockModelBB(aimedBlock, BlockSelectInfo, inflate);

            if(BlockSelectInfo.ModelBB.HasValue)
                DrawSelection(BlockSelectInfo.ModelMatrix, BlockSelectInfo.ModelBB.Value, color, lineWidth);
            else
                DrawSelection(BlockSelectInfo.BlockMatrix, BlockSelectInfo.Boundaries, color, lineWidth);
            #endregion
        }

        List<MyEntity> _inputs = new List<MyEntity>();
        List<MyEntity> _inputsForNext = new List<MyEntity>();

        /// <summary>
        /// Fills given <see cref="BlockSelectInfo"/> object with data.
        /// Also only updates model BB 4 times a second if same object is fed.
        /// <paramref name="inflate"/> is used to inflate the bb once, because modelBB is cached doing it outside would increase it in size every frame.
        /// </summary>
        public void GetBlockModelBB(IMySlimBlock block, BlockSelectInfo fillData, double inflate = 0)
        {
            MyCubeBlock fatBlock = block.FatBlock as MyCubeBlock;
            MyCubeBlockDefinition def = (MyCubeBlockDefinition)block.BlockDefinition;
            MyCubeGrid grid = (MyCubeGrid)block.CubeGrid;

            Vector3 halfSize = def.Size * grid.GridSizeHalf;
            fillData.Boundaries = new BoundingBoxD(-halfSize, halfSize);
            if(inflate != 0)
                fillData.Boundaries.Inflate(inflate);

            Matrix localMatrix;
            block.Orientation.GetMatrix(out localMatrix);
            localMatrix.Translation = (block.Max + block.Min) * grid.GridSizeHalf; // local block float-center
            fillData.BlockMatrix = localMatrix * grid.WorldMatrix;

            if(fatBlock != null && def.BlockTopology == MyBlockTopology.TriangleMesh && !(def is MyParachuteDefinition))
            {
                fillData.ModelMatrix = fatBlock.PositionComp.WorldMatrixRef;

                const bool DebugDrawSubparts = false;

                if(DebugDrawSubparts || !fillData.ModelBB.HasValue || Main.Tick % (Constants.TicksPerSecond / 4) == 0)
                {
                    BoundingBoxD modelBB = (BoundingBoxD)fatBlock.PositionComp.LocalAABB;

                    #region Subpart localBB inclusion
                    // HACK: recursion without methods, avoids mod profiler...
                    _inputs.Clear();
                    _inputsForNext.Clear();

                    _inputs.Add(fatBlock);

                    MatrixD transformToBlockLocal = fatBlock.PositionComp.WorldMatrixInvScaled;

                    while(_inputs.Count > 0)
                    {
                        foreach(MyEntity entity in _inputs)
                        {
                            if(entity.Subparts != null)
                            {
                                foreach(MyEntitySubpart subpart in entity.Subparts.Values)
                                {
                                    bool visible = subpart.Render.Visible;

                                    if(DebugDrawSubparts)
                                    {
                                        MatrixD wm = subpart.WorldMatrix;
                                        BoundingBoxD localBox = (BoundingBoxD)subpart.PositionComp.LocalAABB;
                                        Color color = (visible ? Color.Lime : Color.Red) * 0.75f;
                                        MySimpleObjectDraw.DrawTransparentBox(ref wm, ref localBox, ref color, MySimpleObjectRasterizer.SolidAndWireframe, 1, 0.01f, Constants.Mat_Square, SelectionLineMaterial, blendType: BlendType.AdditiveTop);
                                    }

                                    if(!visible)
                                        continue;

                                    MyOrientedBoundingBoxD obb = new MyOrientedBoundingBoxD(subpart.PositionComp.LocalAABB, subpart.PositionComp.WorldMatrixRef);
                                    obb.GetCorners(Corners, 0);

                                    for(int i = 0; i < Corners.Length; i++)
                                    {
                                        Vector3D corner = Corners[i];
                                        modelBB.Include(Vector3D.Transform(corner, transformToBlockLocal));
                                    }

                                    _inputsForNext.Add(subpart);
                                }
                            }
                        }

                        _inputs.Clear();
                        MyUtils.Swap(ref _inputsForNext, ref _inputs);
                    }

                    _inputs.Clear();
                    _inputsForNext.Clear();
                    #endregion

                    if(inflate != 0)
                        modelBB.Inflate(inflate);

                    fillData.ModelBB = modelBB;
                }
            }
        }

        public void DrawSelection(MatrixD worldMatrix, BoundingBoxD localBB, Color color, float lineWidth)
        {
            #region Selection lines
            const float LineLength = 2f; // directions are half-long

            Vector3D center = Vector3D.Transform((localBB.Min + localBB.Max) * 0.5, worldMatrix);
            Vector3D halfExtent = (localBB.Max - localBB.Min) * 0.5;
            Vector3 left = (Vector3)(worldMatrix.Left * halfExtent.X);
            Vector3 up = (Vector3)(worldMatrix.Up * halfExtent.Y);
            Vector3 back = (Vector3)(worldMatrix.Backward * halfExtent.Z);

            Vector3D top = center + up;
            Vector3D bottom = center - up;

            Vector3D cornerTop1 = top + left + back;
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop1, -left, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop1, -up, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop1, -back, LineLength, lineWidth, SelectionBlendType);

            Vector3D cornerTop2 = top - left - back;
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop2, left, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop2, -up, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop2, back, LineLength, lineWidth, SelectionBlendType);

            Vector3D cornerBottom1 = bottom + left - back;
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom1, -left, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom1, up, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom1, back, LineLength, lineWidth, SelectionBlendType);

            Vector3D cornerBottom2 = bottom - left + back;
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom2, left, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom2, up, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom2, -back, LineLength, lineWidth, SelectionBlendType);
            #endregion Selection lines

            #region Selection corners
            float cornerRadius = lineWidth;
            Vector4 colorCorner = color;

            MyTransparentGeometry.AddPointBillboard(SelectionCornerMaterial, colorCorner, cornerTop1, cornerRadius, 0, blendType: SelectionBlendType);
            MyTransparentGeometry.AddPointBillboard(SelectionCornerMaterial, colorCorner, cornerTop2, cornerRadius, 0, blendType: SelectionBlendType);
            MyTransparentGeometry.AddPointBillboard(SelectionCornerMaterial, colorCorner, cornerBottom1, cornerRadius, 0, blendType: SelectionBlendType);
            MyTransparentGeometry.AddPointBillboard(SelectionCornerMaterial, colorCorner, cornerBottom2, cornerRadius, 0, blendType: SelectionBlendType);

            Vector3D cornerTop3 = top - left + back;
            MyTransparentGeometry.AddPointBillboard(SelectionCornerMaterial, colorCorner, cornerTop3, cornerRadius, 0, blendType: SelectionBlendType);
            Vector3D cornerTop4 = top + left - back;
            MyTransparentGeometry.AddPointBillboard(SelectionCornerMaterial, colorCorner, cornerTop4, cornerRadius, 0, blendType: SelectionBlendType);

            Vector3D cornerBottom3 = bottom - left - back;
            MyTransparentGeometry.AddPointBillboard(SelectionCornerMaterial, colorCorner, cornerBottom3, cornerRadius, 0, blendType: SelectionBlendType);
            Vector3D cornerBottom4 = bottom + left + back;
            MyTransparentGeometry.AddPointBillboard(SelectionCornerMaterial, colorCorner, cornerBottom4, cornerRadius, 0, blendType: SelectionBlendType);
            #endregion corners

            #region See-through-walls lines
            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;

            const float DepthRatio = 0.01f;
            lineWidth *= DepthRatio * 0.5f; // and half as thin
            color *= 0.5f; // half opacity too

            center = camMatrix.Translation + ((center - camMatrix.Translation) * DepthRatio);

            //halfExtent *= DepthRatio;
            left *= DepthRatio;
            up *= DepthRatio;
            back *= DepthRatio;

            top = center + up;
            bottom = center - up;

            cornerTop1 = top + left + back;
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop1, -left, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop1, -up, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop1, -back, LineLength, lineWidth, SelectionBlendType);

            cornerTop2 = top - left - back;
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop2, left, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop2, -up, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop2, back, LineLength, lineWidth, SelectionBlendType);

            cornerBottom1 = bottom + left - back;
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom1, -left, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom1, up, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom1, back, LineLength, lineWidth, SelectionBlendType);

            cornerBottom2 = bottom - left + back;
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom2, left, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom2, up, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom2, -back, LineLength, lineWidth, SelectionBlendType);
            #endregion
        }
    }
}
