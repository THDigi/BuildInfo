using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Input;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

using Draygo.API;

namespace Digi.BuildInfo
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class BuildInfo : MySessionComponentBase
    {
        public override void LoadData()
        {
            instance = this;
            Log.SetUp("Build Info", 514062285, "BuildInfo");
        }

        public static BuildInfo instance = null;
        public bool init = false;
        public bool isThisDS = false;
        public LeakInfo leakInfo = null; // leak info component
        private short tick = 0; // global incrementing gamelogic tick
        private bool showBuildInfo = true;
        private bool showMountPoints = false;
        private bool useTextAPI = true; // the user's preference for textAPI or notification; use TextAPIEnabled to determine if you need to use textAPI or not!
        private bool TextAPIEnabled { get { return (useTextAPI && textAPI != null && textAPI.Heartbeat); } }
        private MyDefinitionId lastDefId; // last selected definition ID, can be set to MENU_DEFID too!
        private IMyHudNotification buildInfoNotification = null;
        private IMyHudNotification mountPointsNotification = null;
        private IMyHudNotification transparencyNotification = null;
        private IMyHudNotification freezeGizmoNotification = null;
        private Cache cache = null; // currently selected cache to avoid another dictionary lookup in Draw()
        private bool textShown = false;

        // menu specific stuff
        private bool showMenu = false;
        private bool menuNeedsUpdate = true;
        private int menuSelectedItem = 0;
        private Vector3 previousMove = Vector3.Zero;

        // used by the textAPI view mode
        public HUDTextAPI textAPI = null;
        private bool rotationHints = true;
        private bool hudVisible = true;
        private double aspectRatio = 1;
        private int lastNewLineIndex = 0;
        private HUDTextAPI.HUDMessage textObject;
        private StringBuilder textAPIlines = new StringBuilder();
        private readonly Dictionary<MyDefinitionId, Cache> cachedInfoTextAPI = new Dictionary<MyDefinitionId, Cache>();

        // used by the HUD notification view mode
        private int atLine = SCROLL_FROM_LINE;
        private long lastScroll = 0;
        private List<HudLine> notificationLines = new List<HudLine>();
        private readonly Dictionary<MyDefinitionId, Cache> cachedInfoNotification = new Dictionary<MyDefinitionId, Cache>();

        // used in generating the block info text or menu for either view mode
        private int line = -1;
        private bool addLineCalled = false;
        private float largestLineWidth = 0;
        public float ignorePx = 0;

        // resource sink group cache
        public int resourceSinkGroups = 0;
        public int resourceSourceGroups = 0;
        public readonly Dictionary<MyStringHash, ResourceGroupData> resourceGroupPriority = new Dictionary<MyStringHash, ResourceGroupData>();

        // character size cache for notifications; textAPI has its own.
        private readonly Dictionary<char, int> charSize = new Dictionary<char, int>();

        // cached block data that is inaccessible via definitions (like thruster flames)
        public readonly Dictionary<MyDefinitionId, IBlockData> blockData = new Dictionary<MyDefinitionId, IBlockData>();

        // various temporary caches
        private readonly HashSet<Vector3I> cubes = new HashSet<Vector3I>();
        private readonly List<MyDefinitionId> removeCacheIds = new List<MyDefinitionId>();

        // constants
        public const int CACHE_EXPIRE_SECONDS = 60 * 5; // how long a cached string remains stored until it's purged
        private const int SPACE_SIZE = 8; // space character's width; used in HUD notification view mode.
        private const int MAX_LINES = 8; // max amount of HUD notification lines to print; used in HUD notification view mode.
        private const int SCROLL_FROM_LINE = 2; // ignore lines to this line when scrolling, to keep important stuff like mass in view at all times; used in HUD notification view mode.
        private const int TEXT_ID = 1; // textAPI ID
        private const double TEXT_SCALE = 0.8; // textAPI text scale
        private readonly Vector2D TEXT_HUDPOS = new Vector2D(-0.9825, 0.8); // textAPI default left side position
        private readonly Vector2D TEXT_HUDPOS_WIDE = new Vector2D(-0.9825 / 3f, 0.8); // textAPI default left side position when using a really wide resolution
        private readonly Vector2D MENU_HUDPOS = new Vector2D(-0.3, 0.4); // textAPI menu position
        private readonly Vector2D MENU_HUDPOS_WIDE = new Vector2D(-0.3 / 3f, 0.4); // textAPI menu position when using a really wide resolution
        private readonly HUDTextAPI.HUDMessage TEXTOBJ_EMPTY = new HUDTextAPI.HUDMessage(TEXT_ID, 0, Vector2D.Zero, string.Empty); // empty text object used to hide the textAPI one
        private readonly MyStringId MATERIAL_BACKGROUND = MyStringId.GetOrCompute("BuildInfo_TextBackground");
        private readonly MyDefinitionId DEFID_MENU = new MyDefinitionId(typeof(MyObjectBuilder_GuiScreen)); // just a random non-block type to use as the menu's ID
        public readonly HashSet<MyObjectBuilderType> DEFAULT_ALLOWED_TYPES = new HashSet<MyObjectBuilderType>() // used in inventory formatting if type argument is null
        {
            typeof(MyObjectBuilder_Ore),
            typeof(MyObjectBuilder_Ingot),
            typeof(MyObjectBuilder_Component)
        };

        public void Init()
        {
            Log.Init();
            init = true;
            isThisDS = (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated);

            if(isThisDS) // not doing anything DS side so get rid of this component entirely
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(DisposeComponent);
                return;
            }

            ComputeCharacterSizes();
            ComputeResourceGroups();
            UpdateConfigValues();

            leakInfo = new LeakInfo();

            textAPI = new HUDTextAPI((long)Log.workshopId);
            textObject.id = TEXT_ID;
            textObject.ttl = int.MaxValue;
            textObject.scale = TEXT_SCALE;
            textObject.options = HUDTextAPI.Options.HideHud;

            MyAPIGateway.Utilities.MessageEntered += MessageEntered;
            MyAPIGateway.Gui.GuiControlRemoved += GuiControlRemoved;
        }

        private void DisposeComponent()
        {
            Log.Close();
            instance = null;
            SetUpdateOrder(MyUpdateOrder.NoUpdate);
            MyAPIGateway.Session.UnregisterComponent(this);
        }

        protected override void UnloadData()
        {
            instance = null;

            try
            {
                if(init)
                {
                    init = false;

                    MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
                    MyAPIGateway.Gui.GuiControlRemoved -= GuiControlRemoved;

                    if(leakInfo != null)
                    {
                        leakInfo.Close();
                        leakInfo = null;
                    }

                    if(textAPI != null)
                    {
                        textAPI.Close();
                        textAPI = null;
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            Log.Close();
        }

        public void MessageEntered(string msg, ref bool send)
        {
            if(msg.StartsWith("/buildinfo", StringComparison.InvariantCultureIgnoreCase))
            {
                send = false;
                showMenu = true;
            }
        }

        private void GuiControlRemoved(object obj)
        {
            try
            {
                if(obj.ToString().EndsWith("ScreenOptionsSpace")) // closing options menu just assumes you changed something so it'll re-check config settings
                {
                    UpdateConfigValues();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void UpdateConfigValues()
        {
            var cfg = MyAPIGateway.Session.Config;
            hudVisible = !cfg.MinimalHud;
            aspectRatio = (double)cfg.ScreenWidth / (double)cfg.ScreenHeight;
            bool newValue = cfg.RotationHints;

            if(rotationHints != newValue)
            {
                rotationHints = newValue;
                HideText();

                foreach(CacheTextAPI c in cachedInfoTextAPI.Values)
                {
                    c.InvalidateScreenPosition();
                }
            }
        }

        public override void HandleInput()
        {
            try
            {
                if(!init || MyAPIGateway.Gui.IsCursorVisible || MyAPIGateway.Gui.ChatEntryVisible)
                    return;

                if(MyCubeBuilder.Static != null && MyCubeBuilder.Static.IsActivated)
                {
                    var input = MyAPIGateway.Input;

                    if(showMenu)
                    {
                        bool canUseTextAPI = (textAPI != null && textAPI.Heartbeat);
                        int usableMenuItems = (canUseTextAPI ? 6 : 5);
                        var move = Vector3.Round(input.GetPositionDelta(), 1);

                        if(previousMove.Z == 0)
                        {
                            if(move.Z > 0.2f)
                            {
                                menuNeedsUpdate = true;

                                if(++menuSelectedItem >= usableMenuItems)
                                    menuSelectedItem = 0;
                            }

                            if(move.Z < -0.2f)
                            {
                                menuNeedsUpdate = true;

                                if(--menuSelectedItem < 0)
                                    menuSelectedItem = (usableMenuItems - 1);
                            }
                        }

                        if(previousMove.X == 0 && Math.Abs(move.X) > 0.2f)
                        {
                            menuNeedsUpdate = true;

                            switch(menuSelectedItem)
                            {
                                case 0:
                                    showMenu = false;
                                    menuNeedsUpdate = true;
                                    break;
                                case 1:
                                    showBuildInfo = !showBuildInfo;
                                    if(buildInfoNotification == null)
                                        buildInfoNotification = MyAPIGateway.Utilities.CreateNotification("");
                                    buildInfoNotification.Text = (showBuildInfo ? "Build info ON" : "Build info OFF");
                                    buildInfoNotification.Show();
                                    break;
                                case 2:
                                    showMountPoints = !showMountPoints;
                                    break;
                                case 3:
                                    MyCubeBuilder.Static.UseTransparency = !MyCubeBuilder.Static.UseTransparency;
                                    break;
                                case 4:
                                    MyCubeBuilder.Static.FreezeGizmo = !MyCubeBuilder.Static.FreezeGizmo;
                                    break;
                                case 5:
                                    useTextAPI = !useTextAPI;
                                    HideText();
                                    break;
                            }
                        }

                        previousMove = move;
                    }

                    if(input.IsNewGameControlPressed(MyControlsSpace.VOXEL_HAND_SETTINGS))
                    {
                        if(input.IsAnyShiftKeyPressed())
                        {
                            MyCubeBuilder.Static.UseTransparency = !MyCubeBuilder.Static.UseTransparency;
                            menuNeedsUpdate = true;

                            if(transparencyNotification == null)
                                transparencyNotification = MyAPIGateway.Utilities.CreateNotification("");

                            transparencyNotification.Text = (MyCubeBuilder.Static.UseTransparency ? "Placement transparency ON" : "Placement transparency OFF");
                            transparencyNotification.Show();
                        }
                        else if(input.IsAnyCtrlKeyPressed())
                        {
                            showMountPoints = !showMountPoints;
                            menuNeedsUpdate = true;

                            if(mountPointsNotification == null)
                                mountPointsNotification = MyAPIGateway.Utilities.CreateNotification("");

                            mountPointsNotification.Text = (showMountPoints ? "Mount points view ON" : "Mount points view OFF");
                            mountPointsNotification.Show();
                        }
                        else if(input.IsAnyAltKeyPressed())
                        {
                            SetFreezeGizmo(!MyCubeBuilder.Static.FreezeGizmo);
                        }
                        else
                        {
                            showMenu = !showMenu;
                            menuNeedsUpdate = true;
                        }
                    }
                }
                else if(showMenu)
                {
                    showMenu = false;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void SetFreezeGizmo(bool freeze)
        {
            if(freezeGizmoNotification == null)
                freezeGizmoNotification = MyAPIGateway.Utilities.CreateNotification("");

            if(freeze && MyCubeBuilder.Static.DynamicMode)
            {
                freezeGizmoNotification.Text = "First aim at a grid!";
                freezeGizmoNotification.Font = MyFontEnum.Red;
            }
            else
            {
                MyCubeBuilder.Static.FreezeGizmo = freeze;

                freezeGizmoNotification.Text = (freeze ? "Freeze placement position ON" : "Freeze placement position OFF");
                freezeGizmoNotification.Font = MyFontEnum.White;
            }

            freezeGizmoNotification.Show();
        }

        public override void Draw()
        {
            try
            {
                if(!init || !hudVisible)
                    return;

                // background for the textAPI's text
                if(TextAPIEnabled && textShown)
                {
                    var cacheTextAPI = (CacheTextAPI)cache;
                    var camera = MyAPIGateway.Session.Camera;
                    var camMatrix = camera.WorldMatrix;
                    var fov = camera.FovWithZoom;

                    var localscale = 0.1735 * Math.Tan(fov / 2);
                    var localXmul = (aspectRatio * 9d / 16d);
                    var localYmul = (1 / aspectRatio) * localXmul;

                    var origin = cacheTextAPI.screenPos;
                    origin.X *= localscale * localXmul;
                    origin.Y *= localscale * localYmul;
                    var textpos = Vector3D.Transform(new Vector3D(origin.X, origin.Y, -0.1), camMatrix);

                    var widthStep = localscale * 9d * 0.001075;
                    var width = cacheTextAPI.largestLineWidth * widthStep;
                    var heightStep = localscale * 9d * 0.001;
                    var height = (cacheTextAPI.numLines + 2) * heightStep;
                    textpos += camMatrix.Right * (width - (widthStep * 2)) + camMatrix.Down * (height - (heightStep * 2));

                    // TODO use HUD background transparency once that is a thing
                    MyTransparentGeometry.AddBillboardOriented(MATERIAL_BACKGROUND, Color.White * 0.75f, textpos, camMatrix.Left, camMatrix.Up, (float)width, (float)height);
                }

                if(showMountPoints && MyCubeBuilder.Static.DynamicMode) // HACK only in dynamic mode because GetBuildBoundingBox() gives bad values when aiming at a grid
                {
                    var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;

                    if(def != null && MyCubeBuilder.Static.IsActivated)
                    {
                        var box = MyCubeBuilder.Static.GetBuildBoundingBox();
                        var m = MatrixD.CreateFromQuaternion(box.Orientation);
                        m.Translation = box.Center;

                        #region experiments
                        // experiments to find a way to get gizmo center when placed on grids... nothing useful so far
                        //
                        //var grid = MyCubeBuilder.Static.FindClosestGrid();
                        //
                        //if(grid != null)
                        //{
                        //    var gridPos = grid.WorldMatrix.Translation;
                        //    var localPos = (box.Center - gridPos) * grid.GridSize;
                        //    m.Translation = gridPos + localPos;
                        //
                        //    if(def.Size.Size > 1)
                        //    {
                        //        //var center = Vector3D.Transform(def.Center * grid.GridSize, m);
                        //        //MyAPIGateway.Utilities.ShowNotification("dot=" + Math.Round(Vector3D.Dot(m.Translation, m.Up), 3), 16);
                        //
                        //        var extraSize = (def.Size - Vector3I.One) * grid.GridSizeHalf;
                        //        m.Translation += m.Left * extraSize.X;
                        //        m.Translation += m.Up * extraSize.Y;
                        //        m.Translation += m.Backward * extraSize.Z;
                        //
                        //        //var extraSize = def.Size - Vector3I.One;
                        //        //var dotX = (Vector3D.Dot(m., m.Right) > 0 ? 1 : -1);
                        //        //var dotY = (Vector3D.Dot(m.Translation, m.Up) > 0 ? 1 : -1);
                        //        //var dotZ = (Vector3D.Dot(m.Translation, m.Backward) > 0 ? 1 : -1);
                        //        //m.Translation += m.Right * (grid.GridSizeHalf * extraSize.X * dotX);
                        //        //m.Translation += m.Up * (grid.GridSizeHalf * extraSize.Y * dotY);
                        //        //m.Translation += m.Backward * (grid.GridSizeHalf * extraSize.Z * dotZ);
                        //    }
                        //}
                        #endregion

                        MyCubeBuilder.DrawMountPoints(MyDefinitionManager.Static.GetCubeSize(def.CubeSize), def, ref m);
                    }
                }

                if(leakInfo != null)
                    leakInfo.Draw();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void GenerateMenuText()
        {
            ResetLines();

            int showMenuItems = 6;
            bool canUseTextAPI = (textAPI != null && textAPI.Heartbeat);
            var inputName = GetControlAssignedName(MyControlsSpace.VOXEL_HAND_SETTINGS);

            AddLine(MyFontEnum.Blue).Append("Build info settings:").EndLine();

            for(int i = 0; i < showMenuItems; i++)
            {
                AddLine(font: (menuSelectedItem == i ? MyFontEnum.Green : (i == 5 && !canUseTextAPI ? MyFontEnum.Red : MyFontEnum.White)));

                GetLine().Append("[ ");

                switch(i)
                {
                    case 0:
                        GetLine().Append("Close menu");
                        if(inputName != null)
                            GetLine().Append("   (" + inputName + ")");
                        break;
                    case 1:
                        GetLine().Append("Show build info: ").Append(showBuildInfo ? "ON" : "OFF");
                        break;
                    case 2:
                        GetLine().Append("Show mount points: ").Append(showMountPoints ? "ON" : "OFF");
                        if(inputName != null)
                            GetLine().Append("   (Ctrl+" + inputName + ")");
                        break;
                    case 3:
                        GetLine().Append("Transparent model: ").Append(MyCubeBuilder.Static.UseTransparency ? "ON" : "OFF");
                        if(inputName != null)
                            GetLine().Append("   (Shift+" + inputName + ")");
                        break;
                    case 4:
                        GetLine().Append("Freeze in position: ").Append(MyCubeBuilder.Static.FreezeGizmo ? "ON" : "OFF");
                        if(inputName != null)
                            GetLine().Append("   (Alt+" + inputName + ")");
                        break;
                    case 5:
                        GetLine().Append("Use TextAPI: ");
                        if(canUseTextAPI)
                            GetLine().Append(useTextAPI ? "ON" : "OFF");
                        else
                            GetLine().Append("OFF (Mod not detected)");
                        break;
                }

                GetLine().Append(" ]").EndLine();
            }

            AddLine(MyFontEnum.Blue).Append("Use movement controls to navigate and edit settings.").EndLine();

            if(inputName == null)
                AddLine(MyFontEnum.ErrorMessageBoxCaption).Append("The 'Open voxel hand settings' control is not assigned!").EndLine();

            EndAddedLines();
        }

        private void GenerateBlockText(MyCubeBlockDefinition def)
        {
            ResetLines();

            int airTightFaces = 0;
            int totalFaces = 0;
            var defTypeId = def.Id.TypeId;
            var isDoor = (defTypeId == typeof(MyObjectBuilder_Door)
                        || defTypeId == typeof(MyObjectBuilder_AirtightDoorGeneric)
                        || defTypeId == typeof(MyObjectBuilder_AirtightHangarDoor)
                        || defTypeId == typeof(MyObjectBuilder_AirtightSlideDoor)
                        || defTypeId == typeof(MyObjectBuilder_AdvancedDoor));
            var airTight = IsAirTight(def, ref airTightFaces, ref totalFaces);
            var deformable = (def.BlockTopology == MyBlockTopology.Cube);
            var assembleTime = (int)(def.MaxIntegrity / def.IntegrityPointsPerSec);
            var buildModels = (def.BuildProgressModels != null && def.BuildProgressModels.Length > 0);
            var weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
            var grindRatio = def.DisassembleRatio;

            if(def is MyDoorDefinition || def is MyAdvancedDoorDefinition) // HACK hardcoded; from MyDoor & MyAdvancedDoor's overridden DisassembleRatio
                grindRatio *= 3.3f;

            #region Block name line only for textAPI
            if(TextAPIEnabled)
            {
                AddLine(MyFontEnum.DarkBlue).Append(def.DisplayNameText);

                var stages = def.BlockStages;

                const string variantColor = "<color=0,200,0>";

                if(stages != null && stages.Length > 0)
                {
                    GetLine().Append("  ").AddIgnored(variantColor).Append("(Variant 1 of ").Append(stages.Length + 1).Append(")");
                }
                else
                {
                    stages = MyCubeBuilder.Static.ToolbarBlockDefinition.BlockStages;

                    if(stages != null && stages.Length > 0)
                    {
                        int num = 0;

                        for(int i = 0; i < stages.Length; ++i)
                        {
                            if(def.Id == stages[i])
                            {
                                num = i + 2; // +2 instead of +1 because the 1st block is not in the list, it's the list holder
                                break;
                            }
                        }

                        GetLine().Append("  ").AddIgnored(variantColor).Append("(Variant ").Append(num).Append(" of ").Append(stages.Length + 1).Append(")");
                    }
                }

                GetLine().EndLine();
            }
            #endregion

            #region Line 1
            AddLine().MassFormat(def.Mass).Separator().VectorFormat(def.Size).Separator().TimeFormat(assembleTime / weldMul).MultiplierFormat(weldMul);

            if(grindRatio > 1)
                GetLine().Separator().Append("Deconstruct speed: ").PercentFormat(1f / grindRatio);

            if(!buildModels)
                GetLine().Separator().Append("(No construction models)");

            GetLine().EndLine();
            #endregion

            #region Line 2
            AddLine(font: (deformable ? MyFontEnum.Blue : MyFontEnum.White)).Append("Integrity: ").AppendFormat("{0:#,###,###,###,###}", def.MaxIntegrity).Separator().Append("Deformable: ");
            if(deformable)
                GetLine().Append("Yes (").NumFormat(def.DeformationRatio, 3).Append(")");
            else
                GetLine().Append("No");
            GetLine().EndLine();
            #endregion

            #region Line 3
            AddLine(font: (airTight ? MyFontEnum.Green : (airTightFaces == 0 ? MyFontEnum.Red : MyFontEnum.Blue))).Append("Air-tight faces: ");

            if(airTight)
            {
                GetLine().Append("All");
            }
            else
            {
                if(airTightFaces == 0)
                    GetLine().Append("None");
                else
                    GetLine().Append(airTightFaces).Append(" of ").Append(totalFaces);
            }

            if(isDoor)
                GetLine().Append(" (front+back are toggled)");

            GetLine().EndLine();
            #endregion

            #region Optional - different item gain on grinding
            foreach(var comp in def.Components)
            {
                if(comp.DeconstructItem != comp.Definition)
                {
                    AddLine(MyFontEnum.ErrorMessageBoxCaption).Append("When grinding: ").Append(comp.Definition.DisplayNameText).Append(" turns into ").Append(comp.DeconstructItem.DisplayNameText).EndLine();
                }
            }
            #endregion

            // TODO when VoxelPlacementSettings and VoxelPlacementMode are whitelisted:
            //if(def.VoxelPlacement.HasValue)
            //{
            //    var vp = def.VoxelPlacement.Value;
            //    SetText(line++, "Voxel rules - Dynamic: " + vp.DynamicMode.PlacementMode + ", Static: " + vp.StaticMode.PlacementMode);
            //}

            #region Optional - creative-only stuff
            if(MyAPIGateway.Session.CreativeMode || MyAPIGateway.Session.EnableCopyPaste) // creative game mode OR spacemaster's creative tools are enabled
            {
                if(def.MirroringBlock != null)
                {
                    MyCubeBlockDefinition mirrorDef;

                    if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(new MyDefinitionId(def.Id.TypeId, def.MirroringBlock), out mirrorDef))
                        AddLine(MyFontEnum.Blue).Append("Mirrors with: ").Append(mirrorDef.DisplayNameText).EndLine();
                    else
                        AddLine(MyFontEnum.Red).Append("Mirrors with: ").Append(def.MirroringBlock).Append(" (Error: not found)").EndLine();
                }
            }
            #endregion

            #region Details on last lines
            if(defTypeId != typeof(MyObjectBuilder_CubeBlock)) // anything non-decorative
                GenerateAdvancedBlockText(def);

            if(!def.Context.IsBaseGame)
                AddLine(MyFontEnum.Blue).Append("Mod: ").ModFormat(def.Context).EndLine();

            EndAddedLines();
            #endregion
        }

        private void GenerateAdvancedBlockText(MyCubeBlockDefinition def)
        {
            // TODO convert these if conditions to 'as' checking when their interfaces are not internal anymore

            var defTypeId = def.Id.TypeId;

            if(defTypeId == typeof(MyObjectBuilder_TerminalBlock)) // control panel block
            {
                // HACK hardcoded
                AddLine(MyFontEnum.Green).Append("Power required*: No").EndLine();
                return;
            }

            if(defTypeId == typeof(MyObjectBuilder_Conveyor) || defTypeId == typeof(MyObjectBuilder_ConveyorConnector)) // conveyor hubs and tubes
            {
                // HACK hardcoded; from MyGridConveyorSystem
                float requiredPower = MyEnergyConstants.REQUIRED_INPUT_CONVEYOR_LINE;
                AddLine().Append("Power required*: ").PowerFormat(requiredPower).Separator().ResourcePriority("Conveyors", hardcoded: true).EndLine();
                return;
            }

            if(defTypeId == typeof(MyObjectBuilder_Drill))
            {
                // HACK hardcoded; from MyShipDrill
                float requiredPower = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_DRILL;
                AddLine().Append("Power required*: ").PowerFormat(requiredPower);

                // HACK hardcoded only for vanilla definitions unchanged
                if(def.Context.IsBaseGame)
                    GetLine().Separator().ResourcePriority("Defense", hardcoded: true);

                GetLine().EndLine();

                float volume;
                if(GetInventoryFromComponent(def, out volume))
                {
                    AddLine().Append("Inventory: ").InventoryFormat(volume, typeof(MyObjectBuilder_Ore)).EndLine();
                }
                else
                {
                    // HACK hardcoded; from MyShipDrill
                    var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                    volume = (float)(def.Size.X * def.Size.Y * def.Size.Z) * gridSize * gridSize * gridSize * 0.5f;
                    AddLine().Append("Inventory*: ").InventoryFormat(volume, typeof(MyObjectBuilder_Ore)).EndLine();
                }

                // HACK MyShipDrillDefinition is internal; also GetObjectBuilder() is useless
                //var defObj = (MyObjectBuilder_ShipDrillDefinition)def.GetObjectBuilder();
                //AddLine().Append("Harvest radius: ").DistanceFormat(defObj.SensorRadius).Separator().Append("Front offset: ").DistanceFormat(defObj.SensorOffset).EndLine();
                //AddLine().Append("Alternate (no ore) radius: ").DistanceFormat(defObj.CutOutRadius).Separator().Append("Front offset: ").DistanceFormat(defObj.CutOutOffset).EndLine();

                // HACK hardcoded only for vanilla definitions unchanged
                if(def.Context.IsBaseGame)
                {
                    if(def.Id.SubtypeName == "SmallBlockDrill")
                    {
                        const float sensorRadius = 1.3f;
                        const float sensorOffset = 0.8f;
                        const float cutOutRadius = 1.3f;
                        const float cutOutOffset = 0.6f;
                        AddLine().Append("Harvest radius*: ").DistanceFormat(sensorRadius).Separator().Append("Front offset*: ").DistanceFormat(sensorOffset).EndLine();
                        AddLine().Append("Alternate (no ore) radius*: ").DistanceFormat(cutOutRadius).Separator().Append("Front offset*: ").DistanceFormat(cutOutOffset).EndLine();
                    }
                    else if(def.Id.SubtypeName == "LargeBlockDrill")
                    {
                        const float sensorRadius = 1.9f;
                        const float sensorOffset = 2.8f;
                        const float cutOutRadius = 1.9f;
                        const float cutOutOffset = 2.8f;
                        AddLine().Append("Harvest radius*: ").DistanceFormat(sensorRadius).Separator().Append("Front offset*: ").DistanceFormat(sensorOffset).EndLine();
                        AddLine().Append("Alternate (no ore) radius*: ").DistanceFormat(cutOutRadius).Separator().Append("Front offset*: ").DistanceFormat(cutOutOffset).EndLine();
                    }
                }
                return;
            }

            if(defTypeId == typeof(MyObjectBuilder_ShipConnector))
            {
                // HACK hardcoded; from MyShipConnector
                float requiredPower = MyEnergyConstants.MAX_REQUIRED_POWER_CONNECTOR;
                if(def.CubeSize == MyCubeSize.Small)
                    requiredPower *= 0.01f;

                AddLine().Append("Power required*: ").PowerFormat(requiredPower).Separator().ResourcePriority("Conveyors", hardcoded: true).EndLine();

                float volume;
                if(GetInventoryFromComponent(def, out volume))
                {
                    AddLine().Append("Inventory: ").InventoryFormat(volume).EndLine();
                }
                else
                {
                    // HACK hardcoded; from MyShipConnector
                    var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                    volume = (def.Size * gridSize * 0.8f).Volume;
                    AddLine().Append("Inventory*: ").InventoryFormat(volume).EndLine();
                }
                return;
            }

            var shipWelder = def as MyShipWelderDefinition;
            var shipGrinder = def as MyShipGrinderDefinition;
            if(shipWelder != null || shipGrinder != null)
            {
                // HACK hardcoded; from MyShipToolBase
                float requiredPower = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_GRINDER;

                AddLine().Append("Power required*: ").PowerFormat(requiredPower).Separator().ResourcePriority("Defense", hardcoded: true).EndLine();

                float volume;
                if(GetInventoryFromComponent(def, out volume))
                {
                    AddLine().Append("Inventory: ").InventoryFormat(volume).EndLine();
                }
                else
                {
                    // HACK hardcoded; from MyShipToolBase
                    var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                    volume = (float)def.Size.X * gridSize * (float)def.Size.Y * gridSize * (float)def.Size.Z * gridSize * 0.5f;
                    AddLine().Append("Inventory*: ").InventoryFormat(volume).EndLine();
                }

                if(shipWelder != null)
                {
                    float weld = 2; // HACK hardcoded; from MyShipWelder
                    var mul = MyAPIGateway.Session.WelderSpeedMultiplier;
                    AddLine().Append("Weld speed*: ").PercentFormat(weld * mul).Append(" split accross targets").MultiplierFormat(mul).EndLine();
                }
                else
                {
                    float grind = 2; // HACK hardcoded; from MyShipGrinder
                    var mul = MyAPIGateway.Session.GrinderSpeedMultiplier;
                    AddLine().Append("Grind speed*: ").PercentFormat(grind * mul).Append(" split accross targets").MultiplierFormat(mul).EndLine();
                }
                return;
            }

            var piston = def as MyPistonBaseDefinition;
            var motor = def as MyMotorStatorDefinition;
            if(piston != null || motor != null)
            {
                if(piston != null)
                {
                    AddLine().Append("Power required: ").PowerFormat(piston.RequiredPowerInput).Separator().ResourcePriority(piston.ResourceSinkGroup).EndLine();
                    AddLine().Append("Extended length: ").DistanceFormat(piston.Maximum).Separator().Append("Max speed: ").DistanceFormat(piston.MaxVelocity).EndLine();
                }

                if(motor != null)
                {
                    AddLine().Append("Power required: ").PowerFormat(motor.RequiredPowerInput).Separator().ResourcePriority(motor.ResourceSinkGroup).EndLine();

                    if(!(def is MyMotorSuspensionDefinition))
                    {
                        AddLine().Append("Max force: ").ForceFormat(motor.MaxForceMagnitude).EndLine();

                        if(motor.RotorDisplacementMin < motor.RotorDisplacementMax)
                            AddLine().Append("Displacement: ").DistanceFormat(motor.RotorDisplacementMin).Append(" to ").DistanceFormat(motor.RotorDisplacementMax).EndLine();
                    }

                    var suspension = def as MyMotorSuspensionDefinition;
                    if(suspension != null)
                    {
                        AddLine().Append("Force: ").ForceFormat(suspension.PropulsionForce).Separator().Append("Steer speed: ").RotationSpeed(suspension.SteeringSpeed * 60).Separator().Append("Steer angle: ").AngleFormat(suspension.MaxSteer).EndLine();
                        AddLine().Append("Height: ").DistanceFormat(suspension.MinHeight).Append(" to ").DistanceFormat(suspension.MaxHeight).EndLine();
                    }
                }

                var topPart = (motor != null ? motor.TopPart : piston.TopPart);
                var group = MyDefinitionManager.Static.TryGetDefinitionGroup(topPart);

                if(group == null)
                    return;

                var partDef = (def.CubeSize == MyCubeSize.Large ? group.Large : group.Small);
                var airTightFaces = 0;
                var totalFaces = 0;
                var airTight = IsAirTight(partDef, ref airTightFaces, ref totalFaces);
                var deformable = def.BlockTopology == MyBlockTopology.Cube;
                var buildModels = def.BuildProgressModels != null && def.BuildProgressModels.Length > 0;
                var weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
                var weldTime = ((def.MaxIntegrity / def.IntegrityPointsPerSec) / weldMul);
                var grindRatio = def.DisassembleRatio;

                AddLine(MyFontEnum.Blue).Append("Part: ").Append(partDef.DisplayNameText).EndLine();

                string padding = (TextAPIEnabled ? "        - " : "       - ");

                AddLine().Append(padding).MassFormat(partDef.Mass).Separator().VectorFormat(partDef.Size).Separator().TimeFormat(weldTime).MultiplierFormat(weldMul);

                if(grindRatio > 1)
                    GetLine().Separator().Append("Deconstruct speed: ").PercentFormat(1f / grindRatio);

                if(!buildModels)
                    GetLine().Append(" (No construction models)");

                GetLine().EndLine();

                AddLine().Append(padding).Append("Integrity: ").AppendFormat("{0:#,###,###,###,###}", partDef.MaxIntegrity);

                if(deformable)
                    GetLine().Separator().Append("Deformable (").NumFormat(partDef.DeformationRatio, 3).Append(")");

                GetLine().Separator().Append("Air-tight faces: ").Append(airTight ? "All" : (airTightFaces == 0 ? "None" : airTightFaces + " of " + totalFaces));
                GetLine().EndLine();
                return;
            }

            var shipController = def as MyShipControllerDefinition;
            if(shipController != null)
            {
                var rc = def as MyRemoteControlDefinition;
                if(rc != null)
                {
                    AddLine().Append("Power required: ").PowerFormat(rc.RequiredPowerInput).Separator().ResourcePriority(rc.ResourceSinkGroup).EndLine();
                }

                // HACK MyCryoChamberDefinition is internal
                if(defTypeId == typeof(MyObjectBuilder_CryoChamber))
                {
                    // UNDONE objectbuilder is not actually getting the values from the definition, so it's useless.
                    //var defObj = (MyObjectBuilder_CryoChamberDefinition)def.GetObjectBuilder();
                    //AddLine().Append("Power required: ").PowerFormat(defObj.IdlePowerConsumption).Separator().ResourcePriority(defObj.ResourceSinkGroup).EndLine();

                    // hardcoded only for the vanilla definition if it is not overwritten by a mod
                    if(def.Context.IsBaseGame && def.Id.SubtypeName == "LargeBlockCryoChamber")
                    {
                        const float idlePowerConsumption = 0.00003f;
                        AddLine().Append("Power required*: ").PowerFormat(idlePowerConsumption).Separator().ResourcePriority("Utility", hardcoded: true).EndLine();
                    }
                }

                AddLine((shipController.EnableShipControl ? MyFontEnum.Green : MyFontEnum.Red)).Append("Ship controls: ").Append(shipController.EnableShipControl ? "Yes" : "No").EndLine();
                AddLine((shipController.EnableFirstPerson ? MyFontEnum.Green : MyFontEnum.Red)).Append("First person view: ").Append(shipController.EnableFirstPerson ? "Yes" : "No").EndLine();
                AddLine((shipController.EnableBuilderCockpit ? MyFontEnum.Green : MyFontEnum.Red)).Append("Can build: ").Append(shipController.EnableBuilderCockpit ? "Yes" : "No").EndLine();

                var cockpit = def as MyCockpitDefinition;
                if(cockpit != null)
                {
                    float volume;

                    if(GetInventoryFromComponent(def, out volume))
                    {
                        AddLine().Append("Inventory: ").InventoryFormat(volume).EndLine();
                    }
                    else
                    {
                        volume = Vector3.One.Volume; // HACK hardcoded; from MyCockpit
                        AddLine().Append("Inventory*: ").InventoryFormat(volume).EndLine();
                    }

                    AddLine((cockpit.IsPressurized ? MyFontEnum.Green : MyFontEnum.Red)).Append("Pressurized: ");

                    if(cockpit.IsPressurized)
                        GetLine().Append("Yes, Oxygen capacity: ").NumFormat(cockpit.OxygenCapacity, 3).Append(" oxy");
                    else
                        GetLine().Append("No");

                    GetLine().EndLine();

                    if(cockpit.HUD != null)
                    {
                        MyDefinitionBase defBase;
                        if(MyDefinitionManager.Static.TryGetDefinition(new MyDefinitionId(typeof(MyObjectBuilder_HudDefinition), cockpit.HUD), out defBase))
                        {
                            // HACK MyHudDefinition is not whitelisted; also GetObjectBuilder() is useless because it doesn't get filled in
                            //var hudDefObj = (MyObjectBuilder_HudDefinition)defBase.GetObjectBuilder();
                            AddLine(MyFontEnum.Green).Append("Custom HUD: ").Append(cockpit.HUD).Separator().Append("Added by: ").ModFormat(defBase.Context).EndLine();
                        }
                        else
                        {
                            AddLine(MyFontEnum.Red).Append("Custom HUD: ").Append(cockpit.HUD).Append("  (Error: not found)").EndLine();
                        }
                    }
                }

                return;
            }

            var thrust = def as MyThrustDefinition;
            if(thrust != null)
            {
                if(!thrust.FuelConverter.FuelId.IsNull())
                {
                    AddLine().Append("Requires power to be controlled").Separator().ResourcePriority(thrust.ResourceSinkGroup).EndLine();
                    AddLine().Append("Requires fuel: ").Append(thrust.FuelConverter.FuelId.SubtypeId).Separator().Append("Efficiency: ").NumFormat(thrust.FuelConverter.Efficiency * 100, 2).Append("%").EndLine();
                }
                else
                {
                    AddLine().Append("Power: ").PowerFormat(thrust.MaxPowerConsumption).Separator().Append("Idle: ").PowerFormat(thrust.MinPowerConsumption).Separator().ResourcePriority(thrust.ResourceSinkGroup).EndLine();
                }

                AddLine().Append("Force: ").ForceFormat(thrust.ForceMagnitude).Separator().Append("Dampener factor: ").NumFormat(thrust.SlowdownFactor, 3).EndLine();

                if(thrust.EffectivenessAtMinInfluence < 1.0f || thrust.EffectivenessAtMaxInfluence < 1.0f)
                {
                    //if(thrust.NeedsAtmosphereForInfluence) // seems to be a pointless var
                    //{

                    AddLine(thrust.EffectivenessAtMaxInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White).PercentFormat(thrust.EffectivenessAtMaxInfluence).Append(" max thrust ");
                    if(thrust.MaxPlanetaryInfluence < 1f)
                        GetLine().Append("in ").PercentFormat(thrust.MaxPlanetaryInfluence).Append(" atmosphere");
                    else
                        GetLine().Append("in atmosphere");
                    GetLine().EndLine();

                    AddLine(thrust.EffectivenessAtMinInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White).PercentFormat(thrust.EffectivenessAtMinInfluence).Append(" max thrust ");

                    if(thrust.MinPlanetaryInfluence > 0f)
                        GetLine().Append("below ").PercentFormat(thrust.MinPlanetaryInfluence).Append(" atmosphere");
                    else
                        GetLine().Append("in space");
                    GetLine().EndLine();

                    //}
                    //else
                    //{
                    //    SetText(line++, PercentFormat(thrust.EffectivenessAtMaxInfluence) + " max thrust " + (thrust.MaxPlanetaryInfluence < 1f ? "in " + PercentFormat(thrust.MaxPlanetaryInfluence) + " planet influence" : "on planets"), thrust.EffectivenessAtMaxInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White);
                    //    SetText(line++, PercentFormat(thrust.EffectivenessAtMinInfluence) + " max thrust " + (thrust.MinPlanetaryInfluence > 0f ? "below " + PercentFormat(thrust.MinPlanetaryInfluence) + " planet influence" : "in space"), thrust.EffectivenessAtMinInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White);
                    //}
                }
                else
                {
                    AddLine(MyFontEnum.Green).Append("No thrust limits in space or planets").EndLine();
                }

                if(thrust.ConsumptionFactorPerG > 0)
                    AddLine(MyFontEnum.Red).Append("Extra consumption: +").PercentFormat(thrust.ConsumptionFactorPerG).Append(" per natural g acceleration").EndLine();

                var data = (BlockDataThrust)blockData.GetValueOrDefault(def.Id, null);

                if(data == null)
                {
                    var fakeBlock = SpawnFakeBlock(def) as MyThrust;

                    if(fakeBlock == null)
                    {
                        var error = "Couldn't get block data from fake entity!";
                        Log.Error(error, error);
                    }
                    else
                    {
                        data = new BlockDataThrust(fakeBlock);
                    }
                }

                if(data == null)
                {
                    var error = "Couldn't get block data for: " + def.Id;
                    Log.Error(error, error);
                }
                else
                {
                    var flameDistance = data.distance * Math.Max(1, thrust.SlowdownFactor); // if dampeners are stronger than normal thrust then the flame will be longer... not sure if this scaling is correct though

                    // HACK hardcoded; from MyThrust.DamageGrid() and MyThrust.ThrustDamage()
                    var damage = thrust.FlameDamage * data.flames;
                    var flameShipDamage = damage * 30f;
                    var flameDamage = damage * 10f * data.radius;

                    AddLine();

                    if(data.flames > 1)
                        GetLine().Append("Flames: ").Append(data.flames).Separator().Append("Max distance: ");
                    else
                        GetLine().Append("Flame max distance: ");

                    GetLine().DistanceFormat(flameDistance).Separator().Append("Max damage: ").NumFormat(flameShipDamage, 3).Append(" to ships").Separator().NumFormat(flameDamage, 3).Append(" to the rest").EndLine();
                }

                return;
            }

            var lg = def as MyLandingGearDefinition;
            if(lg != null)
            {
                // HACK hardcoded
                AddLine(MyFontEnum.Green).Append("Power required*: No").EndLine();
                return;
            }

            var light = def as MyLightingBlockDefinition;
            if(light != null)
            {
                var radius = light.LightRadius;
                var spotlight = def as MyReflectorBlockDefinition;
                if(spotlight != null)
                    radius = light.LightReflectorRadius;

                AddLine().Append("Power required: ").PowerFormat(light.RequiredPowerInput).Separator().ResourcePriority(light.ResourceSinkGroup).EndLine();
                AddLine().Append("Radius: ").DistanceFormat(radius.Min).Append(" to ").DistanceFormat(radius.Max).Separator().Append("Default: ").DistanceFormat(radius.Default).EndLine();
                AddLine().Append("Intensity: ").NumFormat(light.LightIntensity.Min, 3).Append(" to ").NumFormat(light.LightIntensity.Max, 3).Separator().Append("Default: ").NumFormat(light.LightIntensity.Default, 3).EndLine();
                AddLine().Append("Falloff: ").NumFormat(light.LightFalloff.Min, 3).Append(" to ").NumFormat(light.LightFalloff.Max, 3).Separator().Append("Default: ").NumFormat(light.LightFalloff.Default, 3).EndLine();

                if(spotlight == null)
                    AddLine(MyFontEnum.Blue).Append("Physical collisions: ").Append(light.HasPhysics ? "On" : "Off").EndLine();

                return;
            }

            var oreDetector = def as MyOreDetectorDefinition;
            if(oreDetector != null)
            {
                var requiredPowerInput = 0.002f; // HACK hardcoded; from MyOreDetector
                AddLine().Append("Power required*: ").PowerFormat(requiredPowerInput).Separator().ResourcePriority(oreDetector.ResourceSinkGroup).EndLine();
                AddLine().Append("Max range: ").DistanceFormat(oreDetector.MaximumRange).EndLine();
                return;
            }

            var gyro = def as MyGyroDefinition;
            if(gyro != null)
            {
                AddLine().Append("Power required: ").PowerFormat(gyro.RequiredPowerInput).Separator().ResourcePriority(gyro.ResourceSinkGroup).EndLine();
                AddLine().Append("Force: ").ForceFormat(gyro.ForceMagnitude).EndLine();
                return;
            }

            var projector = def as MyProjectorDefinition;
            if(projector != null)
            {
                AddLine().Append("Power required: ").PowerFormat(projector.RequiredPowerInput).Separator().ResourcePriority(projector.ResourceSinkGroup).EndLine();
                return;
            }

            var door = def as MyDoorDefinition;
            if(door != null)
            {
                float requiredPowerInput = MyEnergyConstants.MAX_REQUIRED_POWER_DOOR; // HACK hardcoded; from MyDoor
                float moveTime = (1f / ((MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS / 1000f) * door.OpeningSpeed)) / MyEngineConstants.UPDATE_STEPS_PER_SECOND; // computed after MyDoor.UpdateCurrentOpening()

                AddLine().Append("Power required*: ").PowerFormat(requiredPowerInput).Separator().ResourcePriority(door.ResourceSinkGroup).EndLine();
                AddLine().Append("Move time: ").TimeFormat(moveTime).Separator().Append("Distance: ").DistanceFormat(door.MaxOpen).EndLine();
                return;
            }

            var airTightDoor = def as MyAirtightDoorGenericDefinition; // does not extend MyDoorDefinition
            if(airTightDoor != null)
            {
                float moveTime = (1f / ((MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS / 1000f) * airTightDoor.OpeningSpeed)) / MyEngineConstants.UPDATE_STEPS_PER_SECOND; // computed after MyDoor.UpdateCurrentOpening()

                AddLine().Append("Power: ").PowerFormat(airTightDoor.PowerConsumptionMoving).Separator().Append("Idle: ").PowerFormat(airTightDoor.PowerConsumptionIdle).Separator().ResourcePriority(airTightDoor.ResourceSinkGroup).EndLine();
                AddLine().Append("Move time: ").TimeFormat(moveTime).EndLine();
                return;
            }

            var advDoor = def as MyAdvancedDoorDefinition; // does not extend MyDoorDefinition
            if(advDoor != null)
            {
                AddLine().Append("Power: ").PowerFormat(advDoor.PowerConsumptionMoving).Separator().Append("Idle: ").PowerFormat(advDoor.PowerConsumptionIdle).Separator().ResourcePriority(advDoor.ResourceSinkGroup).EndLine();

                float openTime = 0;
                float closeTime = 0;

                foreach(var seq in advDoor.OpeningSequence)
                {
                    var moveTime = (seq.MaxOpen / seq.Speed);

                    openTime = Math.Max(openTime, seq.OpenDelay + moveTime);
                    closeTime = Math.Max(closeTime, seq.CloseDelay + moveTime);
                }

                AddLine().Append("Move time - Opening: ").TimeFormat(openTime).Separator().Append("Closing: ").TimeFormat(closeTime).EndLine();
                return;
            }

            var production = def as MyProductionBlockDefinition;
            if(production != null)
            {
                AddLine().Append("Power: ").PowerFormat(production.OperationalPowerConsumption).Separator().Append("Idle: ").PowerFormat(production.StandbyPowerConsumption).Separator().ResourcePriority(production.ResourceSinkGroup).EndLine();

                var assembler = def as MyAssemblerDefinition;
                if(assembler != null)
                {
                    var mulSpeed = MyAPIGateway.Session.AssemblerSpeedMultiplier;
                    var mulEff = MyAPIGateway.Session.AssemblerEfficiencyMultiplier;

                    AddLine().Append("Assembly speed: ").PercentFormat(assembler.AssemblySpeed * mulSpeed).MultiplierFormat(mulSpeed).Separator().Append("Efficiency: ").PercentFormat(mulEff).MultiplierFormat(mulEff).EndLine();
                }

                var refinery = def as MyRefineryDefinition;
                if(refinery != null)
                {
                    var mul = MyAPIGateway.Session.RefinerySpeedMultiplier;

                    AddLine().Append("Refine speed: ").PercentFormat(refinery.RefineSpeed * mul).MultiplierFormat(mul).Separator().Append("Efficiency: ").PercentFormat(refinery.MaterialEfficiency).EndLine();
                }

                var gasTank = def as MyGasTankDefinition;
                if(gasTank != null)
                {
                    AddLine().Append("Stores: ").Append(gasTank.StoredGasId.SubtypeName).Separator().Append("Capacity: ").NumFormat(gasTank.Capacity, 3).EndLine();
                }

                var oxygenGenerator = def as MyOxygenGeneratorDefinition;
                if(oxygenGenerator != null)
                {
                    AddLine().Append("Ice consumption: ").MassFormat(oxygenGenerator.IceConsumptionPerSecond).Append(" per second").EndLine();

                    if(oxygenGenerator.ProducedGases.Count > 0)
                    {
                        AddLine().Append("Produces: ");

                        foreach(var gas in oxygenGenerator.ProducedGases)
                        {
                            GetLine().Append(gas.Id.SubtypeName).Append(" (ratio: ").NumFormat(gas.IceToGasRatio, 2).Append("), ");
                        }

                        GetLine().Length -= 2;
                        GetLine().EndLine();
                    }
                    else
                    {
                        AddLine(MyFontEnum.Red).Append("Produces: <N/A>").EndLine();
                    }
                }

                var volume = (production.InventoryMaxVolume > 0 ? production.InventoryMaxVolume : production.InventorySize.Volume);

                if(refinery != null || assembler != null)
                {
                    AddLine().Append("In+out inventories: ").InventoryFormat(volume * 2, production.InputInventoryConstraint, production.OutputInventoryConstraint).EndLine();
                }
                else
                {
                    AddLine().Append("Inventory: ").InventoryFormat(volume, production.InputInventoryConstraint).EndLine();
                }

                if(production.BlueprintClasses != null)
                {
                    if(production.BlueprintClasses.Count == 0)
                    {
                        AddLine(MyFontEnum.Red).Append("Has no blueprint classes.").EndLine();
                    }
                    else
                    {
                        AddLine();

                        if(def is MyRefineryDefinition)
                            GetLine().Append("Refines: ");
                        else if(def is MyGasTankDefinition)
                            GetLine().Append("Refills: ");
                        else if(def is MyAssemblerDefinition)
                            GetLine().Append("Builds: ");
                        else
                            GetLine().Append("Blueprints: ");

                        foreach(var bp in production.BlueprintClasses)
                        {
                            GetLine().Append(bp.DisplayNameText).Append(", ");
                        }

                        GetLine().Length -= 2;
                        GetLine().EndLine();
                    }
                }

                return;
            }

            var upgradeModule = def as MyUpgradeModuleDefinition;
            if(upgradeModule != null)
            {
                if(upgradeModule.Upgrades == null || upgradeModule.Upgrades.Length == 0)
                {
                    AddLine(MyFontEnum.Red).Append("Upgrade: N/A").EndLine();
                }
                else
                {
                    foreach(var upgrade in upgradeModule.Upgrades)
                    {
                        AddLine().Append("Upgrade: ").Append(upgrade.UpgradeType).Append(" ");

                        switch(upgrade.ModifierType)
                        {
                            case MyUpgradeModifierType.Additive: GetLine().Append("+").Append(upgrade.Modifier).Append(" added"); break;
                            case MyUpgradeModifierType.Multiplicative: GetLine().Append("multiplied by ").Append(upgrade.Modifier); break;
                            default: GetLine().Append(upgrade.Modifier).Append(" (").Append(upgrade.ModifierType).Append(")"); break;
                        }

                        GetLine().Append(" per slot").EndLine();
                    }
                }
                return;
            }

            var powerProducer = def as MyPowerProducerDefinition;
            if(powerProducer != null)
            {
                AddLine().Append("Power output: ").PowerFormat(powerProducer.MaxPowerOutput).Separator().ResourcePriority(powerProducer.ResourceSourceGroup).EndLine();

                var reactor = def as MyReactorDefinition;
                if(reactor != null)
                {
                    if(reactor.FuelDefinition != null)
                        AddLine().Append("Requires fuel: ").IdTypeSubtypeFormat(reactor.FuelId).EndLine();

                    var volume = (reactor.InventoryMaxVolume > 0 ? reactor.InventoryMaxVolume : reactor.InventorySize.Volume);
                    var invLimit = reactor.InventoryConstraint;

                    if(invLimit != null)
                    {
                        AddLine().Append("Inventory: ").InventoryFormat(volume, reactor.InventoryConstraint).EndLine();
                        AddLine().Append("Inventory items ").Append(invLimit.IsWhitelist ? "allowed" : "NOT allowed").Append(":").EndLine();

                        foreach(var id in invLimit.ConstrainedIds)
                        {
                            AddLine().Append("       - ").IdTypeSubtypeFormat(id).EndLine();
                        }

                        foreach(var type in invLimit.ConstrainedTypes)
                        {
                            AddLine().Append("       - All of type: ").IdTypeFormat(type).EndLine();
                        }
                    }
                    else
                    {
                        AddLine().Append("Inventory: ").InventoryFormat(volume).EndLine();
                    }
                }

                var battery = def as MyBatteryBlockDefinition;
                if(battery != null)
                {
                    AddLine(battery.AdaptibleInput ? MyFontEnum.White : MyFontEnum.Red).Append("Power input: ").PowerFormat(battery.RequiredPowerInput).Append(battery.AdaptibleInput ? " (adaptable)" : " (minimum required)").Separator().ResourcePriority(battery.ResourceSinkGroup).EndLine();
                    AddLine().Append("Power capacity: ").PowerStorageFormat(battery.MaxStoredPower).Separator().Append("Pre-charged: ").PowerStorageFormat(battery.MaxStoredPower * battery.InitialStoredPowerRatio).Append(" (").NumFormat(battery.InitialStoredPowerRatio * 100, 2).Append("%)").EndLine();
                    return;
                }

                var solarPanel = def as MySolarPanelDefinition;
                if(solarPanel != null)
                {
                    AddLine(solarPanel.IsTwoSided ? MyFontEnum.White : MyFontEnum.Red).Append(solarPanel.IsTwoSided ? "Two-sided" : "One-sided").EndLine();
                }
                return;
            }

            var oxygenFarm = def as MyOxygenFarmDefinition;
            if(oxygenFarm != null)
            {
                AddLine().Append("Power: ").PowerFormat(oxygenFarm.OperationalPowerConsumption).Separator().ResourcePriority(oxygenFarm.ResourceSinkGroup).EndLine();
                AddLine().Append("Produces: ").NumFormat(oxygenFarm.MaxGasOutput, 3).Append(" ").Append(oxygenFarm.ProducedGas.SubtypeName).Append(" per second").Separator().ResourcePriority(oxygenFarm.ResourceSourceGroup).EndLine();
                AddLine(oxygenFarm.IsTwoSided ? MyFontEnum.White : MyFontEnum.Red).Append(oxygenFarm.IsTwoSided ? "Two-sided" : "One-sided").EndLine();
                return;
            }

            var vent = def as MyAirVentDefinition;
            if(vent != null)
            {
                AddLine().Append("Idle: ").PowerFormat(vent.StandbyPowerConsumption).Separator().Append("Operational: ").PowerFormat(vent.OperationalPowerConsumption).Separator().ResourcePriority(vent.ResourceSinkGroup).EndLine();
                AddLine().Append("Output - Rate: ").NumFormat(vent.VentilationCapacityPerSecond, 3).Append(" oxy/s").Separator().ResourcePriority(vent.ResourceSourceGroup).EndLine();
                return;
            }

            var medicalRoom = def as MyMedicalRoomDefinition;
            if(medicalRoom != null)
            {
                // HACK hardcoded; from MyMedicalRoom
                var requiredPowerInput = MyEnergyConstants.MAX_REQUIRED_POWER_MEDICAL_ROOM;

                AddLine().Append("Power*: ").PowerFormat(requiredPowerInput).Separator().ResourcePriority(medicalRoom.ResourceSinkGroup).EndLine();

                AddLine(medicalRoom.ForceSuitChangeOnRespawn ? MyFontEnum.Blue : (!medicalRoom.RespawnAllowed ? MyFontEnum.Red : MyFontEnum.White)).Append("Respawn: ").BoolFormat(medicalRoom.RespawnAllowed).Separator();
                if(medicalRoom.RespawnAllowed && medicalRoom.ForceSuitChangeOnRespawn)
                {
                    GetLine().Append("Forced suit: ");

                    if(string.IsNullOrEmpty(medicalRoom.RespawnSuitName))
                    {
                        GetLine().Append("(Error: empty)");
                    }
                    else
                    {
                        MyCharacterDefinition charDef;
                        if(MyDefinitionManager.Static.Characters.TryGetValue(medicalRoom.RespawnSuitName, out charDef))
                            GetLine().Append(charDef.Name);
                        else
                            GetLine().Append(medicalRoom.RespawnSuitName).Append(" (Error: not found)");
                    }
                }
                else
                    GetLine().Append("Forced suit: No");
                GetLine().EndLine();

                AddLine(medicalRoom.HealingAllowed ? MyFontEnum.White : MyFontEnum.Red).Append("Healing: ").BoolFormat(medicalRoom.HealingAllowed).EndLine();

                AddLine(medicalRoom.RefuelAllowed ? MyFontEnum.White : MyFontEnum.Red).Append("Recharge: ").BoolFormat(medicalRoom.RefuelAllowed).EndLine();

                AddLine().Append("Suit change: ").BoolFormat(medicalRoom.SuitChangeAllowed).EndLine();

                if(medicalRoom.CustomWardrobesEnabled && medicalRoom.CustomWardrobeNames != null && medicalRoom.CustomWardrobeNames.Count > 0)
                {
                    AddLine(MyFontEnum.Blue).Append("Usable suits:");

                    foreach(var charName in medicalRoom.CustomWardrobeNames)
                    {
                        MyCharacterDefinition charDef;
                        if(!MyDefinitionManager.Static.Characters.TryGetValue(charName, out charDef))
                            AddLine(MyFontEnum.Red).Append("    ").Append(charName).Append(" (not found in definitions)").EndLine();
                        else
                            AddLine().Append("    ").Append(charDef.DisplayNameText).EndLine();
                    }
                }
                else
                    AddLine().Append("Usable suits: (all)").EndLine();
            }

            var radioAntenna = def as MyRadioAntennaDefinition;
            if(radioAntenna != null)
            {
                // HACK hardcoded; from MyRadioAntenna
                float maxRadius = (def.CubeSize == MyCubeSize.Large ? 50000f : 5000f);
                float requiredPowerInput = (maxRadius / 500f) * 0.002f;

                AddLine().Append("Max required power*: ").PowerFormat(requiredPowerInput).Separator().ResourcePriority(radioAntenna.ResourceSinkGroup).EndLine();
                AddLine().Append("Max radius*: ").DistanceFormat(maxRadius).EndLine();
                return;
            }

            var laserAntenna = def as MyLaserAntennaDefinition;
            if(laserAntenna != null)
            {
                AddLine().Append("Power: ").PowerFormat(laserAntenna.PowerInputLasing).Separator().Append("Turning: ").PowerFormat(laserAntenna.PowerInputTurning).Separator().Append("Idle: ").PowerFormat(laserAntenna.PowerInputIdle).Separator().ResourcePriority(laserAntenna.ResourceSinkGroup).EndLine();
                AddLine(laserAntenna.RequireLineOfSight ? MyFontEnum.White : MyFontEnum.Green).Append("Range: ").DistanceFormat(laserAntenna.MaxRange).Separator().Append("Line-of-sight: ").Append(laserAntenna.RequireLineOfSight ? "Required" : "Not required").EndLine();
                AddLine().Append("Rotation Pitch: ").AngleFormatDeg(laserAntenna.MinElevationDegrees).Append(" to ").AngleFormatDeg(laserAntenna.MaxElevationDegrees).Separator().Append("Yaw: ").AngleFormatDeg(laserAntenna.MinAzimuthDegrees).Append(" to ").AngleFormatDeg(laserAntenna.MaxAzimuthDegrees).EndLine();
                AddLine().Append("Rotation Speed: ").RotationSpeed(laserAntenna.RotationRate * 60).EndLine();
                return;
            }

            var beacon = def as MyBeaconDefinition;
            if(beacon != null)
            {
                // HACK hardcoded; from MyBeacon
                float maxRadius = (def.CubeSize == MyCubeSize.Large ? 50000f : 5000f);
                float requiredPowerInput = (maxRadius / 100000f) * 0.02f;

                AddLine().Append("Max required power*: ").PowerFormat(requiredPowerInput).Separator().ResourcePriority(beacon.ResourceSinkGroup).EndLine();
                AddLine().Append("Max radius*: ").DistanceFormat(maxRadius).EndLine();
                return;
            }

            var timer = def as MyTimerBlockDefinition;
            if(timer != null)
            {
                // HACK hardcoded; from MyTimerBlock
                float requiredPowerInput = 1E-07f;

                AddLine().Append("Power required*: ").PowerFormat(requiredPowerInput).Separator().ResourcePriority(timer.ResourceSinkGroup).EndLine();
                return;
            }

            var pb = def as MyProgrammableBlockDefinition;
            if(pb != null)
            {
                // HACK hardcoded; from MyProgrammableBlock
                float requiredPowerInput = 0.0005f;

                AddLine().Append("Power required*: ").PowerFormat(requiredPowerInput).Separator().ResourcePriority(pb.ResourceSinkGroup).EndLine();
                return;
            }

            var sound = def as MySoundBlockDefinition;
            if(sound != null)
            {
                // HACK hardcoded; from MySoundBlock
                float requiredPowerInput = 0.0002f;

                AddLine().Append("Power required*: ").PowerFormat(requiredPowerInput).Separator().ResourcePriority(sound.ResourceSinkGroup).EndLine();
                return;
            }

            var sensor = def as MySensorBlockDefinition;
            if(sensor != null)
            {
                Vector3 minField = Vector3.One;
                Vector3 maxField = new Vector3(sensor.MaxRange * 2);

                // HACK hardcoded; from MySensorBlock - sensor.RequiredPowerInput exists but is always reporting 0 and it seems ignored in the source code (see: Sandbox.Game.Entities.Blocks.MySensorBlock.CalculateRequiredPowerInput())
                float requiredPower = 0.0003f * (float)Math.Pow((maxField - minField).Volume, 1f / 3f);

                AddLine().Append("Max required power*: ").PowerFormat(requiredPower).Separator().ResourcePriority(sensor.ResourceSinkGroup).EndLine();
                AddLine().Append("Max area: ").VectorFormat(maxField).EndLine();
                return;
            }

            var artificialMass = def as MyVirtualMassDefinition;
            if(artificialMass != null)
            {
                AddLine().Append("Power required: ").PowerFormat(artificialMass.RequiredPowerInput).Separator().ResourcePriority(artificialMass.ResourceSinkGroup).EndLine();
                AddLine().Append("Artificial mass: ").MassFormat(artificialMass.VirtualMass).EndLine();
                return;
            }

            var spaceBall = def as MySpaceBallDefinition; // this doesn't extend MyVirtualMassDefinition
            if(spaceBall != null)
            {
                // HACK hardcoded
                AddLine(MyFontEnum.Green).Append("Power required*: No").EndLine();
                AddLine().Append("Max artificial mass: ").MassFormat(spaceBall.MaxVirtualMass).EndLine();
                return;
            }

            var warhead = def as MyWarheadDefinition;
            if(warhead != null)
            {
                // HACK hardcoded
                AddLine(MyFontEnum.Green).Append("Power required*: No").EndLine();
                AddLine().Append("Radius: ").DistanceFormat(warhead.ExplosionRadius).EndLine();
                AddLine().Append("Damage: ").AppendFormat("{0:#,###,###,###,##0.##}", warhead.WarheadExplosionDamage).EndLine();
                return;
            }

            var button = def as MyButtonPanelDefinition;
            if(button != null)
            {
                // HACK hardcoded; from MyButtonPanel
                float requiredPowerInput = 0.0001f;

                AddLine().Append("Power required*: ").PowerFormat(requiredPowerInput).Separator().ResourcePriority(button.ResourceSinkGroup).EndLine();
                AddLine().Append("Button count: ").Append(button.ButtonCount).EndLine();
                return;
            }

            var lcd = def as MyTextPanelDefinition;
            if(lcd != null)
            {
                AddLine().Append("Power required: ").PowerFormat(lcd.RequiredPowerInput).Separator().ResourcePriority(lcd.ResourceSinkGroup).EndLine();
                AddLine().Append("Screen resolution: ").Append(lcd.TextureResolution * lcd.TextureAspectRadio).Append("x").Append(lcd.TextureResolution).EndLine();
                return;
            }

            var camera = def as MyCameraBlockDefinition;
            if(camera != null)
            {
                AddLine().Append("Power required: ").PowerFormat(camera.RequiredPowerInput).Separator().ResourcePriority(camera.ResourceSinkGroup).EndLine();
                AddLine().Append("Field of view: ").AngleFormat(camera.MinFov).Append(" to ").AngleFormat(camera.MaxFov).EndLine();

                //var index = Math.Max(camera.OverlayTexture.LastIndexOf('/'), camera.OverlayTexture.LastIndexOf('\\')); // last / or \ char
                //AddLine().Append("Overlay texture: " + camera.OverlayTexture.Substring(index + 1));
                return;
            }

            var cargo = def as MyCargoContainerDefinition;
            if(cargo != null)
            {
                var poweredCargo = def as MyPoweredCargoContainerDefinition;
                if(poweredCargo != null)
                {
                    AddLine().Append("Power required: ").PowerFormat(poweredCargo.RequiredPowerInput).Separator().ResourcePriority(poweredCargo.ResourceSinkGroup).EndLine();
                }

                float volume = cargo.InventorySize.Volume;

                if(Math.Abs(volume) > 0.0001f || GetInventoryFromComponent(def, out volume))
                {
                    AddLine().Append("Inventory: ").InventoryFormat(volume).EndLine();
                }
                else
                {
                    // HACK hardcoded; from MyCargoContainer
                    var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                    volume = (float)def.Size.X * gridSize * (float)def.Size.Y * gridSize * (float)def.Size.Z * gridSize;

                    AddLine().Append("Inventory*: ").InventoryFormat(volume).EndLine();
                }

                return;
            }

            var sorter = def as MyConveyorSorterDefinition;
            if(sorter != null)
            {
                AddLine().Append("Power required: ").PowerFormat(sorter.PowerInput).Separator().ResourcePriority(sorter.ResourceSinkGroup).EndLine();
                AddLine().Append("Inventory: ").InventoryFormat(sorter.InventorySize.Volume).EndLine();
                return;
            }

            var gravity = def as MyGravityGeneratorBaseDefinition;
            if(gravity != null)
            {
                var gravityFlat = def as MyGravityGeneratorDefinition;
                if(gravityFlat != null)
                {
                    AddLine().Append("Max power use: ").PowerFormat(gravityFlat.RequiredPowerInput).Separator().ResourcePriority(gravityFlat.ResourceSinkGroup).EndLine();
                    AddLine().Append("Field size: ").VectorFormat(gravityFlat.MinFieldSize).Append(" to ").VectorFormat(gravityFlat.MaxFieldSize).EndLine();
                }

                var gravitySphere = def as MyGravityGeneratorSphereDefinition;
                if(gravitySphere != null)
                {
                    AddLine().Append("Base power usage: ").PowerFormat(gravitySphere.BasePowerInput).Separator().Append("Consumption: ").PowerFormat(gravitySphere.ConsumptionPower).Separator().ResourcePriority(gravitySphere.ResourceSinkGroup).EndLine();
                    AddLine().Append("Radius: ").DistanceFormat(gravitySphere.MinRadius).Append(" to ").DistanceFormat(gravitySphere.MaxRadius).EndLine();
                }

                AddLine().Append("Acceleration: ").ForceFormat(gravity.MinGravityAcceleration).Append(" to ").ForceFormat(gravity.MaxGravityAcceleration).EndLine();
                return;
            }

            var jumpDrive = def as MyJumpDriveDefinition;
            if(jumpDrive != null)
            {
                AddLine().Append("Power required: ").PowerFormat(jumpDrive.RequiredPowerInput).Separator().Append("For jump: ").PowerFormat(jumpDrive.PowerNeededForJump).Separator().ResourcePriority(jumpDrive.ResourceSinkGroup).EndLine();
                AddLine().Append("Max distance: ").DistanceFormat((float)jumpDrive.MaxJumpDistance).EndLine();
                AddLine().Append("Max mass: ").MassFormat((float)jumpDrive.MaxJumpMass).EndLine();
                AddLine().Append("Jump delay: ").TimeFormat(jumpDrive.JumpDelay).EndLine();
                return;
            }

            var merger = def as MyMergeBlockDefinition;
            if(merger != null)
            {
                // HACK hardcoded
                AddLine(MyFontEnum.Green).Append("Power required*: No").EndLine();
                AddLine().Append("Pull strength: ").AppendFormat("{0:###,###,##0.#######}", merger.Strength).EndLine();
                return;
            }

            var weapon = def as MyWeaponBlockDefinition;
            if(weapon != null)
            {
                var wepDef = MyDefinitionManager.Static.GetWeaponDefinition(weapon.WeaponDefinitionId);

                float requiredPowerInput = -1;

                if(def is MyLargeTurretBaseDefinition)
                {
                    requiredPowerInput = MyEnergyConstants.MAX_REQUIRED_POWER_TURRET; // HACK hardcoded; from MyLargeTurretBase
                }
                else
                {
                    if(defTypeId == typeof(MyObjectBuilder_SmallGatlingGun)
                    || defTypeId == typeof(MyObjectBuilder_SmallMissileLauncher)
                    || defTypeId == typeof(MyObjectBuilder_SmallMissileLauncherReload))
                        requiredPowerInput = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_GUN; // HACK hardcoded; from MySmallMissileLauncher & MySmallGatlingGun
                }

                if(requiredPowerInput > 0)
                    AddLine().Append("Power required*: ").PowerFormat(requiredPowerInput).Separator().ResourcePriority(weapon.ResourceSinkGroup).EndLine();
                else
                    AddLine().Append("Power priority: ").ResourcePriority(weapon.ResourceSinkGroup).EndLine();

                AddLine().Append("Inventory: ").InventoryFormat(weapon.InventoryMaxVolume, wepDef.AmmoMagazinesId).EndLine();

                var largeTurret = def as MyLargeTurretBaseDefinition;
                if(largeTurret != null)
                {
                    AddLine().Append("Auto-target: ").BoolFormat(largeTurret.AiEnabled).Append(largeTurret.IdleRotation ? " (With idle rotation)" : "(No idle rotation)").Separator().Append("Range: ").DistanceFormat(largeTurret.MaxRangeMeters).EndLine();
                    AddLine().Append("Speed - Pitch: ").RotationSpeed(largeTurret.ElevationSpeed * 60).Separator().Append("Yaw: ").RotationSpeed(largeTurret.RotationSpeed * 60).EndLine();
                    AddLine().Append("Rotation - Pitch: ").AngleFormatDeg(largeTurret.MinElevationDegrees).Append(" to ").AngleFormatDeg(largeTurret.MaxElevationDegrees).Separator().Append("Yaw: ").AngleFormatDeg(largeTurret.MinAzimuthDegrees).Append(" to ").AngleFormatDeg(largeTurret.MaxAzimuthDegrees).EndLine();
                }

                AddLine().Append("Reload time: ").TimeFormat(wepDef.ReloadTime / 1000).EndLine();

                AddLine().Append("Ammo: ");
                for(int i = 0; i < wepDef.AmmoMagazinesId.Length; i++)
                {
                    var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wepDef.AmmoMagazinesId[i]);
                    var ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                    var weaponData = wepDef.WeaponAmmoDatas[(int)ammo.AmmoType];

                    if(i > 0 && i % 2 == 0)
                    {
                        GetLine().EndLine();
                        AddLine().Append("       ");
                    }

                    GetLine().Append(mag.Id.SubtypeName).Append(" (").Append(weaponData.RateOfFire).Append(" RPM)").Append(", ");
                }

                GetLine().Length -= 2;
                GetLine().EndLine();
                return;
            }
        }

        private void PostProcessText(MyDefinitionId id)
        {
            if(TextAPIEnabled)
            {
                textObject.message = textAPIlines.ToString();
                textObject.origin = GetScreenPosition(largestLineWidth);
                textAPI.Send(textObject);
                textShown = true;

                cache = new CacheTextAPI(textObject.message, line, largestLineWidth, textObject.origin);

                if(cachedInfoTextAPI.ContainsKey(id))
                    cachedInfoTextAPI[id] = cache;
                else
                    cachedInfoTextAPI.Add(id, cache);

                textAPIlines.Clear();
            }
            else
            {
                long now = DateTime.UtcNow.Ticks;
                lastScroll = now + TimeSpan.TicksPerSecond;
                atLine = SCROLL_FROM_LINE;

                for(int i = line; i >= 0; --i)
                {
                    var l = notificationLines[i];

                    var textWidthPx = largestLineWidth - l.lineWidthPx;
                    int fillChars = (int)Math.Floor((float)textWidthPx / (float)SPACE_SIZE);

                    if(fillChars >= 1)
                        l.str.Append(' ', fillChars);
                }

                cache = new CacheNotifications(notificationLines);

                if(cachedInfoNotification.ContainsKey(id))
                    cachedInfoNotification[id] = cache;
                else
                    cachedInfoNotification.Add(id, cache);
            }
        }

        private Vector2D GetScreenPosition(float largestLineWidth)
        {
            if(!showMenu && !rotationHints) // right side
            {
                // TODO could use some optimization...

                var textPosition2D = new Vector2D(0, 0.98);

                var camera = MyAPIGateway.Session.Camera;
                var camMatrix = camera.WorldMatrix;
                var fov = camera.FovWithZoom;

                // math from textAPI's sources
                var localscale = 0.1735 * Math.Tan(fov / 2);
                var localXmul = localscale * (aspectRatio * 9d / 16d);
                var localYmul = localscale * (1 / aspectRatio) * localXmul;

                var textpos = Vector3D.Transform(new Vector3D(textPosition2D.X * localXmul, textPosition2D.Y * localYmul, -0.1), camMatrix);

                var widthStep = localscale * 9d * 0.001075;
                var width = largestLineWidth * widthStep;

                var screenPosStart = camera.WorldToScreen(ref textpos);
                textpos += camMatrix.Right * width;
                var screenPosEnd = camera.WorldToScreen(ref textpos);
                var panelSize = screenPosEnd - screenPosStart;

                textPosition2D.X = (1.01 - (panelSize.X * 1.99));

                if(aspectRatio > 5) // really wide resolution (3+ monitors)
                    textPosition2D.X /= 3.0;

                return textPosition2D;
            }
            else // left side or menu
            {
                return (aspectRatio > 5 ? (showMenu ? MENU_HUDPOS_WIDE : TEXT_HUDPOS_WIDE) : (showMenu ? MENU_HUDPOS : TEXT_HUDPOS));
            }
        }

        private void UpdateVisualText()
        {
            if(TextAPIEnabled)
            {
                // show last generated block info message
                if(!textShown && !string.IsNullOrEmpty(textObject.message))
                {
                    var cacheTextAPI = (CacheTextAPI)cache;

                    cacheTextAPI.ResetExpiry();
                    cacheTextAPI.ComputePositionIfNeeded();

                    textObject.origin = cacheTextAPI.screenPos;
                    textObject.message = cacheTextAPI.text;
                    textAPI.Send(textObject);
                    textShown = true;
                }
            }
            else
            {
                // print and scroll through HUD notification types of messages, not needed for text API

                if(!textShown)
                {
                    textShown = true;
                    cache.ResetExpiry();
                }

                var hudLines = ((CacheNotifications)cache).lines;
                int lines = 0;

                foreach(var hud in hudLines)
                {
                    if(hud.Text.Length > 0)
                        lines++;

                    hud.Hide();
                }

                if(lines > MAX_LINES)
                {
                    int l;

                    for(l = 0; l < lines; l++)
                    {
                        var hud = hudLines[l];

                        if(l < SCROLL_FROM_LINE)
                        {
                            hud.ResetAliveTime();
                            hud.Show();
                        }
                    }

                    int d = SCROLL_FROM_LINE;
                    l = atLine;

                    while(d < MAX_LINES)
                    {
                        var hud = hudLines[l];

                        if(hud.Text.Length == 0)
                            break;

                        hud.ResetAliveTime();
                        hud.Show();

                        if(++l >= lines)
                            l = SCROLL_FROM_LINE;

                        d++;
                    }

                    long now = DateTime.UtcNow.Ticks;

                    if(lastScroll < now)
                    {
                        if(++atLine >= lines)
                            atLine = SCROLL_FROM_LINE;

                        lastScroll = now + (long)(TimeSpan.TicksPerSecond * 1.5f);
                    }
                }
                else
                {
                    for(int l = 0; l < lines; l++)
                    {
                        var hud = hudLines[l];
                        hud.ResetAliveTime();
                        hud.Show();
                    }
                }
            }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(isThisDS) // failsafe in case the component is still updating or not removed...
                    return;

                if(!init)
                {
                    if(MyAPIGateway.Session == null)
                        return;

                    Init();
                }

                unchecked // global ticker
                {
                    ++tick;
                }

                if(leakInfo != null) // update the leak info component
                    leakInfo.Update();

                // HUD toggle monitor; required here because it gets the previous value if used in HandleInput()
                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOGGLE_HUD))
                    hudVisible = !MyAPIGateway.Session.Config.MinimalHud;

                #region Cubebuilder monitor
                var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;

                if(def != null && MyCubeBuilder.Static.IsActivated)
                {
                    if(showMenu)
                    {
                        if(menuNeedsUpdate)
                        {
                            lastDefId = DEFID_MENU;
                            menuNeedsUpdate = false;
                            textShown = false;

                            GenerateMenuText();
                            PostProcessText(DEFID_MENU);
                        }
                    }
                    else
                    {
                        if(def.Id != lastDefId)
                        {
                            lastDefId = def.Id;

                            if(TextAPIEnabled ? cachedInfoTextAPI.TryGetValue(def.Id, out cache) : cachedInfoNotification.TryGetValue(def.Id, out cache))
                            {
                                textShown = false; // make the textAPI update
                            }
                            else
                            {
                                GenerateBlockText(def);
                                PostProcessText(def.Id);
                            }
                        }
                    }

                    UpdateVisualText();

                    if(MyAPIGateway.CubeBuilder.FreezeGizmo)
                    {
                        // TODO needed to monitor distance?
                    }
                }
                else
                {
                    if(MyAPIGateway.CubeBuilder.FreezeGizmo)
                        SetFreezeGizmo(false);

                    showMenu = false;
                    HideText();
                }
                #endregion

                #region Purge cache
                if(tick % 60 == 0)
                {
                    var haveNotifCache = cachedInfoNotification.Count > 0;
                    var haveTextAPICache = cachedInfoTextAPI.Count > 0;

                    if(haveNotifCache || haveTextAPICache)
                    {
                        removeCacheIds.Clear();
                        var time = DateTime.UtcNow.Ticks;

                        if(haveNotifCache)
                        {
                            foreach(var kv in cachedInfoNotification)
                                if(kv.Value.expires < time)
                                    removeCacheIds.Add(kv.Key);

                            if(cachedInfoNotification.Count == removeCacheIds.Count)
                                cachedInfoNotification.Clear();
                            else
                                foreach(var key in removeCacheIds)
                                    cachedInfoNotification.Remove(key);

                            removeCacheIds.Clear();
                        }

                        if(haveTextAPICache)
                        {
                            foreach(var kv in cachedInfoTextAPI)
                                if(kv.Value.expires < time)
                                    removeCacheIds.Add(kv.Key);

                            if(cachedInfoTextAPI.Count == removeCacheIds.Count)
                                cachedInfoTextAPI.Clear();
                            else
                                foreach(var key in removeCacheIds)
                                    cachedInfoTextAPI.Remove(key);

                            removeCacheIds.Clear();
                        }
                    }
                }
                #endregion
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void HideText()
        {
            if(textShown)
            {
                textShown = false;
                lastDefId = default(MyDefinitionId);

                // text API hide
                textAPI.Send(TEXTOBJ_EMPTY);

                // HUD notifications don't need hiding, they expire in one frame.
            }
        }

        private void ResetLines()
        {
            if(TextAPIEnabled)
            {
                textAPIlines.Clear();
            }
            else
            {
                foreach(var l in notificationLines)
                    l.str.Clear();
            }

            line = -1;
            largestLineWidth = 0;
            lastNewLineIndex = 0;
            addLineCalled = false;
        }

        private StringBuilder AddLine(string font = MyFontEnum.White)
        {
            EndAddedLines();
            addLineCalled = true;

            ++line;

            if(TextAPIEnabled)
            {
                switch(font)
                {
                    case MyFontEnum.White: textAPIlines.AddIgnored("<color=255,255,255>"); break;
                    case MyFontEnum.Red: textAPIlines.AddIgnored("<color=255,200,0>"); break;
                    case MyFontEnum.Green: textAPIlines.AddIgnored("<color=0,200,0>"); break;
                    case MyFontEnum.Blue: textAPIlines.AddIgnored("<color=0,120,220>"); break; // previously <color=180,210,230>
                    case MyFontEnum.DarkBlue: textAPIlines.AddIgnored("<color=0,50,150>"); break; // previously <color=110,180,225>
                    case MyFontEnum.ErrorMessageBoxCaption: textAPIlines.AddIgnored("<color=255,0,0>"); break;
                }

                return textAPIlines;
            }
            else
            {
                if(line >= notificationLines.Count)
                    notificationLines.Add(new HudLine());

                var nl = notificationLines[line];
                nl.font = font;

                return nl.str.Append("• ");
            }
        }

        public void EndAddedLines()
        {
            if(!addLineCalled)
                return;

            addLineCalled = false;

            if(TextAPIEnabled)
            {
                var px = GetStringSizeTextAPI(textAPIlines, startIndex: lastNewLineIndex) - ignorePx;

                largestLineWidth = Math.Max(largestLineWidth, px);

                lastNewLineIndex = textAPIlines.Length;
                textAPIlines.Append('\n');
            }
            else
            {
                var px = (int)(GetStringSizeNotif(notificationLines[line].str) - ignorePx);

                largestLineWidth = Math.Max(largestLineWidth, px);

                notificationLines[line].lineWidthPx = px;
            }

            ignorePx = 0;
        }

        private StringBuilder GetLine()
        {
            return (TextAPIEnabled ? textAPIlines : notificationLines[line].str);
        }

        public static float GetStringSizeTextAPI(StringBuilder s, int length = 0, int startIndex = 0)
        {
            var dict = instance.textAPI.FontDict;

            if(length > 0)
                startIndex = (s.Length - length);

            int endLength = s.Length;
            int width;
            float totalWidth = 0;

            for(int i = startIndex; i < endLength; ++i)
            {
                var c = s[i];

                if(dict.TryGetValue(c, out width))
                    totalWidth += (width / 45f) * (float)TEXT_SCALE * 1.2f;
                else
                    totalWidth += (15f / 45f) * (float)TEXT_SCALE * 1.2f;
            }

            return totalWidth;
        }

        public static int GetStringSizeNotif(StringBuilder builder)
        {
            int endLength = builder.Length;
            int len;
            int size = 0;

            for(int i = 0; i < endLength; ++i)
            {
                if(instance.charSize.TryGetValue(builder[i], out len))
                    size += len;
                else
                    size += 15;
            }

            return size;
        }

        /// <summary>
        /// Returns true if specified definition has all faces fully airtight.
        /// The referenced arguments are assigned with the said values which should only really be used if it returns false (due to the quick escape return true).
        /// An fully airtight face means it keeps the grid airtight when the face is the only obstacle between empty void and the ship's interior.
        /// Due to the complexity of airtightness when connecting blocks, this method simply can not indicate that, that's what the mount points view is for.
        /// </summary>
        private bool IsAirTight(MyCubeBlockDefinition def, ref int airTightFaces, ref int totalFaces)
        {
            if(def.IsAirTight)
                return true;

            airTightFaces = 0;
            totalFaces = 0;
            cubes.Clear();

            foreach(var kv in def.IsCubePressurized)
            {
                cubes.Add(kv.Key);
            }

            foreach(var kv in def.IsCubePressurized)
            {
                foreach(var kv2 in kv.Value)
                {
                    if(cubes.Contains(kv.Key + kv2.Key))
                        continue;

                    if(kv2.Value)
                        airTightFaces++;

                    totalFaces++;
                }
            }

            cubes.Clear();
            return (airTightFaces == totalFaces);
        }

        /// <summary>
        /// Gets the inventory volume from the EntityComponents and EntityContainers definitions.
        /// </summary>
        private static bool GetInventoryFromComponent(MyDefinitionBase def, out float volume)
        {
            volume = 0;
            MyContainerDefinition containerDef;

            if(MyDefinitionManager.Static.TryGetContainerDefinition(def.Id, out containerDef) && containerDef.DefaultComponents != null)
            {
                MyComponentDefinitionBase compDefBase;

                foreach(var compPointer in containerDef.DefaultComponents)
                {
                    if(compPointer.BuilderType == typeof(MyObjectBuilder_Inventory) && MyComponentContainerExtension.TryGetComponentDefinition(compPointer.BuilderType, compPointer.SubtypeId.GetValueOrDefault(def.Id.SubtypeId), out compDefBase))
                    {
                        var invComp = compDefBase as MyInventoryComponentDefinition;

                        if(invComp != null && invComp.Id.SubtypeId == def.Id.SubtypeId)
                        {
                            volume = invComp.Volume;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the key/button name assigned to the specified control.
        /// </summary>
        private string GetControlAssignedName(MyStringId controlId)
        {
            var control = MyAPIGateway.Input.GetGameControl(controlId);

            if(control.GetKeyboardControl() != MyKeys.None)
                return control.GetKeyboardControl().ToString();
            else if(control.GetSecondKeyboardControl() != MyKeys.None)
                return control.GetSecondKeyboardControl().ToString();
            else if(control.GetMouseControl() != MyMouseButtonsEnum.None)
                return MyAPIGateway.Input.GetName(control.GetMouseControl());

            return null;
        }

        #region Classes for storing generated info
        class Cache
        {
            public long expires;

            public void ResetExpiry()
            {
                expires = DateTime.UtcNow.Ticks + (TimeSpan.TicksPerSecond * CACHE_EXPIRE_SECONDS);
            }
        }

        class CacheTextAPI : Cache
        {
            public string text = null;
            public Vector2D screenPos;
            public int numLines = 0;
            public float largestLineWidth;

            public CacheTextAPI(string text, int numLines, float largestLineWidth, Vector2D screenPos)
            {
                ResetExpiry();
                this.text = text;
                this.numLines = numLines;
                this.largestLineWidth = largestLineWidth;
                this.screenPos = screenPos;
            }

            public void InvalidateScreenPosition()
            {
                screenPos.X = -1;
                screenPos.Y = -1;
            }

            public void ComputePositionIfNeeded()
            {
                if(screenPos.X < -0.5 && screenPos.Y < -0.5)
                    screenPos = instance.GetScreenPosition(largestLineWidth);
            }
        }

        class CacheNotifications : Cache
        {
            public List<IMyHudNotification> lines = null;

            public CacheNotifications(List<HudLine> hudLines)
            {
                ResetExpiry();
                lines = new List<IMyHudNotification>();

                foreach(var hl in hudLines)
                {
                    if(hl.str.Length > 0)
                        lines.Add(MyAPIGateway.Utilities.CreateNotification(hl.str.ToString(), 16, hl.font));
                }
            }
        }

        class HudLine
        {
            public StringBuilder str = new StringBuilder();
            public string font;
            public int lineWidthPx;
        }
        #endregion

        #region Non-definition block data
        public interface IBlockData { }

        public class BlockDataThrust : IBlockData
        {
            public readonly float radius;
            public readonly float distance;
            public readonly int flames;

            public BlockDataThrust(MyThrust thrust)
            {
                var def = thrust.BlockDefinition;
                double distSq = 0;

                // HACK hardcoded; from MyThrust.UpdateThrustFlame()
                thrust.ThrustLengthRand = 10f * def.FlameLengthScale; // make the GetDamageCapsuleLine() method think it thrusts at max and with no random

                var m = thrust.WorldMatrix;

                foreach(var flame in thrust.Flames)
                {
                    var flameLine = thrust.GetDamageCapsuleLine(flame, ref m);
                    var flameDistSq = (flameLine.From - flameLine.To).LengthSquared();

                    if(flameDistSq > distSq)
                    {
                        distSq = flameDistSq;
                        radius = flame.Radius;
                    }
                }

                distance = (float)Math.Sqrt(distSq);
                flames = thrust.Flames.Count;

                instance.blockData.Add(def.Id, this);
            }
        }

        /// <summary>
        /// Spawns a ghost grid with the requested block definition, used for getting data that is only obtainable from a placed block.
        /// </summary>
        private static MyCubeBlock SpawnFakeBlock(MyCubeBlockDefinition def)
        {
            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var spawnPos = camMatrix.Translation + camMatrix.Backward * 10;
            var fakeGridObj = new MyObjectBuilder_CubeGrid()
            {
                CreatePhysics = false,
                PersistentFlags = MyPersistentEntityFlags2.None,
                IsStatic = true,
                GridSizeEnum = def.CubeSize,
                Editable = false,
                DestructibleBlocks = false,
                IsRespawnGrid = false,
                PositionAndOrientation = new MyPositionAndOrientation(spawnPos, Vector3.Forward, Vector3.Up),
                CubeBlocks = new List<MyObjectBuilder_CubeBlock>()
                {
                    new MyObjectBuilder_CubeBlock()
                    {
                        SubtypeName = def.Id.SubtypeName,
                    }
                },
            };

            MyEntities.RemapObjectBuilder(fakeGridObj);
            var fakeEnt = MyEntities.CreateFromObjectBuilderNoinit(fakeGridObj);
            fakeEnt.IsPreview = true;
            fakeEnt.Save = false;
            fakeEnt.Render.Visible = false;
            fakeEnt.Flags = EntityFlags.None;
            MyEntities.InitEntity(fakeGridObj, ref fakeEnt);

            var fakeGrid = (IMyCubeGrid)fakeEnt;
            var fakeBlock = fakeGrid.GetCubeBlock(Vector3I.Zero)?.FatBlock as MyCubeBlock;
            fakeGrid.Close();
            return fakeBlock;
        }
        #endregion

        #region Notification font character width data
        private void ComputeCharacterSizes()
        {
            charSize.Clear();

            // generated from fonts/white_shadow/FontData.xml
            AddCharsSize(" !I`ijl ¡¨¯´¸ÌÍÎÏìíîïĨĩĪīĮįİıĵĺļľłˆˇ˘˙˚˛˜˝ІЇії‹›∙", 8);
            AddCharsSize("\"-rª­ºŀŕŗř", 10);
            AddCharsSize("#0245689CXZ¤¥ÇßĆĈĊČŹŻŽƒЁЌАБВДИЙПРСТУХЬ€", 19);
            AddCharsSize("$&GHPUVY§ÙÚÛÜÞĀĜĞĠĢĤĦŨŪŬŮŰŲОФЦЪЯжы†‡", 20);
            AddCharsSize("%ĲЫ", 24);
            AddCharsSize("'|¦ˉ‘’‚", 6);
            AddCharsSize("(),.1:;[]ft{}·ţťŧț", 9);
            AddCharsSize("*²³¹", 11);
            AddCharsSize("+<=>E^~¬±¶ÈÉÊË×÷ĒĔĖĘĚЄЏЕНЭ−", 18);
            AddCharsSize("/ĳтэє", 14);
            AddCharsSize("3FKTabdeghknopqsuy£µÝàáâãäåèéêëðñòóôõöøùúûüýþÿāăąďđēĕėęěĝğġģĥħĶķńņňŉōŏőśŝşšŢŤŦũūŭůűųŶŷŸșȚЎЗКЛбдекруцяёђћўџ", 17);
            AddCharsSize("7?Jcz¢¿çćĉċčĴźżžЃЈЧавийнопсъьѓѕќ", 16);
            AddCharsSize("@©®мшњ", 25);
            AddCharsSize("ABDNOQRSÀÁÂÃÄÅÐÑÒÓÔÕÖØĂĄĎĐŃŅŇŌŎŐŔŖŘŚŜŞŠȘЅЊЖф□", 21);
            AddCharsSize("L_vx«»ĹĻĽĿŁГгзлхчҐ–•", 15);
            AddCharsSize("MМШ", 26);
            AddCharsSize("WÆŒŴ—…‰", 31);
            AddCharsSize("\\°“”„", 12);
            AddCharsSize("mw¼ŵЮщ", 27);
            AddCharsSize("½Щ", 29);
            AddCharsSize("¾æœЉ", 28);
            AddCharsSize("ю", 23);
            AddCharsSize("ј", 7);
            AddCharsSize("љ", 22);
            AddCharsSize("ґ", 13);
            AddCharsSize("™", 30);
            AddCharsSize("", 40);
            AddCharsSize("", 41);
            AddCharsSize("", 32);
            AddCharsSize("", 34);
        }

        private void AddCharsSize(string chars, int size)
        {
            for(int i = 0; i < chars.Length; i++)
            {
                charSize.Add(chars[i], size);
            }
        }
        #endregion

        #region Resource group priorities
        private void ComputeResourceGroups()
        {
            resourceGroupPriority.Clear();
            resourceSourceGroups = 0;
            resourceSinkGroups = 0;

            var groupDefs = MyDefinitionManager.Static.GetDefinitionsOfType<MyResourceDistributionGroupDefinition>();
            var orderedGroups = from def in groupDefs
                                orderby def.Priority
                                select def;

            foreach(var group in orderedGroups)
            {
                resourceGroupPriority.Add(group.Id.SubtypeId, new ResourceGroupData()
                {
                    def = group,
                    priority = (group.IsSource ? ++resourceSourceGroups : ++resourceSinkGroups),
                });
            }
        }

        public struct ResourceGroupData
        {
            public MyResourceDistributionGroupDefinition def;
            public int priority;
        }
        #endregion
    }

    #region Extensions
    static class Extensions
    {
        public static StringBuilder Separator(this StringBuilder s)
        {
            return s.Append(", ");
        }

        public static void EndLine(this StringBuilder s)
        {
            BuildInfo.instance.EndAddedLines();
        }

        public static StringBuilder BoolFormat(this StringBuilder s, bool b)
        {
            return s.Append(b ? "Yes" : "No");
        }

        public static StringBuilder AddIgnored(this StringBuilder s, string text)
        {
            s.Append(text);
            BuildInfo.instance.ignorePx += BuildInfo.GetStringSizeTextAPI(s, length: text.Length);
            return s;
        }

        public static StringBuilder ResourcePriority(this StringBuilder s, string groupName, bool hardcoded = false) // HACK some ResourceSinkGroup are string type for SOME reason
        {
            return s.ResourcePriority(MyStringHash.GetOrCompute(groupName), hardcoded);
        }

        public static StringBuilder ResourcePriority(this StringBuilder s, MyStringHash groupId, bool hardcoded = false)
        {
            s.Append("Priority");

            if(hardcoded)
                s.Append('*');

            s.Append(": ");

            BuildInfo.ResourceGroupData data;

            if(groupId == null || !BuildInfo.instance.resourceGroupPriority.TryGetValue(groupId, out data))
                s.Append("(Undefined)");
            else
                s.Append(groupId.String).Append(" (").Append(data.priority).Append("/").Append(data.def.IsSource ? BuildInfo.instance.resourceSourceGroups : BuildInfo.instance.resourceSinkGroups).Append(")");

            return s;
        }

        public static StringBuilder ForceFormat(this StringBuilder s, float N)
        {
            if(N > 1000000)
                return s.NumFormat(N / 1000000, 3).Append(" MN");

            if(N > 1000)
                return s.NumFormat(N / 1000, 3).Append(" kN");

            return s.NumFormat(N, 3).Append(" N");
        }

        public static StringBuilder RotationSpeed(this StringBuilder s, float radPerSecond)
        {
            return s.Append(Math.Round(MathHelper.ToDegrees(radPerSecond), 2)).Append("°/s");
        }

        public static StringBuilder TorqueFormat(this StringBuilder s, float N)
        {
            return s.NumFormat(N, 3).Append("NM");
        }

        public static StringBuilder PowerFormat(this StringBuilder s, float MW)
        {
            float W = MW * 1000000f;

            if(W > 1000000)
                return s.NumFormat(MW, 3).Append(" MW");
            if(W > 1000)
                return s.NumFormat(W / 1000f, 3).Append(" kW");

            return s.NumFormat(W, 3).Append(" W");
        }

        public static StringBuilder PowerStorageFormat(this StringBuilder s, float MW)
        {
            return s.PowerFormat(MW).Append("h");
        }

        public static StringBuilder DistanceFormat(this StringBuilder s, float m)
        {
            if(m > 1000)
                return s.NumFormat(m / 1000, 3).Append(" km");

            if(m < 10)
                return s.NumFormat(m, 3).Append(" m");

            return s.Append((int)m).Append(" m");
        }

        public static StringBuilder MassFormat(this StringBuilder s, float kg)
        {
            if(kg > 1000000)
                return s.Append((int)(kg / 1000000)).Append(" MT");

            if(kg > 1000)
                return s.Append((int)(kg / 1000)).Append(" T");

            if(kg < 1f)
                return s.Append((int)(kg * 1000)).Append(" g");

            return s.Append((int)kg).Append(" kg");
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyInventoryConstraint inputConstraint, MyInventoryConstraint outputConstraint)
        {
            var types = new HashSet<MyObjectBuilderType>(inputConstraint.ConstrainedTypes);
            types.UnionWith(outputConstraint.ConstrainedTypes);

            var items = new HashSet<MyDefinitionId>(inputConstraint.ConstrainedIds);
            items.UnionWith(outputConstraint.ConstrainedIds);

            return s.InventoryFormat(volume, types: types, items: items, isWhitelist: inputConstraint.IsWhitelist); // HACK only using input constraint's whitelist status, not sure if output inventory's whitelist is needed
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, MyInventoryConstraint inventoryConstraint)
        {
            return s.InventoryFormat(volume,
                types: new HashSet<MyObjectBuilderType>(inventoryConstraint.ConstrainedTypes),
                items: new HashSet<MyDefinitionId>(inventoryConstraint.ConstrainedIds),
                isWhitelist: inventoryConstraint.IsWhitelist);
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, params MyObjectBuilderType[] allowedTypesParams)
        {
            return s.InventoryFormat(volume, types: new HashSet<MyObjectBuilderType>(allowedTypesParams));
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, params MyDefinitionId[] allowedItems)
        {
            return s.InventoryFormat(volume, items: new HashSet<MyDefinitionId>(allowedItems));
        }

        public static StringBuilder InventoryFormat(this StringBuilder s, float volume, HashSet<MyObjectBuilderType> types = null, HashSet<MyDefinitionId> items = null, bool isWhitelist = true)
        {
            var mul = MyAPIGateway.Session.InventoryMultiplier;

            MyValueFormatter.AppendVolumeInBestUnit(volume * mul, s);

            if(Math.Abs(mul - 1) > 0.001f)
                s.Append(" (x").Append(Math.Round(mul, 2)).Append(")");

            if(types == null && items == null)
                types = BuildInfo.instance.DEFAULT_ALLOWED_TYPES;

            var physicalItems = MyDefinitionManager.Static.GetPhysicalItemDefinitions();
            var minMass = float.MaxValue;
            var maxMass = 0f;

            foreach(var item in physicalItems)
            {
                if(!item.Public || item.Mass <= 0 || item.Volume <= 0)
                    continue; // skip hidden and physically impossible items

                if((types != null && isWhitelist == types.Contains(item.Id.TypeId)) || (items != null && isWhitelist == items.Contains(item.Id)))
                {
                    var fillMass = item.Mass * (volume / item.Volume);
                    minMass = Math.Min(fillMass, minMass);
                    maxMass = Math.Max(fillMass, maxMass);
                }
            }

            if(minMass != float.MaxValue && maxMass != 0)
            {
                if(Math.Abs(minMass - maxMass) > 0.00001f)
                    s.Append(", Cargo mass: ").MassFormat(minMass).Append(" to ").MassFormat(maxMass);
                else
                    s.Append(", Max cargo mass: ").MassFormat(minMass);
            }

            return s;
        }

        public static StringBuilder TimeFormat(this StringBuilder s, float seconds)
        {
            //if(seconds > 60)
            //    return String.Format("{0:0}m {1:0.##}s", (seconds / 60), (seconds % 60));
            //else
            return s.AppendFormat("{0:0.##}s", seconds);
        }

        public static StringBuilder AngleFormat(this StringBuilder s, float radians)
        {
            return s.AngleFormatDeg(MathHelper.ToDegrees(radians));
        }

        public static StringBuilder AngleFormatDeg(this StringBuilder s, float degrees)
        {
            return s.Append((int)degrees).Append('°');
        }

        public static StringBuilder VectorFormat(this StringBuilder s, Vector3 vec)
        {
            return s.Append(vec.X).Append('x').Append(vec.Y).Append('x').Append(vec.Z);
        }

        public static StringBuilder SpeedFormat(this StringBuilder s, float mps)
        {
            return s.NumFormat(mps, 3).Append(" m/s");
        }

        public static StringBuilder PercentFormat(this StringBuilder s, float ratio)
        {
            return s.Append((int)(ratio * 100)).Append('%');
        }

        public static StringBuilder MultiplierFormat(this StringBuilder s, float mul)
        {
            if(Math.Abs(mul - 1f) > 0.001f)
                s.Append(" (x").NumFormat(mul, 2).Append(")");

            return s;
        }

        public static StringBuilder IdTypeFormat(this StringBuilder s, MyObjectBuilderType type)
        {
            var typeName = type.ToString();
            var index = typeName.IndexOf('_') + 1;
            s.Append(typeName, index, typeName.Length - index);
            return s;
        }

        public static StringBuilder IdTypeSubtypeFormat(this StringBuilder s, MyDefinitionId id)
        {
            s.IdTypeFormat(id.TypeId).Append("/").Append(id.SubtypeName);
            return s;
        }

        public static StringBuilder ModFormat(this StringBuilder s, MyModContext context)
        {
            s.Append(context.ModName);

            // HACK workaround for MyModContext not having workshop ID as ulong... only filename which is unreliable in determining that
            var mod = MyAPIGateway.Session.Mods.First((m) => m.Name == context.ModId);
            if(mod.PublishedFileId != 0)
                s.Append(" (workshop: ").Append(mod.PublishedFileId).Append(")");

            return s;
        }

        public static StringBuilder NumFormat(this StringBuilder s, float f, int d)
        {
            return s.Append(Math.Round(f, d));
        }
    }
    #endregion

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), useEntityUpdate: true)]
    public class ThrustBlock : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                Entity.Components.Remove<ThrustBlock>(); // no longer needing this component past this first update
                
                if(BuildInfo.instance != null && !BuildInfo.instance.isThisDS) // only rendering players need to use this, DS has none so skipping it; also instance is null on DS but checking just in case
                {
                    var block = (MyThrust)Entity;

                    if(!BuildInfo.instance.blockData.ContainsKey(block.BlockDefinition.Id) && ((IMyModel)block.Model).AssetName == block.BlockDefinition.Model)
                        new BuildInfo.BlockDataThrust(block);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}