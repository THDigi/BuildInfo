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

// TODO: separate overlay code for clean multi-block overlay (held + lockon + anything in future)

namespace Digi.BuildInfo.Features.Overlays
{
    public class Overlays : ModComponent
    {
        public int DrawOverlay { get; private set; }

        private OverlayCall SelectedOverlayCall;
        private IMyHudNotification OverlayNotification;

        private float CellSize;
        private float CellSizeHalf;

        private bool AnyLabelShown;
        private readonly LabelData[] Labels;

        private bool BlockFunctionalForPressure;

        private bool DoorAirtightBlink = true;
        //private int DoorAirtightBlinkTick = 0;

        class LabelData
        {
            public HudAPIv2.SpaceMessage Text;
            public HudAPIv2.SpaceMessage Shadow;
            public float UnderlineLength = -1;
        }

        public delegate void OverlayCall(MyCubeBlockDefinition def, MatrixD drawMatrix);
        public readonly Dictionary<MyObjectBuilderType, OverlayCall> DrawLookup
                  = new Dictionary<MyObjectBuilderType, OverlayCall>(MyObjectBuilderType.Comparer);

        #region Constants
        private const BlendTypeEnum OVERLAY_BLEND_TYPE = BlendTypeEnum.PostPP;
        public readonly MyStringId OVERLAY_SQUARE_MATERIAL = MyStringId.GetOrCompute("BuildInfo_Square");
        public readonly MyStringId OVERLAY_LASER_MATERIAL = MyStringId.GetOrCompute("BuildInfo_Laser");
        public readonly MyStringId OVERLAY_DOT_MATERIAL = MyStringId.GetOrCompute("WhiteDot");
        public readonly MyStringId OVERLAY_GRADIENT_MATERIAL = MyStringId.GetOrCompute("BuildInfo_TransparentGradient");
        public readonly MyStringId OVERLAY_MUZZLEFLASH_MATERIAL = MyStringId.GetOrCompute("MuzzleFlashMachineGunFront");

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
            "Ports",
        };

        private enum TextAPIMsgIds
        {
            // its label is not constnt so id can be shared, use with the label sb
            DynamicLabel = 0,

            // the rest have static messages that are assigned on creation
            AxisZ,
            AxisX,
            AxisY,
            ModelOffset,
            SensorRadius,
            MineRadius,
            CarveRadius,
            WeldingRadius,
            GrindingRadius,
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
            UpdateOrder = -500; // for Draw() mainly, to always render first (and therefore, under)

            int count = Enum.GetValues(typeof(TextAPIMsgIds)).Length;
            Labels = new LabelData[count];
        }

        public override void RegisterComponent()
        {
            InitLookups();

            //Main.GameConfig.HudStateChanged += GameConfig_HudStateChanged;
            //Main.EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
            Main.EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;
        }

        public override void UnregisterComponent()
        {
            //Main.GameConfig.HudStateChanged -= GameConfig_HudStateChanged;
            //Main.EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
            Main.EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;
        }

        //private void GameConfig_HudStateChanged(HudState prevState, HudState newState)
        //{
        //    if(newState == HudState.OFF)
        //        HideLabels();
        //}

        //private void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        //{
        //    HideLabels();
        //}

        private void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            if(Main.LockOverlay.LockedOnBlock != null)
                return;

            SelectedOverlayCall = null;

            if(def != null)
                SelectedOverlayCall = DrawLookup.GetValueOrDefault(def.Id.TypeId, null);

            //HideLabels();
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
            int mode = DrawOverlay;
            if(++mode >= OverlayNames.Length)
                mode = 0;

            SetOverlayMode(mode, showNotification);
        }

        public void SetOverlayMode(int mode, bool showNotification = true)
        {
            DrawOverlay = mode;

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, DrawOverlay > 0);
            //HideLabels();

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
                SelectedOverlayCall = DrawLookup.GetValueOrDefault(defId.Value.TypeId, null);
            else
                SelectedOverlayCall = null;
        }

        private void InitLookups()
        {
            // TODO: add sensor/gravity/etc range overlay that's better than the vanilla one!

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
            DrawLookup.Add(blockType, call);
        }

        public override void UpdateDraw()
        {
            if(DrawOverlay == 0 || (Main.LockOverlay.LockedOnBlock == null && Main.EquipmentMonitor.BlockDef == null) || (Main.GameConfig.HudState == HudState.OFF && !Main.Config.OverlaysAlwaysVisible.Value))
                return;

            var def = Main.EquipmentMonitor.BlockDef;
            var aimedBlock = Main.EquipmentMonitor.AimedBlock;

            // re-assigns def & aimedBlock if overlay is locked-on to something different than the aimed block
            if(Main.LockOverlay.LockedOnBlock != null && !Main.LockOverlay.UpdateLockedOnBlock(ref aimedBlock, ref def))
                return;

            // TODO: show overlays on both held block and lock-on overlay at same time.

            CellSize = (aimedBlock != null ? aimedBlock.CubeGrid.GridSize : Main.EquipmentMonitor.BlockGridSize);
            CellSizeHalf = CellSize / 2;

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
                    if(aimedBlock == null)
                    {
                        SetOverlayMode(0);
                        throw new Exception($"Unexpected case: aimedBlock=false, LockedOnBlock={(Main.LockOverlay.LockedOnBlock != null).ToString()}, " +
                                            $"IsCubeBuilder={Main.EquipmentMonitor.IsCubeBuilder.ToString()}, Tool={Main.EquipmentMonitor.ToolDefId.ToString()}, " +
                                            $"CubeBuilderActive={MyCubeBuilder.Static.IsActivated.ToString()}");
                    }

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
                //        var halfExtents = nd.Size * CellSizeHalf;
                //        var localBB = new BoundingBoxD(-halfExtents, halfExtents).Inflate(MOUNTPOINT_THICKNESS * 0.5);

                //        var color = Color.Lime;

                //        MySimpleObjectDraw.DrawTransparentBox(ref wm, ref localBB, ref color, MySimpleObjectRasterizer.Wireframe, 4, 0.001f, lineMaterial: MyStringId.GetOrCompute("Square"), blendType: BlendTypeEnum.PostPP);
                //    }
                //}

#if false // TODO: implement solar as overlay? a box that covers the rays+padding and points towards sun, fades out to not expose max distance...
                if(aimedBlock != null)
                {
                    var solarPanel = aimedBlock.FatBlock as IMySolarPanel;
                    if(solarPanel != null)
                    {
                        var sunDir = Sandbox.Game.MyVisualScriptLogicProvider.GetSunDirection();
                        var solarDef = (MySolarPanelDefinition)def;

                        var angleToSun = Vector3.Dot(Vector3.Transform(solarDef.PanelOrientation, solarPanel.WorldMatrix.GetOrientation()), sunDir);
                        bool isTwoSided = solarDef.IsTwoSided;

                        for(int idx = 0; idx < 8; idx++)
                        {
                            if((angleToSun < 0f && !isTwoSided) || !solarPanel.IsFunctional)
                                continue;

                            //var pos = solar.WorldMatrix.Translation;
                            //MyPlanet closestPlanet = MyGamePruningStructure.GetClosestPlanet(pos);
                            //if(closestPlanet == null)
                            //    continue;
                            //
                            //public static bool IsThereNight(MyPlanet planet, ref Vector3D position)
                            //{
                            //    Vector3D value = position - planet.PositionComp.GetPosition();
                            //    if((float)value.Length() > planet.MaximumRadius * 1.1f)
                            //    {
                            //        return false;
                            //    }
                            //    Vector3 vector = Vector3.Normalize(value);
                            //    return Vector3.Dot(MySector.DirectionToSunNormalized, vector) < -0.1f;
                            //}
                            //if(IsThereNight(closestPlanet, ref pos))
                            //    continue;

                            MatrixD orientation = solarPanel.WorldMatrix.GetOrientation();
                            var panelOrientationWorld = Vector3.Transform(solarDef.PanelOrientation, orientation);

                            float dotFw = (float)solarPanel.WorldMatrix.Forward.Dot(panelOrientationWorld);

                            Vector3D translation = solarPanel.WorldMatrix.Translation;
                            translation += ((idx % 4) - 1.5f) * CellSize * dotFw * (solarDef.Size.X / 4f) * solarPanel.WorldMatrix.Left;
                            translation += ((idx / 4) - 0.5f) * CellSize * dotFw * (solarDef.Size.Y / 2f) * solarPanel.WorldMatrix.Up;

                            translation += CellSize * dotFw * (solarDef.Size.Z / 2f) * panelOrientationWorld * solarDef.PanelOffset;

                            var from = translation + sunDir * 100f;
                            var to = translation + sunDir * solarPanel.CubeGrid.GridSize / 4f;

                            MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, Color.Orange, from, (to - from), 1f, 0.05f, OVERLAY_BLEND_TYPE);
                        }
                    }
                }
#endif

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
                    DrawMountPointAxisText(def, ref drawMatrix);
                }
                else
                {
                    // re-assigning mount points temporarily to prevent the original mountpoint wireframe from being drawn while keeping the axis information
                    var mp = def.MountPoints;
                    def.MountPoints = BLANK_MOUNTPOINTS;
                    MyCubeBuilder.DrawMountPoints(CellSize, def, ref drawMatrix);
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
                    var mainMatrix = MatrixD.CreateTranslation((center - (def.Size * 0.5f)) * CellSize) * drawMatrix;
                    var mountPoints = def.GetBuildProgressModelMountPoints(1f);
                    bool drawLabel = CanDrawLabel();

                    if(DrawOverlay == 1 && BlockFunctionalForPressure)
                    {
                        // TODO: have a note saying that blocks that aren't fully built are always not airtight? (blockFunctionalForPressure)

                        if(def.IsAirTight.HasValue)
                        {
                            if(def.IsAirTight.Value)
                            {
                                var halfExtents = def.Size * CellSizeHalf;
                                var localBB = new BoundingBoxD(-halfExtents, halfExtents).Inflate(MOUNTPOINT_THICKNESS * 0.5);
                                MySimpleObjectDraw.DrawTransparentBox(ref drawMatrix, ref localBB, ref AIRTIGHT_COLOR, MySimpleObjectRasterizer.Solid, 1, lineWidth: 0.01f, lineMaterial: OVERLAY_SQUARE_MATERIAL, faceMaterial: OVERLAY_SQUARE_MATERIAL, blendType: MOUNTPOINT_BLEND_TYPE);
                            }
                        }
                        else if(mountPoints != null)
                        {
                            var half = Vector3D.One * -CellSizeHalf;
                            var corner = (Vector3D)def.Size * -CellSizeHalf;
                            var transformMatrix = MatrixD.CreateTranslation(corner - half) * drawMatrix;

                            foreach(var kv in def.IsCubePressurized) // precomputed: [position][normal] = airtight type
                            {
                                foreach(var kv2 in kv.Value)
                                {
                                    if(kv2.Value != MyCubeBlockDefinition.MyCubePressurizationMark.PressurizedAlways) // pos+normal not always airtight
                                        continue;

                                    var pos = Vector3D.Transform((Vector3D)(kv.Key * CellSize), transformMatrix);
                                    var dirForward = Vector3.TransformNormal(kv2.Key, drawMatrix);
                                    var dirIndex = (int)Base6Directions.GetDirection(kv2.Key);
                                    var dirUp = Vector3.TransformNormal(DIRECTIONS[((dirIndex + 2) % 6)], drawMatrix);

                                    var m = MatrixD.Identity;
                                    m.Translation = pos + dirForward * CellSizeHalf;
                                    m.Forward = dirForward;
                                    m.Backward = -dirForward;
                                    m.Left = Vector3D.Cross(dirForward, dirUp);
                                    m.Right = -m.Left;
                                    m.Up = dirUp;
                                    m.Down = -dirUp;
                                    var scale = new Vector3D(CellSize, CellSize, MOUNTPOINT_THICKNESS);
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

                            var bb = new BoundingBoxD(Vector3.Min(startLocal, endLocal) * CellSize, Vector3.Max(startLocal, endLocal) * CellSize);
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
                if(DrawOverlay == 3)
                    DrawPortsMode(def, drawMatrix);
                else if(SelectedOverlayCall != null)
                    SelectedOverlayCall.Invoke(def, drawMatrix);

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

        //public void HideLabels()
        //{
        //    if(!AnyLabelShown)
        //        return;
        //
        //    AnyLabelShown = false;
        //
        //    for(int i = 0; i < Labels.Length; ++i)
        //    {
        //        var label = Labels[i];
        //
        //        if(label != null && label.Text != null)
        //        {
        //            label.Text.Visible = false;
        //            label.Shadow.Visible = false;
        //        }
        //    }
        //}

        #region Block-specific overlays
        private void DrawOverlay_Doors(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            if(DrawOverlay != 1)
                return;

            if(!BlockFunctionalForPressure)
            {
                //HideLabels();
                return;
            }

            IMySlimBlock block = (Main.LockOverlay.LockedOnBlock ?? Main.EquipmentMonitor.AimedBlock);

            //if(block != null)
            //{
            //    doorAirtightBlink = true;
            //}
            //else if(!MyParticlesManager.Paused && ++doorAirtightBlinkTick >= 60)
            //{
            //    doorAirtightBlinkTick = 0;
            //    doorAirtightBlink = !doorAirtightBlink;
            //}

            var cubeSize = def.Size * CellSizeHalf;
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
                var half = Vector3D.One * -CellSizeHalf;
                var corner = (Vector3D)def.Size * -CellSizeHalf;
                var transformMatrix = MatrixD.CreateTranslation(corner - half) * drawMatrix;

                foreach(var kv in def.IsCubePressurized) // precomputed: [position][normal] = airtight type
                {
                    foreach(var kv2 in kv.Value)
                    {
                        // only look for cell sides that are pressurized when doors are closed
                        if(kv2.Value != MyCubeBlockDefinition.MyCubePressurizationMark.PressurizedClosed)
                            continue;

                        var pos = Vector3D.Transform((Vector3D)(kv.Key * CellSize), transformMatrix);
                        var dirForward = Vector3.TransformNormal(kv2.Key, drawMatrix);
                        var dirIndex = (int)Base6Directions.GetDirection(kv2.Key);
                        var dirUp = Vector3.TransformNormal(DIRECTIONS[((dirIndex + 2) % 6)], drawMatrix);

                        if(DoorAirtightBlink)
                        {
                            var m = MatrixD.Identity;
                            m.Translation = pos + dirForward * CellSizeHalf;
                            m.Forward = dirForward;
                            m.Backward = -dirForward;
                            m.Left = Vector3D.Cross(dirForward, dirUp);
                            m.Right = -m.Left;
                            m.Up = dirUp;
                            m.Down = -dirUp;
                            var scale = new Vector3D(CellSize, CellSize, MOUNTPOINT_THICKNESS);
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

            //if(drawLabel) // no label was rendered since it would've set itself false by now
            //{
            //    HideLabels();
            //}
        }

        private void DrawOverlay_Weapons(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            WcApiDef.WeaponDefinition wcDef;
            if(Main.WeaponCoreAPIHandler.Weapons.TryGetValue(def.Id, out wcDef))
            {
                DrawOverlay_WeaponCoreWeapon(def, wcDef, drawMatrix);
                return;
            }

            var data = Main.LiveDataHandler.Get<BData_Weapon>(def);
            if(data == null)
                return;

            var weaponBlockDef = def as MyWeaponBlockDefinition;
            MyWeaponDefinition weaponDef;
            if(weaponBlockDef == null || !MyDefinitionManager.Static.TryGetWeaponDefinition(weaponBlockDef.WeaponDefinitionId, out weaponDef))
                return;

            bool canDrawLabel = CanDrawLabel();

            IMySlimBlock slimBlock = (Main.LockOverlay.LockedOnBlock ?? Main.EquipmentMonitor.AimedBlock);
            var weaponBlock = slimBlock?.FatBlock as IMyGunObject<MyGunBase>;

            IMyEntity muzzleEntity = null;
            bool hasMuzzles = (data.Muzzles != null && data.Muzzles.Count > 0);

            #region Accuracy cone
            if(hasMuzzles)
            {
                MyAmmoDefinition ammoDef = weaponBlock?.GunBase?.CurrentAmmoDefinition;
                if(ammoDef == null)
                {
                    var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(weaponDef.AmmoMagazinesId[0]);
                    ammoDef = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                }

                MatrixD barrelMatrix;
                if(weaponBlock != null)
                {
                    muzzleEntity = BData_Weapon.GetAimSubpart(slimBlock.FatBlock);
                    barrelMatrix = muzzleEntity.WorldMatrix;
                }
                else
                {
                    barrelMatrix = drawMatrix;
                }

                // for debugging barrel ent orientation
                //MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, Color.Red, barrelMatrix.Translation, barrelMatrix.Right, 3f, 0.005f, blendType: BlendTypeEnum.AdditiveTop);
                //MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, Color.Green, barrelMatrix.Translation, barrelMatrix.Up, 3f, 0.005f, blendType: BlendTypeEnum.AdditiveTop);
                //MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, Color.Blue, barrelMatrix.Translation, barrelMatrix.Forward, 3f, 0.005f, blendType: BlendTypeEnum.AdditiveTop);

                var md = data.Muzzles[0];
                MatrixD accMatrix = (muzzleEntity != null ? md.MatrixForSubpart : md.MatrixForBlock) * barrelMatrix;

                const float AccLineThick = 0.01f;
                const int ConeWireDivideRatio = 36;
                var accColor = new Color(255, 155, 0);

                float ammoRange = ammoDef.MaxTrajectory * weaponDef.RangeMultiplier;
                float projectileMinTravel = ammoRange * Hardcoded.Projectile_RangeMultiplier_Min;
                float projectileMaxTravel = ammoRange * Hardcoded.Projectile_RangeMultiplier_Max;
                bool randomizedRange = weaponDef.UseRandomizedRange;

                if(weaponDef.DeviateShotAngle > 0)
                {
                    float tanShotAngle = (float)Math.Tan(weaponDef.DeviateShotAngle);
                    float radiusAtMaxRange = tanShotAngle * projectileMaxTravel;
                    Utils.DrawTransparentCone(ref accMatrix, radiusAtMaxRange, projectileMaxTravel, ref accColor, MySimpleObjectRasterizer.Solid, ConeWireDivideRatio, lineThickness: AccLineThick, material: OVERLAY_SQUARE_MATERIAL, blendType: OVERLAY_BLEND_TYPE);

                    //var colorAtMinRange = Color.Lime.ToVector4();
                    //var radiusAtMinRange = tanShotAngle * projectileMinTravel;
                    //var circleMatrix = MatrixD.CreateWorld(accMatrix.Translation + accMatrix.Forward * projectileMinTravel, accMatrix.Down, accMatrix.Forward);
                    //MySimpleObjectDraw.DrawTransparentCylinder(ref circleMatrix, radiusAtMinRange, radiusAtMinRange, 0.1f, ref colorAtMinRange, true, ConeWireDivideRatio, 0.05f, OVERLAY_SQUARE_MATERIAL);
                }
                else
                {
                    MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, accColor, accMatrix.Translation, accMatrix.Forward, projectileMaxTravel, AccLineThick, blendType: OVERLAY_BLEND_TYPE);
                }

                //const float PointRadius = 0.025f;
                //MyTransparentGeometry.AddPointBillboard(OVERLAY_DOT_MATERIAL, accColor, accMatrix.Translation, PointRadius, 0, blendType: OVERLAY_BLEND_TYPE); // this is drawn always on top on purpose

                if(canDrawLabel)
                {
                    var labelDir = accMatrix.Up;
                    var labelLineStart = accMatrix.Translation + accMatrix.Forward * 3;

                    if(randomizedRange)
                        DynamicLabelSB.Clear().Append("Accuracy cone\n").DistanceRangeFormat(projectileMinTravel, projectileMaxTravel);
                    else
                        DynamicLabelSB.Clear().Append("Accuracy cone\n").DistanceFormat(projectileMaxTravel);

                    DrawLineLabel(TextAPIMsgIds.DynamicLabel, labelLineStart, labelDir, accColor);
                }
            }
            #endregion Accuracy cone

            #region Barrels
            if(hasMuzzles)
            {
                Vector4 barrelColor = Vector4.One; // white
                Vector4 flashColor = new Vector4(10, 10, 10, 1); // just like hand rifle

                if(muzzleEntity == null && weaponBlock != null)
                    muzzleEntity = BData_Weapon.GetAimSubpart(slimBlock.FatBlock);

                bool haveSubpart = (muzzleEntity != null);

                foreach(var md in data.Muzzles)
                {
                    MatrixD wm = (haveSubpart ? md.MatrixForSubpart * muzzleEntity.WorldMatrix : md.MatrixForBlock * drawMatrix);

                    MyTransparentGeometry.AddPointBillboard(OVERLAY_MUZZLEFLASH_MATERIAL, flashColor, wm.Translation, 0.15f, 0, blendType: BlendTypeEnum.AdditiveBottom);

                    float size = (md.Missile ? 0.06f : 0.025f);
                    float len = (md.Missile ? 2f : 5f);
                    MyTransparentGeometry.AddLineBillboard(OVERLAY_GRADIENT_MATERIAL, barrelColor, wm.Translation, wm.Forward, len, size, OVERLAY_BLEND_TYPE);
                }
            }
            #endregion Barrels

            #region Turret pitch/yaw limits
            var turretDef = def as MyLargeTurretBaseDefinition;
            bool isTurret = (turretDef != null && data.Turret != null);
            if(isTurret)
            {
                const int LineEveryDegrees = 15;
                const float lineThick = 0.03f;
                float radius = (def.Size * CellSizeHalf).AbsMin() + 1f;

                int minPitch = turretDef.MinElevationDegrees; // this one is actually not capped in game for whatever reason
                int maxPitch = Math.Min(turretDef.MaxElevationDegrees, 90); // can't pitch up more than 90deg

                int minYaw = turretDef.MinAzimuthDegrees;
                int maxYaw = turretDef.MaxAzimuthDegrees;

                // pitch limit indicator
                {
                    Color colorPitch = (Color.Red * 0.3f).ToVector4();
                    Vector4 colorPitchLine = Color.Red.ToVector4();

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
                    MatrixD m = MatrixD.CreateWorld(drawMatrix.Translation, Vector3D.Cross(pitchMatrix.Left, drawMatrix.Up), drawMatrix.Up);
                    Vector3D rotationPivot = Vector3D.Transform(data.Turret.PitchLocalPos, m);

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

                // yaw limit indicator
                {
                    Color colorYaw = (Color.Lime * 0.25f).ToVector4();
                    Vector4 colorYawLine = Color.Lime.ToVector4();

                    Vector3D rotationPivot = Vector3D.Transform(data.Turret.YawLocalPos, drawMatrix);

                    MatrixD yawMatrix = MatrixD.CreateWorld(rotationPivot, drawMatrix.Right, drawMatrix.Down);

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

                // camera position indicator
                {
                    Color colorCamera = new Color(55, 155, 255);
                    MatrixD turretCamMatrix = (muzzleEntity == null ? data.Turret.CameraForBlock * drawMatrix : data.Turret.CameraForSubpart * muzzleEntity.WorldMatrix);
                    MyTransparentGeometry.AddLineBillboard(OVERLAY_GRADIENT_MATERIAL, colorCamera, turretCamMatrix.Translation, turretCamMatrix.Forward, 3, 0.01f, OVERLAY_BLEND_TYPE);
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
            var data = Main.LiveDataHandler.Get<BData_ShipTool>(def);
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

                if(isWelder)
                    DrawLineLabel(TextAPIMsgIds.WeldingRadius, sphereEdge, labelDir, color, "Welding radius");
                else
                    DrawLineLabel(TextAPIMsgIds.GrindingRadius, sphereEdge, labelDir, color, "Grinding radius");
            }
        }

        private void DrawOverlay_Thruster(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var data = Main.LiveDataHandler.Get<BData_Thrust>(def);
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
            var data = Main.LiveDataHandler.Get<BData_LandingGear>(def);
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
            var data = Main.LiveDataHandler.Get<BData_Collector>(def);
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

            var data = Main.LiveDataHandler.Get<BData_LaserAntenna>(def);
            if(data == null)
                return;

            bool canDrawLabel = CanDrawLabel();
            IMySlimBlock slimBlock = (Main.LockOverlay.LockedOnBlock ?? Main.EquipmentMonitor.AimedBlock);

            const float LaserThick = 0.02f;
            const float LaserLength = 15f;

            const int LineEveryDegrees = 15;
            const float LineThick = 0.03f;
            float radius = (def.Size * CellSizeHalf).AbsMin() + 1f;

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

                    // from MyLaserAntenna.OnModelChange()
                    var subpartYaw = internalBlock.Subparts?.GetValueOrDefault("LaserComTurret", null);
                    var subpartPitch = subpartYaw?.Subparts?.GetValueOrDefault("LaserCom", null);

                    if(subpartPitch != null)
                    {
                        // NOTE: grid matrix because subpart is parented to grid, see MyLaserAntenna.SetParent()
                        pitchMatrix = subpartPitch.PositionComp.LocalMatrixRef * internalBlock.CubeGrid.WorldMatrix;
                        pitchMatrix.Translation = drawMatrix.Translation;
                    }
                }

                // only yaw rotation
                var m = MatrixD.CreateWorld(drawMatrix.Translation, Vector3D.Cross(pitchMatrix.Left, drawMatrix.Up), drawMatrix.Up);
                Vector3D rotationPivot = Vector3D.Transform(data.Turret.PitchLocalPos, m);

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

                Vector3D rotationPivot = Vector3D.Transform(data.Turret.YawLocalPos, drawMatrix);

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

        #region Draw ports
        const double DepthRatio = 0.01;

        private void DrawPortsMode(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var data = Main.LiveDataHandler.Get<BData_Base>(def);
            if(data == null)
                return;

            // since it's 3D text, these are in metric, per character.
            //float textScale = 0.2f + (Math.Max(0, (def.Size.AbsMax() - 5)) * 0.05f); // add text size for every cell the block is bigger than 8 (in largest axis only)
            //if(def.CubeSize == MyCubeSize.Small)
            //    textScale *= 0.25f; // tweaked text size for smallgrid
            //
            //textScale = MathHelper.Clamp(textScale, 0.01f, 0.5f);

            float textScale = (def.CubeSize == MyCubeSize.Large ? 0.6f : 0.4f);

            if(data.ConveyorPorts != null)
            {
                var color = new Color(255, 255, 0);

                foreach(var info in data.ConveyorPorts)
                {
                    var matrix = info.LocalMatrix * drawMatrix;

                    if((info.Flags & ConveyorFlags.Small) != 0)
                        DrawPort("       Small\nConveyor port", matrix, color);
                    else
                        DrawPort("       Large\nConveyor port", matrix, color, largeShip: true);
                }
            }

            if(data.InteractableConveyorPorts != null)
            {
                var color = new Color(155, 255, 0);

                foreach(var info in data.InteractableConveyorPorts)
                {
                    var matrix = info.LocalMatrix * drawMatrix;

                    if((info.Flags & ConveyorFlags.Small) != 0)
                        DrawPort("        Interactive\nSmall conveyor port", matrix, color);
                    else
                        DrawPort("        Interactive\nLarge conveyor port", matrix, color, largeShip: true);
                }
            }

            if(data.Interactive != null)
            {
                foreach(var info in data.Interactive)
                {
                    var matrix = info.LocalMatrix * drawMatrix;
                    DrawPort(info.Name, matrix, info.Color);
                }
            }

            if(data.UpgradePorts != null)
            {
                var color = new Color(200, 55, 255);

                foreach(var localMatrix in data.UpgradePorts)
                {
                    var matrix = localMatrix * drawMatrix;

                    DrawPort("Upgrade port", matrix, color);
                }
            }

            //if(!MyAPIGateway.Input.IsAnyShiftKeyPressed())
            //{
            //    if(data.Dummies != null)
            //    {
            //        foreach(var kv in data.Dummies)
            //        {
            //            var matrix = kv.Item2 * drawMatrix;
            //            DrawPort(kv.Item1, matrix, Color.Red);
            //        }
            //    }
            //}

            // NOTE: not using classic labels (one per color type) because some ports are small, others large...
            if(AimedPorts.Count > 0)
            {
                float scale = (float)(textScale * DepthRatio);

                PortInfo? closestRender = null;
                double closestDistance = double.MaxValue;

                foreach(var portInfo in AimedPorts)
                {
                    if(closestDistance > portInfo.Distance)
                    {
                        closestDistance = portInfo.Distance;
                        closestRender = portInfo;
                    }
                }

                if(closestRender.HasValue)
                {
                    var labelPos = closestRender.Value.Matrix.Translation;
                    var labelDir = closestRender.Value.Matrix.Right;

                    DynamicLabelSB.Clear().Append(closestRender.Value.Message);
                    DrawLineLabel(TextAPIMsgIds.DynamicLabel, labelPos, labelDir, Color.White, scale: scale, lineHeight: 0, lineThick: 0, align: HudAPIv2.TextOrientation.center, autoAlign: false, alwaysOnTop: false);
                }

                AimedPorts.Clear();
            }
        }

        readonly List<PortInfo> AimedPorts = new List<PortInfo>();

        private void DrawPort(string message, MatrixD portMatrix, Color color, bool largeShip = false)
        {
            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            float lineWidth = 0.01f;

            var closeRenderMatrix = portMatrix;

            // see through walls
            float scale = ConvertToAlwaysOnTop(ref closeRenderMatrix);
            lineWidth *= scale;

            var bb = new BoundingBoxD(-Vector3D.Half, Vector3D.Half);
            Color colorFace = color * 0.1f;
            Color colorLine = color;

            MySimpleObjectDraw.DrawTransparentBox(ref closeRenderMatrix, ref bb, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: OVERLAY_SQUARE_MATERIAL, blendType: OVERLAY_BLEND_TYPE);

            MySimpleObjectDraw.DrawTransparentBox(ref closeRenderMatrix, ref bb, ref colorLine, MySimpleObjectRasterizer.Wireframe, 1, lineWidth, lineMaterial: OVERLAY_LASER_MATERIAL, blendType: OVERLAY_BLEND_TYPE);

            // TODO: some kind of large conveyor indicator?
            //if(largeShip)
            //{
            //    var middleMatrix = closeRenderMatrix;
            //    var originalScale = middleMatrix.Scale;
            //    var scaleVec = Vector3D.One;
            //
            //    if(originalScale.Z < originalScale.X && originalScale.Z < originalScale.Y)
            //        scaleVec.Y = 0.05; // Z is thin, pick either Y or X
            //    else if(originalScale.X < originalScale.Y && originalScale.X < originalScale.Z)
            //        scaleVec.Z = 0.05; // X is thin, pick either Y or Z
            //    else if(originalScale.Y < originalScale.X && originalScale.Y < originalScale.Z)
            //        scaleVec.X = 0.05; // Y is thin, pick either X or Z
            //
            //    MatrixD.Rescale(ref middleMatrix, ref scaleVec);
            //    MySimpleObjectDraw.DrawTransparentBox(ref middleMatrix, ref bb, ref colorLine, MySimpleObjectRasterizer.Wireframe, 1, lineWidth, lineMaterial: OVERLAY_LASER_MATERIAL, blendType: OVERLAY_BLEND_TYPE);
            //}

            if(Main.TextAPI.IsEnabled)
            {
                var obb = new MyOrientedBoundingBoxD(portMatrix);
                RayD aimLine = new RayD(camMatrix.Translation, camMatrix.Forward);
                double? distance = obb.Intersects(ref aimLine);
                if(distance.HasValue)
                {
                    AimedPorts.Add(new PortInfo((float)distance.Value, closeRenderMatrix, color, message));
                }
            }
        }

        struct PortInfo
        {
            public readonly float Distance;
            public readonly MatrixD Matrix;
            public readonly Color Color;
            public readonly string Message;

            public PortInfo(float distance, MatrixD matrix, Color color, string message)
            {
                Distance = distance;
                Matrix = matrix;
                Color = color;
                Message = message;
            }
        }

        float ConvertToAlwaysOnTop(ref MatrixD matrix)
        {
            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D posOverlay = camMatrix.Translation + ((matrix.Translation - camMatrix.Translation) * DepthRatio);

            MatrixD.Rescale(ref matrix, DepthRatio);
            matrix.Translation = posOverlay;
            return (float)DepthRatio;
        }
        #endregion

        #region Draw helpers
        private StringBuilder DynamicLabelSB = new StringBuilder(128);

        private void DrawLineLabel(TextAPIMsgIds id, Vector3D start, Vector3D direction, Color color,
            string message = null, float scale = 1f, float lineHeight = 0.5f, float lineThick = 0.005f,
            OverlayLabelsFlags settingFlag = OverlayLabelsFlags.Other, HudAPIv2.TextOrientation align = HudAPIv2.TextOrientation.ltr,
            bool autoAlign = true, bool alwaysOnTop = false)
        {
            var cm = MyAPIGateway.Session.Camera.WorldMatrix;

            if(alwaysOnTop)
            {
                start = cm.Translation + ((start - cm.Translation) * DepthRatio);
                scale = scale * (float)DepthRatio;
            }

            lineHeight *= scale;
            lineThick *= scale;

            Vector3D shadowOffset = cm.Right * (LABEL_SHADOW_OFFSET.X * scale) + cm.Up * (LABEL_SHADOW_OFFSET.Y * scale);

            MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, LABEL_SHADOW_COLOR, start + shadowOffset, direction, lineHeight, lineThick, LABEL_SHADOW_BLEND_TYPE);
            MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, color, start, direction, lineHeight, lineThick, LABEL_BLEND_TYPE);

            if(!Main.Config.OverlayLabels.IsSet(settingFlag) && !(Main.Config.OverlaysLabelsAlt.Value && MyAPIGateway.Input.IsAnyAltKeyPressed()))
                return;

            var textWorldPos = start + direction * lineHeight;

            // has issue on always-on-top labels
            //if(!alwaysOnTop)
            //{
            //    var sphere = new BoundingSphereD(textWorldPos, 0.01f);
            //    if(!MyAPIGateway.Session.Camera.IsInFrustum(ref sphere))
            //        return;
            //}

            Vector3D textDir = cm.Right;
            if(autoAlign && textDir.Dot(direction) <= -0.8f)
            {
                textDir = cm.Left;
                if(align == HudAPIv2.TextOrientation.ltr)
                    align = HudAPIv2.TextOrientation.rtl;
            }

            var textPos = textWorldPos + textDir * (LABEL_OFFSET.X * scale) + cm.Up * (LABEL_OFFSET.Y * scale);
            var shadowPos = textPos + textDir * (LABEL_SHADOW_OFFSET.X * scale) + cm.Up * (LABEL_SHADOW_OFFSET.Y * scale) + cm.Forward * 0.0001;

            var i = (int)id;
            var labelData = Labels[i];
            if(labelData == null)
                Labels[i] = labelData = new LabelData();

            if(labelData.Text == null)
            {
                var shadowSB = new StringBuilder(DynamicLabelSB.Capacity);
                var msgSB = new StringBuilder(DynamicLabelSB.Capacity);

                labelData.Shadow = new HudAPIv2.SpaceMessage(shadowSB, Vector3D.Zero, Vector3D.Up, Vector3D.Left, LABEL_TEXT_SCALE, Blend: LABEL_SHADOW_BLEND_TYPE);
                labelData.Text = new HudAPIv2.SpaceMessage(msgSB, Vector3D.Zero, Vector3D.Up, Vector3D.Left, LABEL_TEXT_SCALE, Blend: LABEL_BLEND_TYPE);

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
                shadow.Message.Clear().Color(LABEL_SHADOW_COLOR).AppendStringBuilder(DynamicLabelSB);
                text.Message.Clear().Color(color).AppendStringBuilder(DynamicLabelSB);

                labelData.UnderlineLength = GetLabelUnderlineLength(text);
            }

            shadow.TxtOrientation = align;
            shadow.Scale = scale * LABEL_TEXT_SCALE;
            shadow.WorldPosition = shadowPos;
            shadow.Left = cm.Left;
            shadow.Up = cm.Up;
            shadow.Draw(); // this removes the need of having the text visible, also draws text more accurately to my position

            text.TxtOrientation = align;
            text.Scale = scale * LABEL_TEXT_SCALE;
            text.WorldPosition = textPos;
            text.Left = cm.Left;
            text.Up = cm.Up;
            text.Draw();

            var underlineLength = labelData.UnderlineLength * scale;
            MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, LABEL_SHADOW_COLOR, textWorldPos + shadowOffset, textDir, underlineLength, lineThick, LABEL_SHADOW_BLEND_TYPE);
            MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, color, textWorldPos, textDir, underlineLength, lineThick, LABEL_BLEND_TYPE);

            AnyLabelShown = true;
        }

        private float GetLabelUnderlineLength(HudAPIv2.SpaceMessage msg)
        {
            var textSize = msg.GetTextLength();
            return (float)(LABEL_OFFSET.X + (textSize.X * LABEL_TEXT_SCALE));
        }

        private void DrawMountPointAxisText(MyCubeBlockDefinition def, ref MatrixD drawMatrix)
        {
            float textScale = (def.CubeSize == MyCubeSize.Large ? 1.5f : 0.75f);

            var matrix = MatrixD.CreateScale(def.Size * (CellSize / textScale / 2f) + new Vector3D(0.5f));
            matrix.Translation = Vector3D.Zero; // (def.Center - (def.Size * 0.5f));
            matrix = matrix * drawMatrix;

            bool alwaysOnTop = (DrawOverlay == 3);

            DrawLineLabel(TextAPIMsgIds.AxisX, matrix.Translation, matrix.Right, Color.Red, message: "Right",
                lineHeight: 1f, scale: textScale, settingFlag: OverlayLabelsFlags.Axis, autoAlign: true, alwaysOnTop: alwaysOnTop);

            DrawLineLabel(TextAPIMsgIds.AxisY, matrix.Translation, matrix.Up, Color.Lime, message: "Up",
                lineHeight: 1f, scale: textScale, settingFlag: OverlayLabelsFlags.Axis, autoAlign: true, alwaysOnTop: alwaysOnTop);

            DrawLineLabel(TextAPIMsgIds.AxisZ, matrix.Translation, matrix.Forward, Color.Blue, message: "Forward",
                lineHeight: 1f, scale: textScale, settingFlag: OverlayLabelsFlags.Axis, autoAlign: true, alwaysOnTop: alwaysOnTop);
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
