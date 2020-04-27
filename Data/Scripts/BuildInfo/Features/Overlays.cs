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
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features
{
    public class Overlays : ModComponent
    {
        public int drawOverlay = 0;
        private bool anyLabelShown;
        private bool doorAirtightBlink = false;
        private int doorAirtightBlinkTick = 0;
        private bool blockFunctionalForPressure;
        private IMyHudNotification overlayNotification;
        private OverlayCall selectedOverlayCall;
        private readonly LabelData[] labels;

        class LabelData
        {
            public HudAPIv2.SpaceMessage Text;
            public HudAPIv2.SpaceMessage Shadow;
            public float UnderlineLength = -1;
        }

        private readonly MyCubeBlockDefinition.MountPoint[] BLANK_MOUNTPOINTS = new MyCubeBlockDefinition.MountPoint[0];

        public delegate void OverlayCall(MyCubeBlockDefinition def, MatrixD drawMatrix);
        public readonly Dictionary<MyObjectBuilderType, OverlayCall> drawLookup
                  = new Dictionary<MyObjectBuilderType, OverlayCall>(MyObjectBuilderType.Comparer);

        #region Constants
        private const BlendTypeEnum OVERLAY_BLEND_TYPE = BlendTypeEnum.PostPP;
        public readonly MyStringId OVERLAY_SQUARE_MATERIAL = MyStringId.GetOrCompute("BuildInfo_Square");
        public readonly MyStringId OVERLAY_DOT_MATERIAL = MyStringId.GetOrCompute("WhiteDot");

        private const BlendTypeEnum MOUNTPOINT_BLEND_TYPE = BlendTypeEnum.SDR;
        private const double MOUNTPOINT_THICKNESS = 0.05;
        private const float MOUNTPOINT_ALPHA = 0.65f;
        private Color MOUNTPOINT_COLOR = new Color(255, 255, 0) * MOUNTPOINT_ALPHA;
        private Color MOUNTPOINT_DEFAULT_COLOR = new Color(255, 200, 0) * MOUNTPOINT_ALPHA;
        private Color AIRTIGHT_COLOR = new Color(0, 155, 255) * MOUNTPOINT_ALPHA;
        private Color AIRTIGHT_TOGGLE_COLOR = new Color(0, 255, 155) * MOUNTPOINT_ALPHA;

        private const double LABEL_TEXT_SCALE = 0.24;
        private readonly Vector2D LABEL_OFFSET = new Vector2D(0.1, 0.1);
        private const BlendTypeEnum LABEL_BLEND_TYPE = BlendTypeEnum.PostPP;
        private const BlendTypeEnum LABEL_SHADOW_BLEND_TYPE = BlendTypeEnum.PostPP;
        private readonly Color LABEL_SHADOW_COLOR = Color.Black * 0.9f;
        private readonly Vector2D LABEL_SHADOW_OFFSET = new Vector2D(0.01, -0.01);

        private BoundingBoxD unitBB = new BoundingBoxD(Vector3D.One / -2d, Vector3D.One / 2d);

        public readonly string[] NAMES = new string[]
        {
            "OFF",
            "Airtightness",
            "Mounting",
        };

        private readonly string[] AXIS_LABELS = new string[]
        {
            "Forward",
            "Right",
            "Up",
        };

        private enum TextAPIMsgIds
        {
            AXIS_Z, // NOTE these 3 must remain the first 3, because AXIS_LABELS uses their integer values as indexes
            AXIS_X,
            AXIS_Y,
            //MOUNT,
            //MOUNT_ROTATE,
            //MOUNT_DISABLED,
            DRILL_MINE,
            DRILL_CUTOUT,
            SHIP_TOOL,
            ACCURACY_MAX,
            //ACCURACY_100M,
            DOOR_AIRTIGHT,
            THRUST_DAMAGE,
            MAGNET,
            COLLECTOR,
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
        #endregion Constants

        public Overlays(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.NONE;

            int count = Enum.GetValues(typeof(TextAPIMsgIds)).Length;
            labels = new LabelData[count];
        }

        protected override void RegisterComponent()
        {
            InitLookups();

            GameConfig.HudStateChanged += GameConfig_HudStateChanged;
            EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
            EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
            EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;
        }

        protected override void UnregisterComponent()
        {
            GameConfig.HudStateChanged -= GameConfig_HudStateChanged;
            EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
            EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
            EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;
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
            selectedOverlayCall = null;

            if(def != null)
                selectedOverlayCall = drawLookup.GetValueOrDefault(def.Id.TypeId, null);

            HideLabels();
        }

        private void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            if(shipController != null && EquipmentMonitor.IsBuildTool && drawOverlay > 0)
            {
                const BlendTypeEnum BLEND_TYPE = BlendTypeEnum.SDR;
                const float REACH_DISTANCE = Hardcoded.ShipTool_ReachDistance;
                var color = new Vector4(2f, 0, 0, 0.1f); // above 1 color creates bloom
                var shipCtrlDef = (MyShipControllerDefinition)shipController.SlimBlock.BlockDefinition;

                var m = shipController.WorldMatrix;
                m.Translation = Vector3D.Transform(shipCtrlDef.RaycastOffset, m);

                MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, color, m.Translation, m.Forward, REACH_DISTANCE, 0.005f, blendType: BLEND_TYPE);
                MyTransparentGeometry.AddPointBillboard(OVERLAY_DOT_MATERIAL, color, m.Translation + m.Forward * REACH_DISTANCE, 0.015f, 0f, blendType: BLEND_TYPE);
            }
        }

        public void CycleOverlayMode(bool showNotification = true)
        {
            if(++drawOverlay >= NAMES.Length)
                drawOverlay = 0;

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, drawOverlay > 0);
            HideLabels();

            if(showNotification)
            {
                if(overlayNotification == null)
                    overlayNotification = MyAPIGateway.Utilities.CreateNotification("", 2000, MyFontEnum.White);

                overlayNotification.Hide(); // required since SE v1.194
                overlayNotification.Text = "Overlays: " + NAMES[drawOverlay];
                overlayNotification.Show();
            }
        }

        private void InitLookups()
        {
            Add(typeof(MyObjectBuilder_ShipWelder), DrawOverlay_ShipTool);
            Add(typeof(MyObjectBuilder_ShipGrinder), DrawOverlay_ShipTool);

            Add(typeof(MyObjectBuilder_Drill), DrawOverlay_Drill);

            Add(typeof(MyObjectBuilder_ConveyorSorter), DrawOverlay_WeaponCoreWeapon);
            Add(typeof(MyObjectBuilder_SmallGatlingGun), DrawOverlay_Weapons);
            Add(typeof(MyObjectBuilder_SmallMissileLauncher), DrawOverlay_Weapons);
            Add(typeof(MyObjectBuilder_SmallMissileLauncherReload), DrawOverlay_Weapons);
            Add(typeof(MyObjectBuilder_LargeGatlingTurret), DrawOverlay_Weapons);
            Add(typeof(MyObjectBuilder_LargeMissileTurret), DrawOverlay_Weapons);
            Add(typeof(MyObjectBuilder_InteriorTurret), DrawOverlay_Weapons);

            Add(typeof(MyObjectBuilder_Door), DrawOverlay_Doors);
            Add(typeof(MyObjectBuilder_AdvancedDoor), DrawOverlay_Doors);
            Add(typeof(MyObjectBuilder_AirtightDoorGeneric), DrawOverlay_Doors);
            Add(typeof(MyObjectBuilder_AirtightHangarDoor), DrawOverlay_Doors);
            Add(typeof(MyObjectBuilder_AirtightSlideDoor), DrawOverlay_Doors);

            Add(typeof(MyObjectBuilder_Thrust), DrawOverlay_Thruster);

            Add(typeof(MyObjectBuilder_LandingGear), DrawOverlay_LandingGear);

            Add(typeof(MyObjectBuilder_Collector), DrawOverlay_Collector);
        }

        private void Add(MyObjectBuilderType blockType, OverlayCall call)
        {
            drawLookup.Add(blockType, call);
        }

        // boxless HitInfo getters
        IMyEntity GetHitEnt<T>(T val) where T : IHitInfo => val.HitEntity;
        Vector3D GetHitPos<T>(T val) where T : IHitInfo => val.Position;

        protected override void UpdateDraw()
        {
            if(drawOverlay == 0 || EquipmentMonitor.BlockDef == null || (GameConfig.HudState == HudState.OFF && !Config.OverlaysAlwaysVisible.Value))
                return;

            // TODO: show currently selected overlay mode? maybe only with textAPI?
            //overlayNotification.Hide(); // required since SE v1.194
            //overlayNotification.Text = $"Showing {DRAW_OVERLAY_NAME[drawOverlay]} overlays (Ctrl+{voxelHandSettingsInput} to cycle)";
            //overlayNotification.AliveTime = 32;
            //overlayNotification.Show();

            var def = EquipmentMonitor.BlockDef;
            var aimedBlock = EquipmentMonitor.AimedBlock;

            try
            {
                #region DrawMatrix and other needed data
                var drawMatrix = MatrixD.Identity;

                if(EquipmentMonitor.IsCubeBuilder)
                {
                    if(MyAPIGateway.Session.IsCameraUserControlledSpectator && !Utils.CreativeToolsEnabled)
                        return;

                    var box = MyCubeBuilder.Static.GetBuildBoundingBox();
                    drawMatrix = MatrixD.CreateFromQuaternion(box.Orientation);

                    if(MyCubeBuilder.Static.DynamicMode && MyCubeBuilder.Static.HitInfo.HasValue)
                    {
                        var hitEnt = GetHitEnt(MyCubeBuilder.Static.HitInfo.Value);

                        if(hitEnt != null && hitEnt is IMyVoxelBase)
                            drawMatrix.Translation = GetHitPos(MyCubeBuilder.Static.HitInfo.Value); // required for position to be accurate when aiming at a planet
                        else
                            drawMatrix.Translation = MyCubeBuilder.Static.FreePlacementTarget; // required for the position to be accurate when the block is not aimed at anything
                    }
                    else
                    {
                        drawMatrix.Translation = box.Center;
                    }
                }
                else // using welder/grinder
                {
                    Matrix m;
                    Vector3D center;
                    aimedBlock.Orientation.GetMatrix(out m);
                    aimedBlock.ComputeWorldCenter(out center);

                    drawMatrix = m * aimedBlock.CubeGrid.WorldMatrix;
                    drawMatrix.Translation = center;
                }
                #endregion DrawMatrix and other needed data

                #region Draw mount points
                var cellSize = EquipmentMonitor.BlockGridSize;

                if(TextAPIEnabled)
                {
                    DrawMountPointAxixText(def, cellSize, ref drawMatrix);
                }
                else
                {
                    // re-assigning mount points temporarily to prevent the original mountpoint wireframe from being drawn while keeping the axis information
                    var mp = def.MountPoints;
                    def.MountPoints = BLANK_MOUNTPOINTS;
                    MyCubeBuilder.DrawMountPoints(cellSize, def, ref drawMatrix);
                    def.MountPoints = mp;
                }

                blockFunctionalForPressure = true;

                // HACK condition matching the condition in MyGridGasSystem.IsAirtightFromDefinition()
                if(EquipmentMonitor.AimedProjectedBy == null && aimedBlock != null && def.BuildProgressModels != null && def.BuildProgressModels.Length > 0)
                {
                    var progressModel = def.BuildProgressModels[def.BuildProgressModels.Length - 1];

                    if(aimedBlock.BuildLevelRatio < progressModel.BuildRatioUpperBound)
                        blockFunctionalForPressure = false;
                }

                // draw custom mount point styling
                {
                    var minSize = (def.CubeSize == MyCubeSize.Large ? 0.05 : 0.02); // a minimum size to have some thickness
                    var center = def.Center;
                    var mainMatrix = MatrixD.CreateTranslation((center - (def.Size * 0.5f)) * cellSize) * drawMatrix;
                    var mountPoints = def.GetBuildProgressModelMountPoints(1f);
                    bool drawLabel = Config.OverlayLabels.IsSet(OverlayLabelsFlags.Other) && TextAPIEnabled;

                    if(drawOverlay == 1 && blockFunctionalForPressure)
                    {
                        // TODO: have a note saying that blocks that aren't fully built are always not airtight? (blockFunctionalForPressure)

                        if(def.IsAirTight.HasValue)
                        {
                            if(def.IsAirTight.Value)
                            {
                                var halfExtents = def.Size * (cellSize * 0.5);
                                var localBB = new BoundingBoxD(-halfExtents, halfExtents).Inflate(MOUNTPOINT_THICKNESS * 0.5);
                                MySimpleObjectDraw.DrawTransparentBox(ref drawMatrix, ref localBB, ref AIRTIGHT_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: OVERLAY_SQUARE_MATERIAL, blendType: MOUNTPOINT_BLEND_TYPE);
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

                                    MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_COLOR, ref AIRTIGHT_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: OVERLAY_SQUARE_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                                }
                            }
                        }
                    }
                    else if(drawOverlay == 2 && mountPoints != null)
                    {
                        for(int i = 0; i < mountPoints.Length; i++)
                        {
                            var mountPoint = mountPoints[i];

                            if(!mountPoint.Enabled)
                                continue; // ignore all disabled mount points as airtight ones are rendered separate

                            var colorFace = (mountPoint.Default ? MOUNTPOINT_DEFAULT_COLOR : MOUNTPOINT_COLOR);

                            DrawMountPoint(mountPoint, cellSize, ref center, ref mainMatrix, ref colorFace, minSize);
                        }
                    }
                }
                #endregion Draw mount points

                // draw per-block overlays
                selectedOverlayCall?.Invoke(def, drawMatrix);

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
            if(!anyLabelShown)
                return;

            anyLabelShown = false;

            for(int i = 0; i < labels.Length; ++i)
            {
                var label = labels[i];

                if(label != null && label.Text != null)
                {
                    label.Text.Visible = false;
                    label.Shadow.Visible = false;
                }
            }
        }

        #region Block-specific overlays
        private void DrawOverlay_Doors(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            if(drawOverlay != 1)
                return;

            if(!blockFunctionalForPressure)
            {
                HideLabels();
                return;
            }

            if(EquipmentMonitor.AimedBlock != null)
            {
                doorAirtightBlink = true;
            }
            else if(!MyParticlesManager.Paused && ++doorAirtightBlinkTick >= 60)
            {
                doorAirtightBlinkTick = 0;
                doorAirtightBlink = !doorAirtightBlink;
            }

            var cubeSize = def.Size * (EquipmentMonitor.BlockGridSize * 0.5f);
            bool drawLabel = Config.OverlayLabels.IsSet(OverlayLabelsFlags.Other) && TextAPIEnabled;

            if(!drawLabel && !doorAirtightBlink)
                return;

            bool fullyClosed = true;

            if(EquipmentMonitor.AimedBlock != null)
            {
                var door = EquipmentMonitor.AimedBlock.FatBlock as IMyDoor;

                if(door != null)
                    fullyClosed = (door.Status == Sandbox.ModAPI.Ingame.DoorStatus.Closed);
            }

            for(int i = 0; i < 6; ++i)
            {
                var normal = DIRECTIONS[i];
                var normalI = (Vector3I)normal;

                if(Pressurization.IsDoorAirtight(def, ref normalI, fullyClosed))
                {
                    var dirForward = Vector3D.TransformNormal(normal, drawMatrix);
                    var dirLeft = Vector3D.TransformNormal(DIRECTIONS[((i + 4) % 6)], drawMatrix);
                    var dirUp = Vector3D.TransformNormal(DIRECTIONS[((i + 2) % 6)], drawMatrix);

                    var pos = drawMatrix.Translation + dirForward * cubeSize.GetDim((i % 6) / 2);
                    float width = cubeSize.GetDim(((i + 4) % 6) / 2);
                    float height = cubeSize.GetDim(((i + 2) % 6) / 2);

                    if(doorAirtightBlink)
                    {
                        var m = MatrixD.CreateWorld(pos, dirForward, dirUp);
                        m.Right *= width * 2;
                        m.Up *= height * 2;
                        m.Forward *= MOUNTPOINT_THICKNESS;
                        MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_TOGGLE_COLOR, ref AIRTIGHT_TOGGLE_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: OVERLAY_SQUARE_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                    }

                    if(drawLabel) // only label the first one
                    {
                        drawLabel = false;

                        var labelPos = pos + dirLeft * width + dirUp * height;
                        DrawLineLabel(TextAPIMsgIds.DOOR_AIRTIGHT, labelPos, dirLeft, AIRTIGHT_TOGGLE_COLOR, message: "Airtight when closed");

                        if(!doorAirtightBlink) // no need to iterate further if no faces need to be rendered
                            break;
                    }
                }
            }

            var mountPoints = def.GetBuildProgressModelMountPoints(1f);

            if(mountPoints != null)
            {
                var cellSize = EquipmentMonitor.BlockGridSize;
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

                        if(doorAirtightBlink)
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

                            MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_TOGGLE_COLOR, ref AIRTIGHT_TOGGLE_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: OVERLAY_SQUARE_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                        }

                        if(drawLabel) // only label the first one
                        {
                            drawLabel = false;

                            var dirLeft = Vector3D.TransformNormal(DIRECTIONS[((dirIndex + 4) % 6)], drawMatrix);
                            float width = cubeSize.GetDim(((dirIndex + 4) % 6) / 2);
                            float height = cubeSize.GetDim(((dirIndex + 2) % 6) / 2);

                            var labelPos = pos + dirLeft * width + dirUp * height;
                            DrawLineLabel(TextAPIMsgIds.DOOR_AIRTIGHT, labelPos, dirLeft, AIRTIGHT_TOGGLE_COLOR, message: "Airtight when closed");

                            if(!doorAirtightBlink) // no need to iterate further if no faces need to be rendered
                                break;
                        }
                    }
                }
            }

            if(drawLabel) // no label was rendered since it would've set itself false by now
            {
                HideLabels();
            }
        }

        private void DrawOverlay_Weapons(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            if(WeaponCoreAPIHandler.IsBlockWeapon(def.Id))
            {
                DrawOverlay_WeaponCoreWeapon(def, drawMatrix);
                return;
            }

            var data = BData_Base.TryGetDataCached<BData_Weapon>(def);

            if(data == null)
                return;

            const int wireDivideRatio = 12;
            const float lineHeight = 0.5f;
            var color = Color.Red;
            var colorFace = color * 0.5f;
            var weapon = (MyWeaponBlockDefinition)def;
            var wepDef = MyDefinitionManager.Static.GetWeaponDefinition(weapon.WeaponDefinitionId);
            MyAmmoDefinition ammo = null;

            if(EquipmentMonitor.AimedBlock != null)
            {
                var weaponBlock = EquipmentMonitor.AimedBlock.FatBlock as IMyGunObject<MyGunBase>;

                if(weaponBlock != null)
                    ammo = weaponBlock.GunBase.CurrentAmmoDefinition;
            }

            if(ammo == null)
            {
                var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wepDef.AmmoMagazinesId[0]);
                ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
            }

            var height = ammo.MaxTrajectory;
            var tanShotAngle = (float)Math.Tan(wepDef.DeviateShotAngle);
            var accuracyAtMaxRange = tanShotAngle * (height * 2);
            var coneMatrix = data.muzzleLocalMatrix * drawMatrix;

            MyTransparentGeometry.AddPointBillboard(OVERLAY_DOT_MATERIAL, color, coneMatrix.Translation, 0.025f, 0, blendType: OVERLAY_BLEND_TYPE); // this is drawn always on top on purpose
            MySimpleObjectDraw.DrawTransparentCone(ref coneMatrix, accuracyAtMaxRange, height, ref colorFace, wireDivideRatio, faceMaterial: OVERLAY_SQUARE_MATERIAL);

            //const int circleWireDivideRatio = 20;
            //var accuracyAt100m = tanShotAngle * (100 * 2);
            //var color100m = Color.Green.ToVector4();
            //var circleMatrix = MatrixD.CreateWorld(coneMatrix.Translation + coneMatrix.Forward * 3 + coneMatrix.Left * 3, coneMatrix.Down, coneMatrix.Forward);
            //MySimpleObjectDraw.DrawTransparentCylinder(ref circleMatrix, accuracyAt100m, accuracyAt100m, 0.1f, ref color100m, true, circleWireDivideRatio, 0.05f, MATERIAL_SQUARE);

            if(Config.OverlayLabels.IsSet(OverlayLabelsFlags.Other) && TextAPIEnabled)
            {
                var labelDir = coneMatrix.Up;
                var labelLineStart = coneMatrix.Translation + coneMatrix.Forward * 3;

                LabelTextBuilder().Append("Accuracy cone - ").Append(height).Append(" m");
                DrawLineLabel(TextAPIMsgIds.ACCURACY_MAX, labelLineStart, labelDir, color, lineHeight: lineHeight);

                //var lineStart = circleMatrix.Translation + coneMatrix.Down * accuracyAt100m;
                //var labelStart = lineStart + coneMatrix.Down * 0.3f;
                //DrawLineLabelAlternate(TextAPIMsgIds.ACCURACY_100M, lineStart, labelStart, "At 100m (zoomed)", color100m, underlineLength: 1.5f);
            }
        }

        private void DrawOverlay_WeaponCoreWeapon(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            if(WeaponCoreAPIHandler.IsBlockWeapon(def.Id))
            {
                // TODO: weaponcore overlays?
            }
        }

        private void DrawOverlay_Drill(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var drill = (MyShipDrillDefinition)def;

            const float lineHeight = 0.3f;
            const int wireDivRatio = 20;
            var colorMine = Color.Lime;
            var colorMineFace = colorMine * 0.3f;
            var colorCut = Color.Red;
            var colorCutFace = colorCut * 0.3f;
            bool drawLabels = Config.OverlayLabels.IsSet(OverlayLabelsFlags.Other) && TextAPIEnabled;

            drawMatrix.Translation += drawMatrix.Forward * drill.SensorOffset;
            MySimpleObjectDraw.DrawTransparentSphere(ref drawMatrix, drill.SensorRadius, ref colorMineFace, MySimpleObjectRasterizer.Solid, wireDivRatio, faceMaterial: OVERLAY_SQUARE_MATERIAL);

            bool showCutOut = (Math.Abs(drill.SensorRadius - drill.CutOutRadius) > 0.0001f || Math.Abs(drill.SensorOffset - drill.CutOutOffset) > 0.0001f);

            if(drawLabels)
            {
                var labelDir = drawMatrix.Down;
                var sphereEdge = drawMatrix.Translation + (labelDir * drill.SensorRadius);

                LabelTextBuilder().Append(showCutOut ? "Mining radius" : "Mining/cutout radius");
                DrawLineLabel(TextAPIMsgIds.DRILL_MINE, sphereEdge, labelDir, colorMine, lineHeight: lineHeight);
            }

            if(showCutOut)
            {
                var cutMatrix = drawMatrix;
                cutMatrix.Translation += cutMatrix.Forward * drill.CutOutOffset;
                MySimpleObjectDraw.DrawTransparentSphere(ref cutMatrix, drill.CutOutRadius, ref colorCutFace, MySimpleObjectRasterizer.Solid, wireDivRatio, faceMaterial: OVERLAY_SQUARE_MATERIAL);

                if(drawLabels)
                {
                    var labelDir = cutMatrix.Left;
                    var sphereEdge = cutMatrix.Translation + (labelDir * drill.CutOutRadius);
                    DrawLineLabel(TextAPIMsgIds.DRILL_CUTOUT, sphereEdge, labelDir, colorCut, message: "Cutout radius", lineHeight: lineHeight);
                }
            }
        }

        private void DrawOverlay_ShipTool(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var data = BData_Base.TryGetDataCached<BData_ShipTool>(def);

            if(data == null)
                return;

            const float lineHeight = 0.3f;
            const int wireDivRatio = 20;
            var color = Color.Lime;
            var colorFace = color * 0.3f;

            var toolDef = (MyShipToolDefinition)def;
            var matrix = data.DummyMatrix;
            var sensorCenter = matrix.Translation + matrix.Forward * toolDef.SensorOffset;
            drawMatrix.Translation = Vector3D.Transform(sensorCenter, drawMatrix);
            var radius = toolDef.SensorRadius;

            MySimpleObjectDraw.DrawTransparentSphere(ref drawMatrix, radius, ref colorFace, MySimpleObjectRasterizer.Solid, wireDivRatio, faceMaterial: OVERLAY_SQUARE_MATERIAL);

            if(Config.OverlayLabels.IsSet(OverlayLabelsFlags.Other) && TextAPIEnabled)
            {
                bool isWelder = def is MyShipWelderDefinition;
                var labelDir = drawMatrix.Down;
                var sphereEdge = drawMatrix.Translation + (labelDir * radius);

                LabelTextBuilder().Append(isWelder ? "Welding radius" : "Grinding radius");
                DrawLineLabel(TextAPIMsgIds.SHIP_TOOL, sphereEdge, labelDir, color, lineHeight: lineHeight);
            }
        }

        private void DrawOverlay_Thruster(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var data = BData_Base.TryGetDataCached<BData_Thrust>(def);

            if(data == null)
                return;

            const float capsuleRadiusAdd = 0.05f; // so it visually hits things more how the physics engine hits.
            const int wireDivideRatio = 12;
            const float lineHeight = 0.3f;
            var color = Color.Red;
            var colorFace = color * 0.5f;
            var capsuleMatrix = MatrixD.CreateWorld(Vector3D.Zero, drawMatrix.Up, drawMatrix.Backward); // capsule is rotated weirdly (pointing up), needs adjusting
            bool drawLabel = Config.OverlayLabels.IsSet(OverlayLabelsFlags.Other) && TextAPIEnabled;

            foreach(var flame in data.Flames)
            {
                var start = Vector3D.Transform(flame.LocalFrom, drawMatrix);
                capsuleMatrix.Translation = start + (drawMatrix.Forward * (flame.Length * 0.5)); // capsule's position is in the center

                MySimpleObjectDraw.DrawTransparentCapsule(ref capsuleMatrix, flame.CapsuleRadius + capsuleRadiusAdd, flame.Length, ref colorFace, wireDivideRatio, OVERLAY_SQUARE_MATERIAL);

                if(drawLabel)
                {
                    drawLabel = false; // label only on the first flame
                    var labelDir = drawMatrix.Down;
                    var labelLineStart = Vector3D.Transform(flame.LocalTo, drawMatrix) + labelDir * flame.Radius;
                    DrawLineLabel(TextAPIMsgIds.THRUST_DAMAGE, labelLineStart, labelDir, color, message: "Thrust damage", lineHeight: lineHeight);
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
            bool drawLabel = Config.OverlayLabels.IsSet(OverlayLabelsFlags.Other) && TextAPIEnabled;

            foreach(var obb in data.Magents)
            {
                var localBB = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);
                var m = MatrixD.CreateFromQuaternion(obb.Orientation);
                m.Translation = obb.Center;
                m *= drawMatrix;

                MySimpleObjectDraw.DrawTransparentBox(ref m, ref localBB, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: OVERLAY_SQUARE_MATERIAL, blendType: MOUNTPOINT_BLEND_TYPE);

                if(drawLabel)
                {
                    drawLabel = false; // only label the first one
                    var labelDir = drawMatrix.Down;
                    var labelLineStart = m.Translation + (m.Down * localBB.HalfExtents.Y) + (m.Backward * localBB.HalfExtents.Z) + (m.Left * localBB.HalfExtents.X);
                    DrawLineLabel(TextAPIMsgIds.MAGNET, labelLineStart, labelDir, color, message: "Magnet", lineHeight: 0.5f);
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
            bool drawLabel = Config.OverlayLabels.IsSet(OverlayLabelsFlags.Other) && TextAPIEnabled;

            var localBB = new BoundingBoxD(-Vector3.Half, Vector3.Half);
            var m = data.boxLocalMatrix * drawMatrix;

            MySimpleObjectDraw.DrawTransparentBox(ref m, ref localBB, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: OVERLAY_SQUARE_MATERIAL, blendType: MOUNTPOINT_BLEND_TYPE);

            if(drawLabel)
            {
                var labelDir = drawMatrix.Down;
                var labelLineStart = m.Translation + (m.Down * localBB.HalfExtents.Y) + (m.Backward * localBB.HalfExtents.Z) + (m.Left * localBB.HalfExtents.X);
                DrawLineLabel(TextAPIMsgIds.COLLECTOR, labelLineStart, labelDir, color, message: "Collection Area", lineHeight: 0.5f);
            }
        }
        #endregion Block-specific overlays

        #region Draw helpers
        private StringBuilder label = new StringBuilder(128);
        private StringBuilder LabelTextBuilder() => label.Clear();

        private void DrawLineLabel(TextAPIMsgIds id, Vector3D start, Vector3D direction, Color color, string message = null, float lineHeight = 0.3f, float lineThick = 0.005f)
        {
            var cm = MyAPIGateway.Session.Camera.WorldMatrix;
            var offset = cm.Right * LABEL_SHADOW_OFFSET.X + cm.Up * LABEL_SHADOW_OFFSET.Y;

            MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, LABEL_SHADOW_COLOR, start + offset, direction, lineHeight, lineThick, LABEL_SHADOW_BLEND_TYPE);
            MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, color, start, direction, lineHeight, lineThick, LABEL_BLEND_TYPE);

            if(Config.OverlayLabels.IsSet(OverlayLabelsFlags.Axis))
            {
                var textWorldPos = start + direction * lineHeight;
                anyLabelShown = true;

                var i = (int)id;
                var labelData = labels[i];

                if(labelData == null)
                    labels[i] = labelData = new LabelData();

                if(labelData.Text == null)
                {
                    var shadowSB = new StringBuilder(label.Capacity);
                    var msgSB = new StringBuilder(label.Capacity);

                    labelData.Shadow = new HudAPIv2.SpaceMessage(shadowSB, textWorldPos, Vector3D.Up, Vector3D.Left, LABEL_TEXT_SCALE, Blend: LABEL_SHADOW_BLEND_TYPE);
                    labelData.Text = new HudAPIv2.SpaceMessage(msgSB, textWorldPos, Vector3D.Up, Vector3D.Left, LABEL_TEXT_SCALE, Blend: LABEL_BLEND_TYPE);

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

                shadow.Visible = true;
                shadow.WorldPosition = shadowPos;
                shadow.Left = cm.Left;
                shadow.Up = cm.Up;

                text.Visible = true;
                text.WorldPosition = textPos;
                text.Left = cm.Left;
                text.Up = cm.Up;

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

        private void DrawMountPoint(MyCubeBlockDefinition.MountPoint mountPoint, float cubeSize, ref Vector3I center, ref MatrixD mainMatrix, ref Color colorFace, double minSize)
        {
            var startLocal = mountPoint.Start - center;
            var endLocal = mountPoint.End - center;

            var bb = new BoundingBoxD(Vector3.Min(startLocal, endLocal) * cubeSize, Vector3.Max(startLocal, endLocal) * cubeSize);
            var obb = new MyOrientedBoundingBoxD(bb, mainMatrix);

            var normalAxis = Base6Directions.GetAxis(Base6Directions.GetDirection(ref mountPoint.Normal));

            var m = MatrixD.CreateFromQuaternion(obb.Orientation);
            m.Right *= Math.Max(obb.HalfExtent.X * 2, (normalAxis == Base6Directions.Axis.LeftRight ? MOUNTPOINT_THICKNESS : 0));
            m.Up *= Math.Max(obb.HalfExtent.Y * 2, (normalAxis == Base6Directions.Axis.UpDown ? MOUNTPOINT_THICKNESS : 0));
            m.Forward *= Math.Max(obb.HalfExtent.Z * 2, (normalAxis == Base6Directions.Axis.ForwardBackward ? MOUNTPOINT_THICKNESS : 0));
            m.Translation = obb.Center;

            MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref colorFace, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: OVERLAY_SQUARE_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);

            //var colorWire = colorFace * 4;
            //MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref colorWire, MySimpleObjectRasterizer.Wireframe, 1, lineWidth: 0.005f, lineMaterial: MATERIAL_SQUARE, onlyFrontFaces: true);
        }

        private void DrawMountPointAxixText(MyCubeBlockDefinition def, float gridSize, ref MatrixD drawMatrix)
        {
            var matrix = MatrixD.CreateScale(def.Size * gridSize);
            matrix.Translation = (def.Center - (def.Size * 0.5f));
            matrix = matrix * drawMatrix;

            DrawAxis(TextAPIMsgIds.AXIS_Z, ref Vector3.Forward, Color.Blue, ref drawMatrix, ref matrix);
            DrawAxis(TextAPIMsgIds.AXIS_X, ref Vector3.Right, Color.Red, ref drawMatrix, ref matrix);
            DrawAxis(TextAPIMsgIds.AXIS_Y, ref Vector3.Up, Color.Lime, ref drawMatrix, ref matrix);
        }

        private void DrawAxis(TextAPIMsgIds id, ref Vector3 direction, Color color, ref MatrixD drawMatrix, ref MatrixD matrix)
        {
            var dir = Vector3D.TransformNormal(direction * 0.5f, matrix);
            var text = AXIS_LABELS[(int)id];
            DrawLineLabel(id, drawMatrix.Translation, dir, color, message: text, lineHeight: 1.5f);
        }
        #endregion Draw helpers
    }
}
