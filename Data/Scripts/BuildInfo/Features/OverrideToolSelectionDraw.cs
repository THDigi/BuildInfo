using System;
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
    public class OverrideToolSelectionDraw : ModComponent
    {
        readonly MyStringId SelectionLineMaterial = MyStringId.GetOrCompute("BuildInfo_Laser");
        readonly MyStringId SelectionCornerMaterial = MyStringId.GetOrCompute("BuildInfo_LaserDot");
        const BlendType SelectionBlendType = BlendType.PostPP;

        bool eventHooked;

        Vector4? ColorCache = null;
        BoundingBoxD? LocalBBCache = null;
        readonly Vector3D[] Corners = new Vector3D[8];

        public OverrideToolSelectionDraw(BuildInfoMod main) : base(main)
        {
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
            LocalBBCache = null;
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
                        if(grid.Immune || !grid.Editable || !Utils.CheckSafezoneAction(aimedBlock, Utils.SZAGrinding) || !Utils.CheckSafezoneAction(aimedBlock.CubeGrid, Utils.SZAGrinding))
                            color = Color.DarkRed;
                        else
                            color = new Color(255, 200, 75);
                    }
                    else
                    {
                        if(!Utils.CheckSafezoneAction(aimedBlock, Utils.SZAWelding) || !Utils.CheckSafezoneAction(aimedBlock.CubeGrid, Utils.SZAWelding))
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

            MatrixD worldMatrix;
            BoundingBoxD localBB;
            GetBlockLocalBB(aimedBlock, ref LocalBBCache, out localBB, out worldMatrix);

            localBB.Inflate((grid.GridSizeEnum == MyCubeSize.Large ? 0.1 : 0.03));
            float lineWidth = (grid.GridSizeEnum == MyCubeSize.Large ? 0.02f : 0.016f);

            DrawSelection(ref worldMatrix, ref localBB, color, lineWidth);
            #endregion
        }

        public void GetBlockLocalBB(IMySlimBlock block, ref BoundingBoxD? cache, out BoundingBoxD localBB, out MatrixD worldMatrix)
        {
            MyCubeBlock fatBlock = block.FatBlock as MyCubeBlock;
            if(fatBlock != null)
            {
                worldMatrix = fatBlock.PositionComp.WorldMatrixRef;

                if(!cache.HasValue || Main.Tick % (Constants.TICKS_PER_SECOND / 4) == 0)
                {
                    BoundingBox localAABB = fatBlock.PositionComp.LocalAABB;
                    localBB = new BoundingBoxD(localAABB.Min, localAABB.Max);

                    #region Subpart localBB inclusion
                    if(fatBlock.Subparts != null)
                    {
                        MatrixD transformToBlockLocal = fatBlock.PositionComp.WorldMatrixInvScaled;

                        foreach(MyEntitySubpart s1 in fatBlock.Subparts.Values)
                        {
                            MyOrientedBoundingBoxD obbS1 = new MyOrientedBoundingBoxD(s1.PositionComp.LocalAABB, s1.PositionComp.WorldMatrixRef);
                            obbS1.GetCorners(Corners, 0);

                            for(int i = 0; i < Corners.Length; i++)
                            {
                                Vector3D corner = Corners[i];
                                localBB.Include(Vector3D.Transform(corner, transformToBlockLocal));
                            }

                            if(s1.Subparts != null)
                                foreach(MyEntitySubpart s2 in s1.Subparts.Values)
                                {
                                    MyOrientedBoundingBoxD obbS2 = new MyOrientedBoundingBoxD(s2.PositionComp.LocalAABB, s2.PositionComp.WorldMatrixRef);
                                    obbS2.GetCorners(Corners, 0);

                                    for(int i = 0; i < Corners.Length; i++)
                                    {
                                        Vector3D corner = Corners[i];
                                        localBB.Include(Vector3D.Transform(corner, transformToBlockLocal));
                                    }

                                    if(s2.Subparts != null)
                                        foreach(MyEntitySubpart s3 in s2.Subparts.Values)
                                        {
                                            MyOrientedBoundingBoxD obbS3 = new MyOrientedBoundingBoxD(s3.PositionComp.LocalAABB, s3.PositionComp.WorldMatrixRef);
                                            obbS3.GetCorners(Corners, 0);

                                            for(int i = 0; i < Corners.Length; i++)
                                            {
                                                Vector3D corner = Corners[i];
                                                localBB.Include(Vector3D.Transform(corner, transformToBlockLocal));
                                            }

                                            if(s3.Subparts != null)
                                                foreach(MyEntitySubpart s4 in s3.Subparts.Values)
                                                {
                                                    MyOrientedBoundingBoxD obbS4 = new MyOrientedBoundingBoxD(s4.PositionComp.LocalAABB, s4.PositionComp.WorldMatrixRef);
                                                    obbS4.GetCorners(Corners, 0);

                                                    for(int i = 0; i < Corners.Length; i++)
                                                    {
                                                        Vector3D corner = Corners[i];
                                                        localBB.Include(Vector3D.Transform(corner, transformToBlockLocal));
                                                    }
                                                }
                                        }
                                }
                        }
                    }
                    #endregion

                    cache = localBB;
                }
                else
                {
                    localBB = cache.Value;
                }
            }
            else
            {
                MyCubeGrid grid = (MyCubeGrid)block.CubeGrid;
                MyCubeBlockDefinition def = (MyCubeBlockDefinition)block.BlockDefinition;

                Matrix localMatrix;
                block.Orientation.GetMatrix(out localMatrix);
                localMatrix.Translation = new Vector3(block.Position) * grid.GridSize;
                worldMatrix = localMatrix * grid.WorldMatrix;

                Vector3 halfSize = def.Size * grid.GridSizeHalf;
                localBB = new BoundingBoxD(-halfSize, halfSize);
            }
        }

        public void DrawSelection(ref MatrixD worldMatrix, ref BoundingBoxD localBB, Color color, float lineWidth)
        {
            #region Selection lines
            const float LineLength = 2f; // directions are half-long

            Vector3D center = Vector3D.Transform((localBB.Min + localBB.Max) * 0.5, worldMatrix);
            Vector3D halfExtent = (localBB.Max - localBB.Min) * 0.5;
            Vector3D left = worldMatrix.Left * halfExtent.X;
            Vector3D up = worldMatrix.Up * halfExtent.Y;
            Vector3D back = worldMatrix.Backward * halfExtent.Z;

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
