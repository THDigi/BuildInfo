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
        const BlendType SelectionBlendType = BlendType.PostPP;

        bool eventHooked;

        Color? ColorCache = null;
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

            // TODO: check how this works/looks with ship welder/grinder once that feature is fixed in vanilla game
            if(Main.EquipmentMonitor.HandTool == null)
                return;

            var grid = (MyCubeGrid)aimedBlock.CubeGrid;
            IMyProjector projector = Main.EquipmentMonitor.AimedProjectedBy;

            if(projector != null && !Main.Config.SelectAllProjectedBlocks.Value)
                return;

            Color color;
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
                            case BuildCheckResult.NotConnected: color = Color.Yellow; break;

                            case BuildCheckResult.IntersectedWithGrid:
                            case BuildCheckResult.IntersectedWithSomethingElse:
                            case BuildCheckResult.NotWeldable: // projector doesn't allow block creation/welding
                                color = Color.DarkRed;
                                break;
                            default: color = new Color(255, 0, 255); break; // unknown state
                        }
                    }
                }
                else
                {
                    if(Main.EquipmentMonitor.IsAnyGrinder)
                    {
                        if(grid.Immune || !grid.Editable || !Utils.CheckSafezoneAction(aimedBlock, Utils.SZAGrinding))
                            color = Color.DarkRed;
                        else
                            color = new Color(255, 200, 75);
                    }
                    else
                    {
                        if(!Utils.CheckSafezoneAction(aimedBlock, Utils.SZAWelding))
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
            #endregion

            MatrixD worldMatrix;
            BoundingBoxD localBB;

            #region Compute box
            var fatBlock = aimedBlock.FatBlock as MyCubeBlock;
            if(fatBlock != null)
            {
                worldMatrix = fatBlock.PositionComp.WorldMatrixRef;

                if(!LocalBBCache.HasValue || Main.Tick % (Constants.TICKS_PER_SECOND / 4) == 0)
                {
                    var localAABB = fatBlock.PositionComp.LocalAABB;
                    localBB = new BoundingBoxD(localAABB.Min, localAABB.Max);

                    #region Subpart localBB inclusion
                    if(fatBlock.Subparts != null)
                    {
                        var transformToBlockLocal = fatBlock.PositionComp.WorldMatrixInvScaled;

                        foreach(var s1 in fatBlock.Subparts.Values)
                        {
                            var obbS1 = new MyOrientedBoundingBoxD(s1.PositionComp.LocalAABB, s1.PositionComp.WorldMatrixRef);
                            obbS1.GetCorners(Corners, 0);

                            for(int i = 0; i < Corners.Length; i++)
                            {
                                var corner = Corners[i];
                                localBB.Include(Vector3D.Transform(corner, transformToBlockLocal));
                            }

                            if(s1.Subparts != null)
                                foreach(var s2 in s1.Subparts.Values)
                                {
                                    var obbS2 = new MyOrientedBoundingBoxD(s2.PositionComp.LocalAABB, s2.PositionComp.WorldMatrixRef);
                                    obbS2.GetCorners(Corners, 0);

                                    for(int i = 0; i < Corners.Length; i++)
                                    {
                                        var corner = Corners[i];
                                        localBB.Include(Vector3D.Transform(corner, transformToBlockLocal));
                                    }

                                    if(s2.Subparts != null)
                                        foreach(var s3 in s2.Subparts.Values)
                                        {
                                            var obbS3 = new MyOrientedBoundingBoxD(s3.PositionComp.LocalAABB, s3.PositionComp.WorldMatrixRef);
                                            obbS3.GetCorners(Corners, 0);

                                            for(int i = 0; i < Corners.Length; i++)
                                            {
                                                var corner = Corners[i];
                                                localBB.Include(Vector3D.Transform(corner, transformToBlockLocal));
                                            }

                                            if(s3.Subparts != null)
                                                foreach(var s4 in s3.Subparts.Values)
                                                {
                                                    var obbS4 = new MyOrientedBoundingBoxD(s4.PositionComp.LocalAABB, s4.PositionComp.WorldMatrixRef);
                                                    obbS4.GetCorners(Corners, 0);

                                                    for(int i = 0; i < Corners.Length; i++)
                                                    {
                                                        var corner = Corners[i];
                                                        localBB.Include(Vector3D.Transform(corner, transformToBlockLocal));
                                                    }
                                                }
                                        }
                                }
                        }
                    }
                    #endregion

                    LocalBBCache = localBB;
                }
                else
                {
                    localBB = LocalBBCache.Value;
                }
            }
            else
            {
                Matrix localMatrix;
                aimedBlock.Orientation.GetMatrix(out localMatrix);
                localMatrix.Translation = new Vector3(aimedBlock.Position) * grid.GridSize;
                worldMatrix = localMatrix * grid.WorldMatrix;

                var halfSize = def.Size * grid.GridSizeHalf;
                localBB = new BoundingBoxD(-halfSize, halfSize);
            }
            #endregion

            localBB.Inflate((grid.GridSizeEnum == MyCubeSize.Large ? 0.1 : 0.03));
            float lineWidth = (grid.GridSizeEnum == MyCubeSize.Large ? 0.02f : 0.016f);

            #region Selection lines
            const float LineLength = 2f; // directions are half-long

            Vector3D center = Vector3D.Transform((localBB.Min + localBB.Max) * 0.5, worldMatrix);
            Vector3D halfExtent = (localBB.Max - localBB.Min) * 0.5;
            Vector3D left = worldMatrix.Left * halfExtent.X;
            Vector3D up = worldMatrix.Up * halfExtent.Y;
            Vector3D back = worldMatrix.Backward * halfExtent.Z;

            Vector3D top = center + up;
            Vector3D bottom = center - up;

            var cornerTop1 = top + left + back;
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop1, -left, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop1, -up, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop1, -back, LineLength, lineWidth, SelectionBlendType);

            var cornerTop2 = top - left - back;
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop2, left, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop2, -up, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerTop2, back, LineLength, lineWidth, SelectionBlendType);

            var cornerBottom1 = bottom + left - back;
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom1, -left, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom1, up, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom1, back, LineLength, lineWidth, SelectionBlendType);

            var cornerBottom2 = bottom - left + back;
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom2, left, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom2, up, LineLength, lineWidth, SelectionBlendType);
            MyTransparentGeometry.AddLineBillboard(SelectionLineMaterial, color, cornerBottom2, -back, LineLength, lineWidth, SelectionBlendType);
            #endregion

            #region See-through-walls lines
            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;

            const float DepthRatio = 0.01f;
            lineWidth *= DepthRatio * 0.5f; // half as thin
            color *= 0.5f; // half opacity too

            center = camMatrix.Translation + ((center - camMatrix.Translation) * DepthRatio);

            halfExtent = (localBB.Max - localBB.Min) * 0.5 * DepthRatio;
            left = worldMatrix.Left * halfExtent.X;
            up = worldMatrix.Up * halfExtent.Y;
            back = worldMatrix.Backward * halfExtent.Z;

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
