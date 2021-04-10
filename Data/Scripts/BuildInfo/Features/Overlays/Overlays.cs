using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using WeaponCore.Api;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features.Overlays
{
    public class Overlays : ModComponent
    {
        public int DrawOverlay = 0;
        private OverlayCall SelectedOverlayCall;
        private IMyHudNotification OverlayNotification;

        private bool AnyLabelShown;
        private readonly LabelData[] Labels;

        private bool BlockFunctionalForPressure;

        private bool DoorAirtightBlink = true;
        private int DoorAirtightBlinkTick = 0;

        class LabelData
        {
            public HudAPIv2.SpaceMessage Text;
            public HudAPIv2.SpaceMessage Shadow;
            public float UnderlineLength = -1;
        }

        public delegate void OverlayCall(MyCubeBlockDefinition def, MatrixD drawMatrix);
        public readonly Dictionary<MyObjectBuilderType, OverlayCall> drawLookup
                  = new Dictionary<MyObjectBuilderType, OverlayCall>(MyObjectBuilderType.Comparer);

        #region Constants
        private const BlendTypeEnum OVERLAY_BLEND_TYPE = BlendTypeEnum.PostPP;
        public readonly MyStringId OVERLAY_SQUARE_MATERIAL = MyStringId.GetOrCompute("BuildInfo_Square");
        public readonly MyStringId OVERLAY_LASER_MATERIAL = MyStringId.GetOrCompute("BuildInfo_Laser");
        public readonly MyStringId OVERLAY_DOT_MATERIAL = MyStringId.GetOrCompute("WhiteDot");
        public readonly MyStringId OVERLAY_GRADIENT_MATERIAL = MyStringId.GetOrCompute("BuildInfo_TransparentGradient");

        private const BlendTypeEnum MOUNTPOINT_BLEND_TYPE = BlendTypeEnum.SDR;
        private const double MOUNTPOINT_THICKNESS = 0.05;
        private const float MOUNTPOINT_ALPHA = 0.65f;
        private Color MOUNTPOINT_COLOR = new Color(255, 255, 0) * MOUNTPOINT_ALPHA;
        private Color MOUNTPOINT_MASKED_COLOR = new Color(255, 55, 0) * MOUNTPOINT_ALPHA;
        private Color MOUNTPOINT_DEFAULT_COLOR = new Color(0, 55, 255) * MOUNTPOINT_ALPHA;
        private Color AIRTIGHT_COLOR = new Color(0, 155, 255) * MOUNTPOINT_ALPHA;
        private Color AIRTIGHT_TOGGLE_COLOR = new Color(0, 255, 155) * MOUNTPOINT_ALPHA;

        private const double LABEL_TEXT_SCALE = 0.24;
        private readonly Vector2D LABEL_OFFSET = new Vector2D(0.1, 0.1);
        private const BlendTypeEnum LABEL_BLEND_TYPE = BlendTypeEnum.PostPP;
        private const BlendTypeEnum LABEL_SHADOW_BLEND_TYPE = BlendTypeEnum.PostPP;
        private readonly Color LABEL_SHADOW_COLOR = Color.Black * 0.9f;
        private readonly Vector2D LABEL_SHADOW_OFFSET = new Vector2D(0.01, -0.01);

        private BoundingBoxD unitBB = new BoundingBoxD(Vector3D.One / -2d, Vector3D.One / 2d);

        public readonly string[] OverlayNames = new string[]
        {
            "OFF",
            "Airtightness",
            "Mounting",
        };

        private readonly string[] AxisLabels = new string[]
        {
            "Forward",
            "Right",
            "Up",
        };

        private enum TextAPIMsgIds
        {
            AxisZ, // NOTE these 3 must remain the first 3, because AXIS_LABELS uses their integer values as indexes
            AxisX,
            AxisY,
            // its label is not constnt so id can be shared, use with the label sb
            DynamicLabel,

            // the rest have static messages that are assigned on creation
            ModelOffset,
            SensorRadius,
            MineRadius,
            CarveRadius,
            PitchLimit,
            YawLimit,
            AirtightWhenClosed,
            ThrustDamage,
            MagnetizedArea,
            CollectionArea,
            TerrainClearence,
            SideClearence,
            OptimalClearence,
            Laser,
        }

        public readonly Vector3[] DIRECTIONS = new Vector3[] // NOTE: order is important, corresponds to +X, -X, +Y, -Y, +Z, -Z
        {
            Vector3.Right,
            Vector3.Left,
            Vector3.Up,
            Vector3.Down,
            Vector3.Backward,
            Vector3.Forward,
        };

        private readonly MyCubeBlockDefinition.MountPoint[] BLANK_MOUNTPOINTS = new MyCubeBlockDefinition.MountPoint[0];
        #endregion Constants

        public Overlays(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.NONE;

            int count = Enum.GetValues(typeof(TextAPIMsgIds)).Length;
            Labels = new LabelData[count];
        }

        public override void RegisterComponent()
        {
            InitLookups();

            Main.GameConfig.HudStateChanged += GameConfig_HudStateChanged;
            Main.EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
            Main.EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;
        }

        public override void UnregisterComponent()
        {
            Main.GameConfig.HudStateChanged -= GameConfig_HudStateChanged;
            Main.EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
            Main.EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;
        }

        private void GameConfig_HudStateChanged(HudState prevState, HudState newState)
        {
            if(newState == HudState.OFF)
                HideLabels();
        }

        private void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        {
            HideLabels();
        }

        private void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            if(Main.LockOverlay.LockedOnBlock != null)
                return;

            SelectedOverlayCall = null;

            if(def != null)
                SelectedOverlayCall = drawLookup.GetValueOrDefault(def.Id.TypeId, null);

            HideLabels();
        }

        private void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            if(shipController != null && Main.EquipmentMonitor.IsBuildTool && DrawOverlay > 0)
            {
                const BlendTypeEnum BLEND_TYPE = BlendTypeEnum.SDR;
                const float REACH_DISTANCE = Hardcoded.ShipTool_ReachDistance;
                var color = new Vector4(2f, 0, 0, 0.1f); // above 1 color creates bloom
                var shipCtrlDef = (MyShipControllerDefinition)shipController.SlimBlock.BlockDefinition;

                var m = shipController.WorldMatrix;
                m.Translation = Vector3D.Transform(shipCtrlDef.RaycastOffset, m);

                MyTransparentGeometry.AddLineBillboard(OVERLAY_LASER_MATERIAL, color, m.Translation, m.Forward, REACH_DISTANCE, 0.005f, blendType: BLEND_TYPE);
                MyTransparentGeometry.AddPointBillboard(OVERLAY_DOT_MATERIAL, color, m.Translation + m.Forward * REACH_DISTANCE, 0.015f, 0f, blendType: BLEND_TYPE);
            }
        }

        public void CycleOverlayMode(bool showNotification = true)
        {
            if(++DrawOverlay >= OverlayNames.Length)
                DrawOverlay = 0;

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, DrawOverlay > 0);
            HideLabels();

            if(showNotification)
            {
                if(OverlayNotification == null)
                    OverlayNotification = MyAPIGateway.Utilities.CreateNotification("", 2000, FontsHandler.WhiteSh);

                OverlayNotification.Hide(); // required since SE v1.194
                OverlayNotification.Text = "Overlays: " + OverlayNames[DrawOverlay];
                OverlayNotification.Show();
            }
        }

        public void SetOverlayCallFor(MyDefinitionId? defId)
        {
            if(defId.HasValue)
                SelectedOverlayCall = drawLookup.GetValueOrDefault(defId.Value.TypeId, null);
            else
                SelectedOverlayCall = null;
        }

        private void InitLookups()
        {
            Add(typeof(MyObjectBuilder_ShipWelder), DrawOverlay_ShipTool);
            Add(typeof(MyObjectBuilder_ShipGrinder), DrawOverlay_ShipTool);

            Add(typeof(MyObjectBuilder_Drill), DrawOverlay_Drill);

            Add(typeof(MyObjectBuilder_ConveyorSorter), DrawOverlay_ConveyorSorter); // also used by WeaponCore

            Add(typeof(MyObjectBuilder_SmallGatlingGun), DrawOverlay_Weapons);
            Add(typeof(MyObjectBuilder_SmallMissileLauncher), DrawOverlay_Weapons);
            Add(typeof(MyObjectBuilder_SmallMissileLauncherReload), DrawOverlay_Weapons);
            Add(typeof(MyObjectBuilder_LargeGatlingTurret), DrawOverlay_Weapons);
            Add(typeof(MyObjectBuilder_LargeMissileTurret), DrawOverlay_Weapons);
            Add(typeof(MyObjectBuilder_InteriorTurret), DrawOverlay_Weapons);

            Add(typeof(MyObjectBuilder_LaserAntenna), DrawOverlay_LaserAntenna);

            Add(typeof(MyObjectBuilder_Door), DrawOverlay_Doors);
            Add(typeof(MyObjectBuilder_AdvancedDoor), DrawOverlay_Doors);
            Add(typeof(MyObjectBuilder_AirtightDoorGeneric), DrawOverlay_Doors);
            Add(typeof(MyObjectBuilder_AirtightHangarDoor), DrawOverlay_Doors);
            Add(typeof(MyObjectBuilder_AirtightSlideDoor), DrawOverlay_Doors);

            Add(typeof(MyObjectBuilder_Thrust), DrawOverlay_Thruster);

            Add(typeof(MyObjectBuilder_LandingGear), DrawOverlay_LandingGear);

            Add(typeof(MyObjectBuilder_Collector), DrawOverlay_Collector);

            Add(typeof(MyObjectBuilder_WindTurbine), DrawOverlay_WindTurbine);
        }

        private void Add(MyObjectBuilderType blockType, OverlayCall call)
        {
            drawLookup.Add(blockType, call);
        }

        public override void UpdateDraw()
        {
            if(DrawOverlay == 0 || (Main.LockOverlay.LockedOnBlock == null && Main.EquipmentMonitor.BlockDef == null) || (Main.GameConfig.HudState == HudState.OFF && !Main.Config.OverlaysAlwaysVisible.Value))
                return;

            var def = Main.EquipmentMonitor.BlockDef;
            var aimedBlock = Main.EquipmentMonitor.AimedBlock;
            var cellSize = Main.EquipmentMonitor.BlockGridSize;

            if(Main.LockOverlay.LockedOnBlock != null && !Main.LockOverlay.UpdateLockedOnBlock(ref aimedBlock, ref def, ref cellSize))
                return;

            try
            {
                #region DrawMatrix and other needed data
                var drawMatrix = MatrixD.Identity;

                if(Main.LockOverlay.LockedOnBlock == null && Main.EquipmentMonitor.IsCubeBuilder)
                {
                    if(MyAPIGateway.Session.IsCameraUserControlledSpectator && !Utils.CreativeToolsEnabled)
                        return;

                    var box = MyCubeBuilder.Static.GetBuildBoundingBox();
                    drawMatrix = MatrixD.CreateFromQuaternion(box.Orientation);

                    if(MyCubeBuilder.Static.DynamicMode && MyCubeBuilder.Static.HitInfo.HasValue)
                    {
                        var hitEnt = MyCubeBuilder.Static.HitInfo.Value.GetHitEnt();
                        if(hitEnt != null && hitEnt is IMyVoxelBase)
                            drawMatrix.Translation = MyCubeBuilder.Static.HitInfo.Value.GetHitPos(); // required for position to be accurate when aiming at a planet
                        else
                            drawMatrix.Translation = MyCubeBuilder.Static.FreePlacementTarget; // required for the position to be accurate when the block is not aimed at anything
                    }
                    else
                    {
                        //drawMatrix.Translation = box.Center;

                        // HACK: potential fix for jittery overlays when aiming at a grid.
                        Vector3D addPosition;
                        MyCubeBuilder.Static.GetAddPosition(out addPosition);
                        drawMatrix.Translation = addPosition;
                    }
                }
                else // using welder/grinder or lockedOnBlock.
                {
                    Matrix m;
                    Vector3D center;
                    aimedBlock.Orientation.GetMatrix(out m);
                    aimedBlock.ComputeWorldCenter(out center);

                    drawMatrix = m * aimedBlock.CubeGrid.WorldMatrix;
                    drawMatrix.Translation = center;
                }
                #endregion DrawMatrix and other needed data

                //if(aimedBlock != null)
                //{
                //    var neighbours = aimedBlock.Neighbours; // DEBUG TODO: change to non-alloc if implemented properly
                //    MyAPIGateway.Utilities.ShowNotification($"neighbours = {neighbours.Count}", 16);
                //    foreach(var n in neighbours)
                //    {
                //        Matrix m;
                //        Vector3D center;
                //        n.Orientation.GetMatrix(out m);
                //        n.ComputeWorldCenter(out center);

                //        var wm = m * n.CubeGrid.WorldMatrix;
                //        wm.Translation = center;

                //        var nd = (MyCubeBlockDefinition)n.BlockDefinition;
                //        var halfExtents = nd.Size * (cellSize * 0.5);
                //        var localBB = new BoundingBoxD(-halfExtents, halfExtents).Inflate(MOUNTPOINT_THICKNESS * 0.5);

                //        var color = Color.Lime;

                //        MySimpleObjectDraw.DrawTransparentBox(ref wm, ref localBB, ref color, MySimpleObjectRasterizer.Wireframe, 4, 0.001f, lineMaterial: MyStringId.GetOrCompute("Square"), blendType: BlendTypeEnum.PostPP);
                //    }
                //}

                if(Main.Config.InternalInfo.Value && def.ModelOffset.LengthSquared() > 0)
                {
                    const float OffsetLineThickness = 0.005f;
                    const float OffsetPointThickness = 0.05f;
                    Color color = new Color(255, 0, 255);

                    var start = drawMatrix.Translation;
                    var dir = Vector3D.TransformNormal(def.ModelOffset, drawMatrix);

                    var cm = MyAPIGateway.Session.Camera.WorldMatrix;
                    var offset = cm.Right * LABEL_SHADOW_OFFSET.X + cm.Up * LABEL_SHADOW_OFFSET.Y;

                    MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, LABEL_SHADOW_COLOR, start + offset, dir, 1f, OffsetLineThickness, LABEL_SHADOW_BLEND_TYPE);
                    MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, color, start, dir, 1f, OffsetLineThickness, blendType: OVERLAY_BLEND_TYPE);

                    DrawLineLabel(TextAPIMsgIds.ModelOffset, drawMatrix.Translation + dir, dir, color, "Center", 0);

                    MyTransparentGeometry.AddPointBillboard(OVERLAY_DOT_MATERIAL, LABEL_SHADOW_COLOR, start + dir + offset, OffsetPointThickness, 0, blendType: OVERLAY_BLEND_TYPE);
                    MyTransparentGeometry.AddPointBillboard(OVERLAY_DOT_MATERIAL, color, start + dir, OffsetPointThickness, 0, blendType: OVERLAY_BLEND_TYPE);
                }

                #region Draw mount points
                if(Main.TextAPI.IsEnabled)
                {
                    DrawMountPointAxisText(def, cellSize, ref drawMatrix);
                }
                else
                {
                    // re-assigning mount points temporarily to prevent the original mountpoint wireframe from being drawn while keeping the axis information
                    var mp = def.MountPoints;
                    def.MountPoints = BLANK_MOUNTPOINTS;
                    MyCubeBuilder.DrawMountPoints(cellSize, def, ref drawMatrix);
                    def.MountPoints = mp;
                }

                BlockFunctionalForPressure = true;

                // HACK condition matching the condition in MyGridGasSystem.IsAirtightFromDefinition()
                if(Main.EquipmentMonitor.AimedProjectedBy == null && aimedBlock != null && def.BuildProgressModels != null && def.BuildProgressModels.Length > 0)
                {
                    var progressModel = def.BuildProgressModels[def.BuildProgressModels.Length - 1];

                    if(aimedBlock.BuildLevelRatio < progressModel.BuildRatioUpperBound)
                        BlockFunctionalForPressure = false;
                }

                // draw custom mount point styling
                {
                    var center = def.Center;
                    var mainMatrix = MatrixD.CreateTranslation((center - (def.Size * 0.5f)) * cellSize) * drawMatrix;
                    var mountPoints = def.GetBuildProgressModelMountPoints(1f);
                    bool drawLabel = CanDrawLabel();

                    if(DrawOverlay == 1 && BlockFunctionalForPressure)
                    {
                        // TODO: have a note saying that blocks that aren't fully built are always not airtight? (blockFunctionalForPressure)

                        if(def.IsAirTight.HasValue)
                        {
                            if(def.IsAirTight.Value)
                            {
                                var halfExtents = def.Size * (cellSize * 0.5);
                                var localBB = new BoundingBoxD(-halfExtents, halfExtents).Inflate(MOUNTPOINT_THICKNESS * 0.5);
                                MySimpleObjectDraw.DrawTransparentBox(ref drawMatrix, ref localBB, ref AIRTIGHT_COLOR, MySimpleObjectRasterizer.Solid, 1, lineWidth: 0.01f, lineMaterial: OVERLAY_SQUARE_MATERIAL, faceMaterial: OVERLAY_SQUARE_MATERIAL, blendType: MOUNTPOINT_BLEND_TYPE);
                            }
                        }
                        else if(mountPoints != null)
                        {
                            var half = Vector3D.One * -(0.5f * cellSize);
                            var corner = (Vector3D)def.Size * -(0.5f * cellSize);
                            var transformMatrix = MatrixD.CreateTranslation(corner - half) * drawMatrix;

                            foreach(var kv in def.IsCubePressurized) // precomputed: [position][normal] = airtight type
                            {
                                foreach(var kv2 in kv.Value)
                                {
                                    if(kv2.Value != MyCubeBlockDefinition.MyCubePressurizationMark.PressurizedAlways) // pos+normal not always airtight
                                        continue;

                                    var pos = Vector3D.Transform((Vector3D)(kv.Key * cellSize), transformMatrix);
                                    var dirForward = Vector3.TransformNormal(kv2.Key, drawMatrix);
                                    var dirIndex = (int)Base6Directions.GetDirection(kv2.Key);
                                    var dirUp = Vector3.TransformNormal(DIRECTIONS[((dirIndex + 2) % 6)], drawMatrix);

                                    var m = MatrixD.Identity;
                                    m.Translation = pos + dirForward * (cellSize * 0.5f);
                                    m.Forward = dirForward;
                                    m.Backward = -dirForward;
                                    m.Left = Vector3D.Cross(dirForward, dirUp);
                                    m.Right = -m.Left;
                                    m.Up = dirUp;
                                    m.Down = -dirUp;
                                    var scale = new Vector3D(cellSize, cellSize, MOUNTPOINT_THICKNESS);
                                    MatrixD.Rescale(ref m, ref scale);

                                    MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_COLOR, ref AIRTIGHT_COLOR, MySimpleObjectRasterizer.Solid, 1, lineWidth: 0.01f, lineMaterial: OVERLAY_SQUARE_MATERIAL, faceMaterial: OVERLAY_SQUARE_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                                }
                            }
                        }
                    }
                    else if(DrawOverlay == 2 && mountPoints != null)
                    {
                        for(int i = 0; i < mountPoints.Length; i++)
                        {
                            var mountPoint = mountPoints[i];

                            if(!mountPoint.Enabled)
                                continue; // ignore all disabled mount points as airtight ones are rendered separate

                            var startLocal = mountPoint.Start - center;
                            var endLocal = mountPoint.End - center;

                            var bb = new BoundingBoxD(Vector3.Min(startLocal, endLocal) * cellSize, Vector3.Max(startLocal, endLocal) * cellSize);
                            var obb = new MyOrientedBoundingBoxD(bb, mainMatrix);

                            var normalAxis = Base6Directions.GetAxis(Base6Directions.GetDirection(ref mountPoint.Normal));

                            var m = MatrixD.CreateFromQuaternion(obb.Orientation);
                            m.Right *= Math.Max(obb.HalfExtent.X * 2, (normalAxis == Base6Directions.Axis.LeftRight ? MOUNTPOINT_THICKNESS : 0));
                            m.Up *= Math.Max(obb.HalfExtent.Y * 2, (normalAxis == Base6Directions.Axis.UpDown ? MOUNTPOINT_THICKNESS : 0));
                            m.Forward *= Math.Max(obb.HalfExtent.Z * 2, (normalAxis == Base6Directions.Axis.ForwardBackward ? MOUNTPOINT_THICKNESS : 0));
                            m.Translation = obb.Center;

                            Color colorFace = MOUNTPOINT_COLOR;
                            if(mountPoint.ExclusionMask != 0 || mountPoint.PropertiesMask != 0)
                                colorFace = MOUNTPOINT_MASKED_COLOR;

                            // TODO: a way to visually tell which mounts with mask can be used on which?

                            MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: OVERLAY_SQUARE_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);

                            if(mountPoint.Default)
                                MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref MOUNTPOINT_DEFAULT_COLOR, MySimpleObjectRasterizer.Wireframe, 8, lineWidth: 0.005f, lineMaterial: OVERLAY_SQUARE_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                        }
                    }
                }
                #endregion Draw mount points

                // draw per-block overlays
                SelectedOverlayCall?.Invoke(def, drawMatrix);

                // TODO: real time neighbour airtight display?
#if false
            {
                var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;

                if(def != null && MyCubeBuilder.Static.IsActivated)
                {
                    var grid = MyCubeBuilder.Static.FindClosestGrid();

                    Vector3D worldAdd;
                    MyCubeBuilder.Static.GetAddPosition(out worldAdd);

                    var bb = MyCubeBuilder.Static.GetBuildBoundingBox();
                    var matrix = Matrix.CreateFromQuaternion(bb.Orientation);

                    var startPos = grid.WorldToGridInteger(worldAdd);

                    for(int i = 0; i < Base6Directions.IntDirections.Length; ++i)
                    {
                        var endPos = startPos + Base6Directions.IntDirections[i];
                        bool airtight = def.IsAirTight || Pressurization.TestPressurize(startPos, endPos - startPos, matrix, def);

                        //if(!airtight)
                        //{
                        //    IMySlimBlock b2 = grid.GetCubeBlock(startPos);
                        //
                        //    if(b2 != null)
                        //    {
                        //        var def2 = (MyCubeBlockDefinition)b2.BlockDefinition;
                        //        airtight = def2.IsAirTight || Pressurization.IsPressurized(b2, endPos, startPos - endPos);
                        //    }
                        //}

                        MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), (airtight ? Color.Green : Color.Red), worldAdd, Vector3D.TransformNormal(Base6Directions.IntDirections[i], matrix), 1f, 0.1f);

                        //MyAPIGateway.Utilities.ShowNotification($"{i}. airtight={airtight}", 16); // DEBUG print
                    }
                }
            }
#endif
            }
            catch(Exception e)
            {
                Log.Error($"Error on overlay draw; heldDefId={def?.Id.ToString()}; aimedDefId={aimedBlock?.BlockDefinition?.Id.ToString()} - {e.Message}\n{e.StackTrace}");
            }
        }

        public void HideLabels()
        {
            if(!AnyLabelShown)
                return;

            AnyLabelShown = false;

            //for(int i = 0; i < Labels.Length; ++i)
            //{
            //    var label = Labels[i];
            //
            //    if(label != null && label.Text != null)
            //    {
            //        label.Text.Visible = false;
            //        label.Shadow.Visible = false;
            //    }
            //}
        }

        #region Block-specific overlays
        private void DrawOverlay_Doors(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            if(DrawOverlay != 1)
                return;

            if(!BlockFunctionalForPressure)
            {
                HideLabels();
                return;
            }

            IMySlimBlock block = (Main.LockOverlay.LockedOnBlock ?? Main.EquipmentMonitor.AimedBlock);
            float cellSize = (block == null ? Main.EquipmentMonitor.BlockGridSize : block.CubeGrid.GridSize);

            //if(block != null)
            //{
            //    doorAirtightBlink = true;
            //}
            //else if(!MyParticlesManager.Paused && ++doorAirtightBlinkTick >= 60)
            //{
            //    doorAirtightBlinkTick = 0;
            //    doorAirtightBlink = !doorAirtightBlink;
            //}

            var cubeSize = def.Size * (cellSize * 0.5f);
            bool drawLabel = CanDrawLabel();

            //if(!drawLabel && !DoorAirtightBlink)
            //    return;

            bool fullyClosed = true;

            if(block != null)
            {
                var door = block.FatBlock as IMyDoor;
                if(door != null)
                    fullyClosed = (door.Status == Sandbox.ModAPI.Ingame.DoorStatus.Closed);
            }

            var isAirTight = Pressurization.IsAirtightFromDefinition(def, 1f);
            if(isAirTight == AirTightMode.SEALED)
                return; // if block is entirely sealed anyway, don't bother with door specifics

            #region Draw sealed sides
            for(int i = 0; i < 6; ++i)
            {
                var normal = DIRECTIONS[i];
                var normalI = (Vector3I)normal;

                if(Pressurization.IsDoorAirtightInternal(def, ref normalI, true))
                {
                    var dirForward = Vector3D.TransformNormal(normal, drawMatrix);
                    var dirLeft = Vector3D.TransformNormal(DIRECTIONS[((i + 4) % 6)], drawMatrix);
                    var dirUp = Vector3D.TransformNormal(DIRECTIONS[((i + 2) % 6)], drawMatrix);

                    var pos = drawMatrix.Translation + dirForward * cubeSize.GetDim((i % 6) / 2);
                    float width = cubeSize.GetDim(((i + 4) % 6) / 2);
                    float height = cubeSize.GetDim(((i + 2) % 6) / 2);

                    if(DoorAirtightBlink)
                    {
                        var m = MatrixD.CreateWorld(pos, dirForward, dirUp);
                        m.Right *= width * 2;
                        m.Up *= height * 2;
                        m.Forward *= MOUNTPOINT_THICKNESS;

                        if(fullyClosed)
                            MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_TOGGLE_COLOR, ref AIRTIGHT_TOGGLE_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: OVERLAY_SQUARE_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                        else
                            MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_TOGGLE_COLOR, ref AIRTIGHT_TOGGLE_COLOR, MySimpleObjectRasterizer.Wireframe, 4, lineWidth: 0.01f, lineMaterial: OVERLAY_LASER_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                    }

                    if(drawLabel) // only label the first one
                    {
                        drawLabel = false;

                        var labelPos = pos + dirLeft * width + dirUp * height;
                        DrawLineLabel(TextAPIMsgIds.AirtightWhenClosed, labelPos, dirLeft, AIRTIGHT_TOGGLE_COLOR, message: "Airtight when closed");

                        if(!DoorAirtightBlink) // no need to iterate further if no faces need to be rendered
                            break;
                    }
                }
            }
            #endregion

            #region Find door-toggled mountpoints
            var mountPoints = def.GetBuildProgressModelMountPoints(1f);
            if(mountPoints != null)
            {
                var half = Vector3D.One * -(0.5f * cellSize);
                var corner = (Vector3D)def.Size * -(0.5f * cellSize);
                var transformMatrix = MatrixD.CreateTranslation(corner - half) * drawMatrix;

                foreach(var kv in def.IsCubePressurized) // precomputed: [position][normal] = airtight type
                {
                    foreach(var kv2 in kv.Value)
                    {
                        // only look for cell sides that are pressurized when doors are closed
                        if(kv2.Value != MyCubeBlockDefinition.MyCubePressurizationMark.PressurizedClosed)
                            continue;

                        var pos = Vector3D.Transform((Vector3D)(kv.Key * cellSize), transformMatrix);
                        var dirForward = Vector3.TransformNormal(kv2.Key, drawMatrix);
                        var dirIndex = (int)Base6Directions.GetDirection(kv2.Key);
                        var dirUp = Vector3.TransformNormal(DIRECTIONS[((dirIndex + 2) % 6)], drawMatrix);

                        if(DoorAirtightBlink)
                        {
                            var m = MatrixD.Identity;
                            m.Translation = pos + dirForward * (cellSize * 0.5f);
                            m.Forward = dirForward;
                            m.Backward = -dirForward;
                            m.Left = Vector3D.Cross(dirForward, dirUp);
                            m.Right = -m.Left;
                            m.Up = dirUp;
                            m.Down = -dirUp;
                            var scale = new Vector3D(cellSize, cellSize, MOUNTPOINT_THICKNESS);
                            MatrixD.Rescale(ref m, ref scale);

                            if(fullyClosed)
                                MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_TOGGLE_COLOR, ref AIRTIGHT_TOGGLE_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: OVERLAY_SQUARE_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                            else
                                MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_TOGGLE_COLOR, ref AIRTIGHT_TOGGLE_COLOR, MySimpleObjectRasterizer.Wireframe, 4, lineWidth: 0.01f, lineMaterial: OVERLAY_LASER_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                        }

                        if(drawLabel) // only label the first one
                        {
                            drawLabel = false;

                            var dirLeft = Vector3D.TransformNormal(DIRECTIONS[((dirIndex + 4) % 6)], drawMatrix);
                            float width = cubeSize.GetDim(((dirIndex + 4) % 6) / 2);
                            float height = cubeSize.GetDim(((dirIndex + 2) % 6) / 2);

                            var labelPos = pos + dirLeft * width + dirUp * height;
                            DrawLineLabel(TextAPIMsgIds.AirtightWhenClosed, labelPos, dirLeft, AIRTIGHT_TOGGLE_COLOR, message: "Airtight when closed");

                            if(!DoorAirtightBlink) // no need to iterate further if no faces need to be rendered
                                break;
                        }
                    }
                }
            }
            #endregion

            if(drawLabel) // no label was rendered since it would've set itself false by now
            {
                HideLabels();
            }
        }

        private void DrawOverlay_Weapons(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            WcApiDef.WeaponDefinition wcDef;
            if(Main.WeaponCoreAPIHandler.Weapons.TryGetValue(def.Id, out wcDef))
            {
                DrawOverlay_WeaponCoreWeapon(def, wcDef, drawMatrix);
                return;
            }

            var data = BData_Base.TryGetDataCached<BData_Weapon>(def);
            if(data == null)
                return;

            var weaponBlockDef = def as MyWeaponBlockDefinition;
            MyWeaponDefinition weaponDef;
            if(weaponBlockDef == null || !MyDefinitionManager.Static.TryGetWeaponDefinition(weaponBlockDef.WeaponDefinitionId, out weaponDef))
                return;

            #region Accuracy cone
            MyAmmoDefinition ammo = null;

            IMySlimBlock slimBlock = (Main.LockOverlay.LockedOnBlock ?? Main.EquipmentMonitor.AimedBlock);
            var weaponBlock = slimBlock?.FatBlock as IMyGunObject<MyGunBase>;

            if(weaponBlock != null)
                ammo = weaponBlock.GunBase.CurrentAmmoDefinition;

            if(ammo == null)
            {
                var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(weaponDef.AmmoMagazinesId[0]);
                ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
            }

            float height = ammo.MaxTrajectory;

            MatrixD accMatrix;
            if(weaponBlock != null)
                accMatrix = weaponBlock.GunBase.GetMuzzleWorldMatrix();
            else
                accMatrix = data.muzzleLocalMatrix * drawMatrix;

            const float PointRadius = 0.025f;
            const float AccLineThick = 0.01f;
            const int ConeWireDivideRatio = 36;
            var accColor = new Color(255, 155, 0);

            MyTransparentGeometry.AddPointBillboard(OVERLAY_DOT_MATERIAL, accColor, accMatrix.Translation, PointRadius, 0, blendType: OVERLAY_BLEND_TYPE); // this is drawn always on top on purpose

            if(weaponDef.DeviateShotAngle > 0)
            {
                float tanShotAngle = (float)Math.Tan(weaponDef.DeviateShotAngle);
                float accuracyAtMaxRange = tanShotAngle * height;
                Utils.DrawTransparentCone(ref accMatrix, accuracyAtMaxRange, height, ref accColor, MySimpleObjectRasterizer.Solid, ConeWireDivideRatio, lineThickness: AccLineThick, material: OVERLAY_SQUARE_MATERIAL, blendType: OVERLAY_BLEND_TYPE);

                //const int circleWireDivideRatio = 20;
                //var accuracyAt100m = tanShotAngle * (100 * 2);
                //var color100m = Color.Green.ToVector4();
                //var circleMatrix = MatrixD.CreateWorld(coneMatrix.Translation + coneMatrix.Forward * 3 + coneMatrix.Left * 3, coneMatrix.Down, coneMatrix.Forward);
                //MySimpleObjectDraw.DrawTransparentCylinder(ref circleMatrix, accuracyAt100m, accuracyAt100m, 0.1f, ref color100m, true, circleWireDivideRatio, 0.05f, MATERIAL_SQUARE);
            }
            else
            {
                MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, accColor, accMatrix.Translation, accMatrix.Forward, height, AccLineThick, blendType: OVERLAY_BLEND_TYPE);
            }

            bool canDrawLabel = CanDrawLabel();
            if(canDrawLabel)
            {
                var labelDir = accMatrix.Up;
                var labelLineStart = accMatrix.Translation + accMatrix.Forward * 3;
                label.Clear().Append("Accuracy cone - ").Append(height).Append(" m");
                DrawLineLabel(TextAPIMsgIds.DynamicLabel, labelLineStart, labelDir, accColor);
            }
            #endregion Accuracy cone

            #region Turret pitch/yaw limits
            var turretDef = def as MyLargeTurretBaseDefinition;
            var turretData = data as BData_WeaponTurret;
            bool isTurret = (turretDef != null && turretData != null);
            if(isTurret)
            {
                float cellSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize); // TODO: cache

                const int LineEveryDegrees = 15;
                const float lineThick = 0.03f;
                float radius = (def.Size.AbsMin() + 1) * cellSize;

                int minPitch = turretDef.MinElevationDegrees; // this one is actually not capped in game for whatever reason
                int maxPitch = Math.Min(turretDef.MaxElevationDegrees, 90); // can't pitch up more than 90deg

                int minYaw = turretDef.MinAzimuthDegrees;
                int maxYaw = turretDef.MaxAzimuthDegrees;

                {
                    var colorPitch = (Color.Red * 0.3f).ToVector4();
                    var colorPitchLine = Color.Red.ToVector4();

                    MatrixD pitchMatrix;
                    if(weaponBlock != null)
                    {
                        pitchMatrix = weaponBlock.GunBase.GetMuzzleWorldMatrix();
                        pitchMatrix.Translation = drawMatrix.Translation;
                    }
                    else
                    {
                        pitchMatrix = drawMatrix;
                    }

                    // only yaw rotation
                    var m = MatrixD.CreateWorld(drawMatrix.Translation, Vector3D.Cross(pitchMatrix.Left, drawMatrix.Up), drawMatrix.Up);
                    Vector3D rotationPivot = Vector3D.Transform(turretData.PitchLocalPos, m);

                    // only yaw rotation but for cylinder
                    pitchMatrix = MatrixD.CreateWorld(rotationPivot, drawMatrix.Down, pitchMatrix.Left);

                    Vector3D firstOuterRimVec, lastOuterRimVec;
                    DrawTurretAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                        ref pitchMatrix, radius, minPitch, maxPitch, LineEveryDegrees,
                        colorPitch, colorPitchLine, OVERLAY_SQUARE_MATERIAL, OVERLAY_LASER_MATERIAL, lineThick, OVERLAY_BLEND_TYPE);

                    if(canDrawLabel)
                    {
                        var labelDir = Vector3D.Normalize(lastOuterRimVec - pitchMatrix.Translation);
                        DrawLineLabel(TextAPIMsgIds.PitchLimit, lastOuterRimVec, labelDir, colorPitchLine, "Pitch limit");
                    }
                }

                {
                    var colorYaw = (Color.Lime * 0.25f).ToVector4();
                    var colorYawLine = Color.Lime.ToVector4();

                    Vector3D rotationPivot = Vector3D.Transform(turretData.YawLocalPos, drawMatrix);

                    var yawMatrix = MatrixD.CreateWorld(rotationPivot, drawMatrix.Right, drawMatrix.Down);

                    Vector3D firstOuterRimVec, lastOuterRimVec;
                    DrawTurretAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                        ref yawMatrix, radius, minYaw, maxYaw, LineEveryDegrees,
                        colorYaw, colorYawLine, OVERLAY_SQUARE_MATERIAL, OVERLAY_LASER_MATERIAL, lineThick, OVERLAY_BLEND_TYPE);

                    if(canDrawLabel)
                    {
                        var labelDir = Vector3D.Normalize(firstOuterRimVec - yawMatrix.Translation);
                        DrawLineLabel(TextAPIMsgIds.YawLimit, firstOuterRimVec, labelDir, colorYawLine, "Yaw limit");
                    }
                }
            }
            #endregion Turret pitch/yaw limits
        }

        private void DrawOverlay_WeaponCoreWeapon(MyCubeBlockDefinition def, WcApiDef.WeaponDefinition wcDef, MatrixD drawMatrix)
        {
            // TODO implement
        }

        private void DrawOverlay_ConveyorSorter(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            WcApiDef.WeaponDefinition wcDef;
            if(Main.WeaponCoreAPIHandler.Weapons.TryGetValue(def.Id, out wcDef))
            {
                DrawOverlay_WeaponCoreWeapon(def, wcDef, drawMatrix);
                return;
            }
        }

        private void DrawOverlay_Drill(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var drill = (MyShipDrillDefinition)def;

            const int wireDivRatio = 20;
            var colorSensorText = Color.Gray;
            var colorSensorFace = colorSensorText * 0.75f;
            var colorMineText = Color.Lime;
            var colorMineFace = colorMineText * 0.75f;
            var colorCarveText = Color.Red;
            var colorCarveFace = colorCarveText * 0.75f;
            float lineThickness = 0.03f;
            var material = OVERLAY_LASER_MATERIAL;
            bool drawLabel = CanDrawLabel();

            #region Mining
            var mineMatrix = drawMatrix;
            mineMatrix.Translation += mineMatrix.Forward * drill.CutOutOffset;
            float mineRadius = Hardcoded.ShipDrill_VoxelVisualAdd + drill.CutOutRadius;
            Utils.DrawTransparentSphere(ref mineMatrix, mineRadius, ref colorMineFace, MySimpleObjectRasterizer.Wireframe, wireDivRatio, lineThickness: lineThickness, material: material, blendType: OVERLAY_BLEND_TYPE);

            if(drawLabel)
            {
                var labelDir = mineMatrix.Up;
                var sphereEdge = mineMatrix.Translation + (labelDir * mineRadius);
                DrawLineLabel(TextAPIMsgIds.MineRadius, sphereEdge, labelDir, colorMineText, message: "Mining radius");
            }
            #endregion

            #region Carving
            var carveMatrix = mineMatrix;
            float carveRadius = Hardcoded.ShipDrill_VoxelVisualAdd + (drill.CutOutRadius * Hardcoded.ShipDrill_MineVoelNoOreRadiusMul);
            Utils.DrawTransparentSphere(ref carveMatrix, carveRadius, ref colorCarveFace, MySimpleObjectRasterizer.Wireframe, wireDivRatio, lineThickness: lineThickness, material: material, blendType: OVERLAY_BLEND_TYPE);

            if(drawLabel)
            {
                var labelDir = carveMatrix.Up;
                var sphereEdge = carveMatrix.Translation + (labelDir * carveRadius);
                DrawLineLabel(TextAPIMsgIds.CarveRadius, sphereEdge, labelDir, colorCarveText, message: "Carving radius");
            }
            #endregion

            #region Sensor
            var sensorMatrix = drawMatrix;
            sensorMatrix.Translation += sensorMatrix.Forward * drill.SensorOffset;
            float sensorRadius = drill.SensorRadius;

            if(Math.Abs(mineRadius - sensorRadius) > 0.001f || Math.Abs(drill.CutOutOffset - drill.SensorOffset) > 0.001f)
            {
                Utils.DrawTransparentSphere(ref sensorMatrix, sensorRadius, ref colorSensorFace, MySimpleObjectRasterizer.Wireframe, wireDivRatio, lineThickness: lineThickness, material: material, blendType: OVERLAY_BLEND_TYPE);
            }

            if(drawLabel)
            {
                var labelDir = drawMatrix.Left;
                var sphereEdge = sensorMatrix.Translation + (labelDir * sensorRadius);
                DrawLineLabel(TextAPIMsgIds.SensorRadius, sphereEdge, labelDir, colorSensorText, message: "Entity detection radius");
            }
            #endregion
        }

        private void DrawOverlay_ShipTool(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var data = BData_Base.TryGetDataCached<BData_ShipTool>(def);
            if(data == null)
                return;

            const int wireDivRatio = 20;
            var color = Color.Lime;
            var colorFace = color * 0.75f;
            float lineThickness = 0.03f;

            var toolDef = (MyShipToolDefinition)def;
            var matrix = data.DummyMatrix;
            var sensorCenter = matrix.Translation + matrix.Forward * toolDef.SensorOffset;
            drawMatrix.Translation = Vector3D.Transform(sensorCenter, drawMatrix);
            var radius = toolDef.SensorRadius;

            Utils.DrawTransparentSphere(ref drawMatrix, radius, ref colorFace, MySimpleObjectRasterizer.Wireframe, wireDivRatio, lineThickness: lineThickness, material: OVERLAY_LASER_MATERIAL, blendType: OVERLAY_BLEND_TYPE);

            if(CanDrawLabel())
            {
                bool isWelder = def is MyShipWelderDefinition;
                var labelDir = drawMatrix.Down;
                var sphereEdge = drawMatrix.Translation + (labelDir * radius);

                label.Clear().Append(isWelder ? "Welding radius" : "Grinding radius");
                DrawLineLabel(TextAPIMsgIds.DynamicLabel, sphereEdge, labelDir, color);
            }
        }

        private void DrawOverlay_Thruster(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var data = BData_Base.TryGetDataCached<BData_Thrust>(def);
            if(data == null)
                return;

            const int wireDivideRatio = 20;
            const float lineThickness = 0.02f;
            var color = Color.Red;
            var colorFace = color * 0.5f;
            var capsuleMatrix = MatrixD.CreateWorld(Vector3D.Zero, drawMatrix.Up, drawMatrix.Backward); // capsule is rotated weirdly (pointing up), needs adjusting
            bool drawLabel = CanDrawLabel();

            foreach(var flame in data.Flames)
            {
                var start = Vector3D.Transform(flame.LocalFrom, drawMatrix);
                capsuleMatrix.Translation = start + (drawMatrix.Forward * (flame.CapsuleLength * 0.5)); // capsule's position is in the center

                float paddedRadius = flame.CapsuleRadius + Hardcoded.Thrust_DamageCapsuleRadiusAdd;
                Utils.DrawTransparentCapsule(ref capsuleMatrix, paddedRadius, flame.CapsuleLength, ref colorFace, MySimpleObjectRasterizer.Wireframe, wireDivideRatio, lineThickness: lineThickness, material: OVERLAY_LASER_MATERIAL, blendType: OVERLAY_BLEND_TYPE);

                if(drawLabel)
                {
                    drawLabel = false; // label only on the first flame
                    var labelDir = drawMatrix.Down;
                    var labelLineStart = Vector3D.Transform(flame.LocalTo, drawMatrix) + labelDir * paddedRadius;
                    DrawLineLabel(TextAPIMsgIds.ThrustDamage, labelLineStart, labelDir, color, message: "Thrust damage");
                }
            }
        }

        private void DrawOverlay_LandingGear(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var data = BData_Base.TryGetDataCached<BData_LandingGear>(def);
            if(data == null)
                return;

            var color = new Color(20, 255, 155);
            var colorFace = color * 0.5f;
            bool drawLabel = CanDrawLabel();

            foreach(var obb in data.Magents)
            {
                var localBB = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);
                var m = MatrixD.CreateFromQuaternion(obb.Orientation);
                m.Translation = obb.Center;
                m *= drawMatrix;

                MySimpleObjectDraw.DrawTransparentBox(ref m, ref localBB, ref colorFace, MySimpleObjectRasterizer.Wireframe, 2, lineWidth: 0.03f, lineMaterial: OVERLAY_LASER_MATERIAL, blendType: MOUNTPOINT_BLEND_TYPE);

                if(drawLabel)
                {
                    drawLabel = false; // only label the first one
                    var labelDir = drawMatrix.Down;
                    var labelLineStart = m.Translation + (m.Down * localBB.HalfExtents.Y) + (m.Backward * localBB.HalfExtents.Z) + (m.Left * localBB.HalfExtents.X);
                    DrawLineLabel(TextAPIMsgIds.MagnetizedArea, labelLineStart, labelDir, color, message: "Magnetized Area");
                }
            }
        }

        private void DrawOverlay_Collector(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var data = BData_Base.TryGetDataCached<BData_Collector>(def);
            if(data == null)
                return;

            var color = new Color(20, 255, 100);
            var colorFace = color * 0.5f;
            bool drawLabel = CanDrawLabel();

            var localBB = new BoundingBoxD(-Vector3.Half, Vector3.Half);
            var m = data.boxLocalMatrix * drawMatrix;

            MySimpleObjectDraw.DrawTransparentBox(ref m, ref localBB, ref colorFace, MySimpleObjectRasterizer.Wireframe, 2, lineWidth: 0.03f, lineMaterial: OVERLAY_LASER_MATERIAL, blendType: MOUNTPOINT_BLEND_TYPE);

            if(drawLabel)
            {
                var labelDir = drawMatrix.Down;
                var labelLineStart = m.Translation + (m.Down * localBB.HalfExtents.Y) + (m.Backward * localBB.HalfExtents.Z) + (m.Left * localBB.HalfExtents.X);
                DrawLineLabel(TextAPIMsgIds.CollectionArea, labelLineStart, labelDir, color, message: "Collection Area");
            }
        }

        private void DrawOverlay_WindTurbine(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var turbineDef = (MyWindTurbineDefinition)def;
            bool drawLabel = CanDrawLabel();

            const float lineThick = 0.05f;
            const float groundLineThick = 0.1f;
            const float groundBottomLineThick = 0.05f;
            Color minColor = Color.Red;
            Color maxColor = Color.YellowGreen;
            Vector4 minLineColorVec = minColor.ToVector4();
            Vector4 maxLineColorVec = maxColor.ToVector4();
            Vector4 minColorVec = (minColor * 0.4f).ToVector4();
            Vector4 maxColorVec = (maxColor * 0.4f).ToVector4();
            Vector4 triangleColor = minColorVec.ToLinearRGB(); // HACK required to match the colors of bother billboards
            MyQuadD quad;

            #region Side clearence circle
            float maxRadius = turbineDef.RaycasterSize;
            float minRadius = maxRadius * turbineDef.MinRaycasterClearance;

            float minRadiusRatio = turbineDef.MinRaycasterClearance;
            float maxRadiusRatio = 1f - minRadiusRatio;

            Vector3D up = drawMatrix.Up;
            Vector3D center = drawMatrix.Translation;

            Vector3D current = Vector3D.Zero;
            Vector3D previous = Vector3D.Zero;
            Vector3D previousInner = Vector3D.Zero;

            const int wireDivideRatio = 360 / 5;
            const float stepDeg = 360f / wireDivideRatio;

            for(int i = 0; i <= wireDivideRatio; i++)
            {
                double angleRad = MathHelperD.ToRadians(stepDeg * i);

                current.X = maxRadius * Math.Cos(angleRad);
                current.Y = 0;
                current.Z = maxRadius * Math.Sin(angleRad);
                current = Vector3D.Transform(current, drawMatrix);

                Vector3D dirToOut = (current - center);
                Vector3D inner = center + dirToOut * minRadiusRatio;

                if(i > 0)
                {
                    // inner circle slice
                    MyTransparentGeometry.AddTriangleBillboard(center, inner, previousInner, up, up, up, Vector2.Zero, Vector2.Zero, Vector2.Zero, OVERLAY_SQUARE_MATERIAL, 0, center, triangleColor, OVERLAY_BLEND_TYPE);

                    // outer circle gradient slices
                    quad = new MyQuadD()
                    {
                        Point0 = previousInner,
                        Point1 = previous,
                        Point2 = current,
                        Point3 = inner,
                    };
                    MyTransparentGeometry.AddQuad(OVERLAY_GRADIENT_MATERIAL, ref quad, minColorVec, ref center, blendType: OVERLAY_BLEND_TYPE);

                    quad = new MyQuadD()
                    {
                        Point0 = previous,
                        Point1 = previousInner,
                        Point2 = inner,
                        Point3 = current,
                    };
                    MyTransparentGeometry.AddQuad(OVERLAY_GRADIENT_MATERIAL, ref quad, maxColorVec, ref center, blendType: OVERLAY_BLEND_TYPE);

                    // inner+outer circle rims
                    MyTransparentGeometry.AddLineBillboard(OVERLAY_LASER_MATERIAL, minLineColorVec, previousInner, (inner - previousInner), 1f, lineThick, OVERLAY_BLEND_TYPE);
                    MyTransparentGeometry.AddLineBillboard(OVERLAY_LASER_MATERIAL, maxLineColorVec, previous, (current - previous), 1f, lineThick, OVERLAY_BLEND_TYPE);
                }

                previous = current;
                previousInner = inner;
            }
            #endregion Side clearence circle

            if(drawLabel)
            {
                var labelDir = drawMatrix.Up;
                var labelLineStart = center + drawMatrix.Left * minRadius;
                DrawLineLabel(TextAPIMsgIds.SideClearence, labelLineStart, labelDir, new Color(255, 155, 0), message: "Side Clearence", lineHeight: 0.5f);

                //labelDir = drawMatrix.Up;
                //labelLineStart = center + drawMatrix.Left * maxRadius;
                //DrawLineLabel(TextAPIMsgIds.OptimalClearence, labelLineStart, labelDir, maxColor, message: "Optimal Clearence");
            }

            #region Ground clearence line
            Vector3D lineStart = drawMatrix.Translation;
            float artificialMultiplier;
            Vector3 gravityAccel = MyAPIGateway.Physics.CalculateNaturalGravityAt(lineStart, out artificialMultiplier);
            bool gravityNearby = (gravityAccel.LengthSquared() > 0);
            Vector3D end;

            if(gravityNearby)
                end = lineStart + Vector3.Normalize(gravityAccel) * turbineDef.OptimalGroundClearance;
            else
                end = lineStart + drawMatrix.Down * turbineDef.OptimalGroundClearance;

            Vector3D minClearence = Vector3D.Lerp(lineStart, end, turbineDef.MinRaycasterClearance);

            MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, minColor, lineStart, (minClearence - lineStart), 1f, groundLineThick, OVERLAY_BLEND_TYPE);

            Vector3D lineDir = (end - minClearence);
            MyTransparentGeometry.AddLineBillboard(OVERLAY_GRADIENT_MATERIAL, minColor, minClearence, lineDir, 1f, groundLineThick, OVERLAY_BLEND_TYPE);
            MyTransparentGeometry.AddLineBillboard(OVERLAY_GRADIENT_MATERIAL, maxColor, end, -lineDir, 1f, groundLineThick, OVERLAY_BLEND_TYPE);

            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D right = Vector3D.Normalize(Vector3D.Cross(lineDir, camMatrix.Forward)); // this determines line width, it's normalized so 1m, doubled because of below math
            MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, maxColor, end - right, right, 2f, groundBottomLineThick, OVERLAY_BLEND_TYPE);

            if(drawLabel)
            {
                var labelDir = drawMatrix.Left;
                var labelLineStart = Vector3D.Lerp(lineStart, end, 0.5f);
                DrawLineLabel(TextAPIMsgIds.TerrainClearence, labelLineStart, labelDir, new Color(255, 155, 0), message: "Terrain Clearence");
            }
            #endregion Ground clearence line
        }

        private void DrawOverlay_LaserAntenna(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var antennaDef = def as MyLaserAntennaDefinition;
            if(antennaDef == null)
                return;

            var data = BData_Base.TryGetDataCached<BData_LaserAntenna>(def);
            if(data == null)
                return;

            bool canDrawLabel = CanDrawLabel();
            IMySlimBlock slimBlock = (Main.LockOverlay.LockedOnBlock ?? Main.EquipmentMonitor.AimedBlock);

            float cellSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize); // TODO: cache

            const float LaserThick = 0.02f;
            const float LaserLength = 15f;

            const int LineEveryDegrees = 15;
            const float LineThick = 0.03f;
            float radius = (def.Size.AbsMin() + 1) * cellSize;

            int minPitch = Math.Max(antennaDef.MinElevationDegrees, -90);
            int maxPitch = Math.Min(antennaDef.MaxElevationDegrees, 90);

            int minYaw = antennaDef.MinAzimuthDegrees;
            int maxYaw = antennaDef.MaxAzimuthDegrees;

            {
                var colorPitch = (Color.Red * 0.3f).ToVector4();
                var colorPitchLine = Color.Red.ToVector4();

                MatrixD pitchMatrix = drawMatrix;
                if(slimBlock?.FatBlock != null)
                {
                    var internalBlock = (MyCubeBlock)slimBlock.FatBlock;

                    // TODO: find a way to get a useful matrix out of the pitch part...
                    // from MyLaserAntenna.OnModelChange()
                    //var subpartYaw = internalBlock.Subparts?.GetValueOrDefault("LaserComTurret", null);
                    //var subpartPitch = subpartYaw?.Subparts?.GetValueOrDefault("LaserCom", null);

                    //if(subpartPitch != null)
                    //{
                    //    pitchMatrix = subpartPitch.WorldMatrix;
                    //    pitchMatrix.Translation = drawMatrix.Translation;
                    //}

                    // HACK: altenrate way of getting part rotation.
                    var antenna = (IMyLaserAntenna)internalBlock;
                    if(antenna.Other != null)
                    {
                        var dir = (antenna.Other.WorldMatrix.Translation - drawMatrix.Translation);
                        pitchMatrix = MatrixD.CreateWorld(drawMatrix.Translation, dir, drawMatrix.Up);
                    }
                }

                // only yaw rotation
                var m = MatrixD.CreateWorld(drawMatrix.Translation, Vector3D.Cross(pitchMatrix.Left, drawMatrix.Up), drawMatrix.Up);
                Vector3D rotationPivot = Vector3D.Transform(data.PitchLocalPos, m);

                // laser visualization
                Color laserColor = new Color(255, 155, 0);
                MyTransparentGeometry.AddLineBillboard(OVERLAY_GRADIENT_MATERIAL, laserColor, rotationPivot, pitchMatrix.Forward, LaserLength, LaserThick, OVERLAY_BLEND_TYPE);

                if(canDrawLabel)
                {
                    var labelPos = rotationPivot + pitchMatrix.Forward * (LaserLength / 2);
                    DrawLineLabel(TextAPIMsgIds.Laser, labelPos, pitchMatrix.Up, laserColor, "Laser");
                }

                // only yaw rotation but for cylinder
                pitchMatrix = MatrixD.CreateWorld(rotationPivot, drawMatrix.Down, pitchMatrix.Left);

                Vector3D firstOuterRimVec, lastOuterRimVec;
                DrawTurretAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                    ref pitchMatrix, radius, minPitch, maxPitch, LineEveryDegrees,
                    colorPitch, colorPitchLine, OVERLAY_SQUARE_MATERIAL, OVERLAY_LASER_MATERIAL, LineThick, OVERLAY_BLEND_TYPE);

                if(canDrawLabel)
                {
                    var labelDir = Vector3D.Normalize(lastOuterRimVec - pitchMatrix.Translation);
                    DrawLineLabel(TextAPIMsgIds.PitchLimit, lastOuterRimVec, labelDir, colorPitchLine, "Pitch limit");
                }
            }

            {
                var colorYaw = (Color.Lime * 0.25f).ToVector4();
                var colorYawLine = Color.Lime.ToVector4();

                Vector3D rotationPivot = Vector3D.Transform(data.YawLocalPos, drawMatrix);

                var yawMatrix = MatrixD.CreateWorld(rotationPivot, drawMatrix.Right, drawMatrix.Down);

                Vector3D firstOuterRimVec, lastOuterRimVec;
                DrawTurretAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                    ref yawMatrix, radius, minYaw, maxYaw, LineEveryDegrees,
                    colorYaw, colorYawLine, OVERLAY_SQUARE_MATERIAL, OVERLAY_LASER_MATERIAL, LineThick, OVERLAY_BLEND_TYPE);

                if(canDrawLabel)
                {
                    var labelDir = Vector3D.Normalize(firstOuterRimVec - yawMatrix.Translation);
                    DrawLineLabel(TextAPIMsgIds.YawLimit, firstOuterRimVec, labelDir, colorYawLine, "Yaw limit");
                }
            }
        }
        #endregion Block-specific overlays

        #region Draw helpers
        private StringBuilder label = new StringBuilder(128);

        private void DrawLineLabel(TextAPIMsgIds id, Vector3D start, Vector3D direction, Color color, string message = null, float lineHeight = 0.5f, float lineThick = 0.005f, OverlayLabelsFlags settingFlag = OverlayLabelsFlags.Other)
        {
            var cm = MyAPIGateway.Session.Camera.WorldMatrix;
            var offset = cm.Right * LABEL_SHADOW_OFFSET.X + cm.Up * LABEL_SHADOW_OFFSET.Y;

            MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, LABEL_SHADOW_COLOR, start + offset, direction, lineHeight, lineThick, LABEL_SHADOW_BLEND_TYPE);
            MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, color, start, direction, lineHeight, lineThick, LABEL_BLEND_TYPE);

            if(Main.Config.OverlayLabels.IsSet(settingFlag) || (Main.Config.OverlaysLabelsAlt.Value && MyAPIGateway.Input.IsAnyAltKeyPressed()))
            {
                var textWorldPos = start + direction * lineHeight;
                AnyLabelShown = true;

                var i = (int)id;
                var labelData = Labels[i];

                if(labelData == null)
                    Labels[i] = labelData = new LabelData();

                if(labelData.Text == null)
                {
                    var shadowSB = new StringBuilder(label.Capacity);
                    var msgSB = new StringBuilder(label.Capacity);

                    labelData.Shadow = new HudAPIv2.SpaceMessage(shadowSB, textWorldPos, Vector3D.Up, Vector3D.Left, LABEL_TEXT_SCALE, Blend: LABEL_SHADOW_BLEND_TYPE);
                    labelData.Text = new HudAPIv2.SpaceMessage(msgSB, textWorldPos, Vector3D.Up, Vector3D.Left, LABEL_TEXT_SCALE, Blend: LABEL_BLEND_TYPE);

                    // not necessary for text.Draw() to work
                    labelData.Shadow.Visible = false;
                    labelData.Text.Visible = false;

                    if(message != null)
                    {
                        shadowSB.Color(LABEL_SHADOW_COLOR).Append(message);
                        msgSB.Color(color).Append(message);

                        labelData.UnderlineLength = GetLabelUnderlineLength(labelData.Text);
                    }
                }

                var shadow = labelData.Shadow;
                var text = labelData.Text;

                if(message == null)
                {
                    shadow.Message.Clear().Color(LABEL_SHADOW_COLOR).AppendStringBuilder(label);
                    text.Message.Clear().Color(color).AppendStringBuilder(label);

                    labelData.UnderlineLength = GetLabelUnderlineLength(text);
                }

                var textPos = textWorldPos + cm.Right * LABEL_OFFSET.X + cm.Up * LABEL_OFFSET.Y;
                var shadowPos = textPos + cm.Right * LABEL_SHADOW_OFFSET.X + cm.Up * LABEL_SHADOW_OFFSET.Y + cm.Forward * 0.0001;

                shadow.WorldPosition = shadowPos;
                shadow.Left = cm.Left;
                shadow.Up = cm.Up;
                shadow.Draw(); // this removes the need of having the text visible, also draws text more accurately to my position

                text.WorldPosition = textPos;
                text.Left = cm.Left;
                text.Up = cm.Up;
                text.Draw();

                var underlineLength = labelData.UnderlineLength;
                MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, LABEL_SHADOW_COLOR, textWorldPos + offset, cm.Right, underlineLength, lineThick, LABEL_SHADOW_BLEND_TYPE);
                MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, color, textWorldPos, cm.Right, underlineLength, lineThick, LABEL_BLEND_TYPE);
            }
        }

        private float GetLabelUnderlineLength(HudAPIv2.SpaceMessage msg)
        {
            var textSize = msg.GetTextLength();
            return (float)(LABEL_OFFSET.X + (textSize.X * LABEL_TEXT_SCALE));
        }

        private void DrawMountPointAxisText(MyCubeBlockDefinition def, float gridSize, ref MatrixD drawMatrix)
        {
            var matrix = MatrixD.CreateScale(def.Size * gridSize);
            matrix.Translation = (def.Center - (def.Size * 0.5f));
            matrix = matrix * drawMatrix;

            DrawAxis(TextAPIMsgIds.AxisZ, ref Vector3.Forward, Color.Blue, ref drawMatrix, ref matrix);
            DrawAxis(TextAPIMsgIds.AxisX, ref Vector3.Right, Color.Red, ref drawMatrix, ref matrix);
            DrawAxis(TextAPIMsgIds.AxisY, ref Vector3.Up, Color.Lime, ref drawMatrix, ref matrix);
        }

        private void DrawAxis(TextAPIMsgIds id, ref Vector3 direction, Color color, ref MatrixD drawMatrix, ref MatrixD matrix)
        {
            var dir = Vector3D.TransformNormal(direction * 0.5f, matrix);
            var text = AxisLabels[(int)id];
            DrawLineLabel(id, drawMatrix.Translation, dir, color, message: text, lineHeight: 1.5f, settingFlag: OverlayLabelsFlags.Axis);
        }

        private void DrawTurretAxisLimit(out Vector3D firstOuterRimVec, out Vector3D lastOuterRimVec,
          ref MatrixD worldMatrix, float radius, int startAngle, int endAngle, int lineEveryDegrees,
          Vector4 faceColor, Vector4 lineColor, MyStringId? faceMaterial = null, MyStringId? lineMaterial = null, float lineThickness = 0.01f,
          BlendTypeEnum blendType = BlendTypeEnum.Standard)
        {
            const int wireDivRatio = (360 / 5); // quality

            Vector3D center = worldMatrix.Translation;
            Vector3D normal = worldMatrix.Forward;
            firstOuterRimVec = center + normal * radius; // fallback
            lastOuterRimVec = firstOuterRimVec;

            Vector4 triangleColor = (faceMaterial.HasValue ? faceColor.ToLinearRGB() : faceColor); // HACK keeping color consistent with other billboards

            // from MyLargeTurretBase

            //static float NormalizeAngle(int angle)
            //{
            //    int n = angle % 360;
            //    if(n == 0 && angle != 0)
            //        return 360f;
            //    return n;
            //}
            // inlined of above
            int startN = startAngle % 360;
            startAngle = ((startN == 0 && startAngle != 0) ? 360 : startN);

            int endN = endAngle % 360;
            endAngle = ((endN == 0 && endAngle != 0) ? 360 : endN);

            double startRad = MathHelperD.ToRadians(startAngle);
            double endRad = MathHelperD.ToRadians(endAngle);
            if(startRad > endRad)
                startRad -= MathHelperD.TwoPi;

            Vector3D current = Vector3D.Zero;
            Vector3D previous = Vector3D.Zero;
            double angleRad = startRad;

            double stepRad = MathHelperD.TwoPi / wireDivRatio;
            bool first = true;

            Vector2 uv0 = new Vector2(0, 0.5f);
            Vector2 uv1 = new Vector2(1, 0);
            Vector2 uv2 = new Vector2(1, 1);

            while(true)
            {
                bool exit = false;
                if(angleRad > endRad)
                {
                    angleRad = endRad;
                    exit = true;
                }

                double x = radius * Math.Cos(angleRad);
                double z = radius * Math.Sin(angleRad);
                current.X = worldMatrix.M41 + x * worldMatrix.M11 + z * worldMatrix.M31; // inlined Transform() without scale
                current.Y = worldMatrix.M42 + x * worldMatrix.M12 + z * worldMatrix.M32;
                current.Z = worldMatrix.M43 + x * worldMatrix.M13 + z * worldMatrix.M33;

                if((first || exit) && lineMaterial.HasValue)
                {
                    MyTransparentGeometry.AddLineBillboard(lineMaterial.Value, lineColor, center, (current - center), 1f, lineThickness, blendType);
                }

                if(!first && faceMaterial.HasValue)
                {
                    MyTransparentGeometry.AddTriangleBillboard(center, current, previous, normal, normal, normal, uv0, uv1, uv2, faceMaterial.Value, 0, center, triangleColor, blendType);
                }

                if(exit)
                {
                    lastOuterRimVec = current;
                    break;
                }

                if(first)
                {
                    firstOuterRimVec = current;

                    angleRad = -MathHelperD.TwoPi;
                    while(angleRad < startRad)
                        angleRad += stepRad;
                }
                else
                {
                    angleRad += stepRad;
                }

                first = false;
                previous = current;
            }
        }
        #endregion Draw helpers

        private bool CanDrawLabel(OverlayLabelsFlags labelsSetting = OverlayLabelsFlags.Other)
        {
            return Main.TextAPI.IsEnabled && (Main.Config.OverlayLabels.IsSet(labelsSetting) || (Main.Config.OverlaysLabelsAlt.Value && MyAPIGateway.Input.IsAnyAltKeyPressed()));
        }
    }
}
