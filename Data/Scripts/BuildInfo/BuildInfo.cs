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
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

using Draygo.API;

namespace Digi.BuildInfo
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class BuildInfo : MySessionComponentBase
    {
        #region Constants
        private const string MOD_NAME = "Build Info";
        private const ulong MOD_WORKSHOP_ID = 514062285;
        private const string MOD_SHORTNAME = "BuildInfo";

        public const int CACHE_EXPIRE_SECONDS = 60 * 5; // how long a cached string remains stored until it's purged.
        private const double FREEZE_MAX_DISTANCE_SQ = 50 * 50; // max distance allowed to go from the frozen block preview before it gets turned off.
        private const int SPACE_SIZE = 8; // space character's width; used in HUD notification view mode.
        private const int MAX_LINES = 8; // max amount of HUD notification lines to print; used in HUD notification view mode.
        private const int SCROLL_FROM_LINE = 2; // ignore lines to this line when scrolling, to keep important stuff like mass in view at all times; used in HUD notification view mode.

        private readonly Vector2D TEXT_HUDPOS = new Vector2D(-0.97, 0.8); // textAPI default left side position
        private readonly Vector2D TEXT_HUDPOS_WIDE = new Vector2D(-0.97 / 3f, 0.8); // textAPI default left side position when using a really wide resolution
        private readonly Vector2D TEXT_HUDPOS_RIGHT = new Vector2D(0.97, 0.97); // textAPI default right side position
        private readonly Vector2D TEXT_HUDPOS_RIGHT_WIDE = new Vector2D(0.97 / 3f, 0.97); // textAPI default right side position when using a really wide resolution

        private const float BACKGROUND_EDGE = 0.02f; // added padding edge around the text boundary for the background image
        private readonly MyStringId MATERIAL_BACKGROUND = MyStringId.GetOrCompute("BuildInfo_TextBackground");
        private readonly MyStringId MATERIAL_SQUARE = MyStringId.GetOrCompute("BuildInfo_Square");
        private readonly MyStringId MATERIAL_WHITEDOT = MyStringId.GetOrCompute("WhiteDot");

        private readonly MyDefinitionId DEFID_MENU = new MyDefinitionId(typeof(MyObjectBuilder_GuiScreen)); // just a random non-block type to use as the menu's ID
        private readonly MyCubeBlockDefinition.MountPoint[] BLANK_MOUNTPOINTS = new MyCubeBlockDefinition.MountPoint[0];
        private BoundingBoxD unitBB = new BoundingBoxD(Vector3D.One / -2d, Vector3D.One / 2d);

        private const double MOUNTPOINT_THICKNESS = 0.05;
        private const float MOUNTPOINT_ALPHA = 0.65f;
        private Color MOUNTPOINT_COLOR = new Color(255, 255, 0) * MOUNTPOINT_ALPHA;
        private Color MOUNTPOINT_DEFAULT_COLOR = new Color(255, 200, 0) * MOUNTPOINT_ALPHA;
        private Color AIRTIGHT_COLOR = new Color(0, 155, 255) * MOUNTPOINT_ALPHA;
        private Color AIRTIGHT_TOGGLE_COLOR = new Color(0, 255, 155) * MOUNTPOINT_ALPHA;

        private const double TEXT_SHADOW_OFFSET = 0.007;
        private const double TEXT_SHADOW_OFFSET_Z = 0.01;
        private readonly Color TEXT_SHADOW_COLOR = Color.Black * 0.9f;

        public readonly Color COLOR_BLOCKTITLE = new Color(50, 155, 255);
        public readonly Color COLOR_BLOCKVARIANTS = new Color(255, 233, 55);
        public readonly Color COLOR_NORMAL = Color.White;
        public readonly Color COLOR_GOOD = Color.Lime;
        public readonly Color COLOR_BAD = Color.Red;
        public readonly Color COLOR_WARNING = Color.Yellow;
        public readonly Color COLOR_UNIMPORTANT = Color.Gray;
        public readonly Color COLOR_PART = new Color(55, 255, 155);
        public readonly Color COLOR_MOD = Color.DeepSkyBlue;

        public readonly Color COLOR_STAT_PROJECTILECOUNT = new Color(0, 255, 0);
        public readonly Color COLOR_STAT_SHIPDMG = new Color(0, 255, 200);
        public readonly Color COLOR_STAT_CHARACTERDMG = new Color(255, 155, 0);
        public readonly Color COLOR_STAT_HEADSHOTDMG = new Color(255, 0, 0);
        public readonly Color COLOR_STAT_SPEED = new Color(0, 200, 255);
        public readonly Color COLOR_STAT_TRAVEL = new Color(55, 80, 255);

        public readonly HashSet<MyObjectBuilderType> DEFAULT_ALLOWED_TYPES = new HashSet<MyObjectBuilderType>() // used in inventory formatting if type argument is null
        {
            typeof(MyObjectBuilder_Ore),
            typeof(MyObjectBuilder_Ingot),
            typeof(MyObjectBuilder_Component)
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
        }

        private string[] AXIS_LABELS = new string[]
        {
            "Forward",
            "Right",
            "Up",
        };

        private string[] DRAW_VOLUME_TYPE = new string[]
        {
            "OFF",
            "Airtightness",
            "Mounting",
        };

        private const string HELP =
            "Chat commands:\n" +
            "  /buildinfo\n    shows this window or menu if you're holding a block.\n" +
            "  /buildinfo help\n    shows this window.\n" +
            "  /buildinfo reload\n    reloads the config.\n" +
            "  /buildinfo clearcache\n    clears the block info cache, not for normal use.\n" +
            "\n" +
            "\n" +
            "The config is located in:\n" +
            "%appdata%\\SpaceEngineers\\Storage\\514062285.sbm_BuildInfo\\settings.cfg\n" +
            "\n" +
            "\n" +
            "The asterisks on the labels (e.g. Power usage*: 10 W) means\n" +
            "  that the value is calculated from hardcoded values taken\n" +
            "  from the game source, they might become inaccurate with updates.\n" +
            "\n" +
            "\n" +
            "Mount points & airtightness explained:\n" +
            "\n" +
            "  Mount points define areas that can be attached to other\n" +
            "    block's mount points.\n" +
            "\n" +
            "  Airtightness also uses the mount points system, if a\n" +
            "    mount point spans accross an entire grid cell face\n" +
            "    then that face is airtight.\n" +
            "\n" +
            "\n" +
            "[1] Laser antenna power usage is linear up to 200km.\n" +
            "\n" +
            "  After that it's a weird formula that I don't really understand:\n" +
            "\n" +
            "  defPowerLasing = 10MW for largeship laser antenna\n" +
            "  n1 = defPowerLasing / 2 / 200000\n" +
            "  n2 = defPowerLasing * 200000 - n2 * 200000 * 200000\n" +
            "  powerUsage = (rangeSquared * n2 + n3) / 1000000\n" +
            "\n";

        private readonly Vector3[] DIRECTIONS = new Vector3[] // NOTE: order is important, corresponds to +X, -X, +Y, -Y, +Z, -Z
        {
            Vector3.Right,
            Vector3.Left,
            Vector3.Up,
            Vector3.Down,
            Vector3.Backward,
            Vector3.Forward,
        };
        #endregion

        #region Fields
        public static BuildInfo instance = null;
        public bool init = false;
        public bool isThisDS = false;
        public Settings settings = null;
        public LeakInfo leakInfo = null; // leak info component
        private short tick = 0; // global incrementing gamelogic tick
        private int drawVolumeType = 0;
        private bool blockDataDraw = false;
        private bool doorAirtightBlink = false;
        private int doorAirtightBlinkTick = 0;
        private MyDefinitionId lastDefId; // last selected definition ID, can be set to MENU_DEFID too!
        public MyCubeBlockDefinition selectedDef = null;
        public BlockDataBase selectedBlockData = null;
        private bool useTextAPI = true; // the user's preference for textAPI or notification; use TextAPIEnabled to determine if you need to use textAPI or not!
        private bool textAPIresponded = false; // if textAPI.Heartbeat returned true yet
        public bool TextAPIEnabled { get { return (useTextAPI && textAPI != null && textAPI.Heartbeat); } }
        private Cache cache = null; // currently selected cache to avoid another dictionary lookup in Draw()
        private bool textShown = false;
        private Vector3D lastGizmoPosition;
        private IMyHudNotification buildInfoNotification = null;
        private IMyHudNotification mountPointsNotification = null;
        private IMyHudNotification transparencyNotification = null;
        private IMyHudNotification freezeGizmoNotification = null;
        private IMyHudNotification unsupportedGridSizeNotification = null;

        // menu specific stuff
        private bool showMenu = false;
        private bool menuNeedsUpdate = true;
        private int menuSelectedItem = 0;
        private Vector3 previousMove = Vector3.Zero;

        // used by the textAPI view mode
        public HudAPIv2 textAPI = null;
        private bool rotationHints = true;
        private bool hudVisible = true;
        private double aspectRatio = 1;
        private float hudBackgroundOpacity = 1f;
        private StringBuilder textAPIlines = new StringBuilder();
        private StringBuilder textSB = new StringBuilder();
        private HudAPIv2.HUDMessage textObject = null;
        private HudAPIv2.BillBoardHUDMessage bgObject = null;
        private HudAPIv2.SpaceMessage[] textAPILabels;
        private HudAPIv2.SpaceMessage[] textAPIShadows;
        private readonly Dictionary<MyDefinitionId, Cache> cachedInfoTextAPI = new Dictionary<MyDefinitionId, Cache>();

        private float TextAPIScale
        {
            get { return settings.textAPIScale * 1.2f; }
        }

        // used by the HUD notification view mode
        private int atLine = SCROLL_FROM_LINE;
        private long lastScroll = 0;
        private List<HudLine> notificationLines = new List<HudLine>();
        private readonly Dictionary<MyDefinitionId, Cache> cachedInfoNotification = new Dictionary<MyDefinitionId, Cache>();

        // used in generating the block info text or menu for either view mode
        private int line = -1;
        private bool addLineCalled = false;
        private float largestLineWidth = 0;

        // resource sink group cache
        public int resourceSinkGroups = 0;
        public int resourceSourceGroups = 0;
        public readonly Dictionary<MyStringHash, ResourceGroupData> resourceGroupPriority = new Dictionary<MyStringHash, ResourceGroupData>();

        // character size cache for notifications; textAPI has its own.
        private readonly Dictionary<char, int> charSize = new Dictionary<char, int>();

        // cached block data that is inaccessible via definitions (like thruster flames)
        public readonly Dictionary<MyDefinitionId, BlockDataBase> blockData = new Dictionary<MyDefinitionId, BlockDataBase>();

        // various temporary caches
        private readonly HashSet<Vector3I> cubes = new HashSet<Vector3I>();
        private readonly List<MyDefinitionId> removeCacheIds = new List<MyDefinitionId>();
        public readonly Dictionary<string, IMyModelDummy> dummies = new Dictionary<string, IMyModelDummy>();
        #endregion

        public override void LoadData()
        {
            instance = this;
            Log.SetUp(MOD_NAME, MOD_WORKSHOP_ID, MOD_SHORTNAME);
        }

        public bool Init()
        {
            Log.Init();
            init = true;
            isThisDS = (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated);

            if(isThisDS) // not doing anything DS side so get rid of this component entirely
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(DisposeComponent);
                return false;
            }

            int count = Enum.GetValues(typeof(TextAPIMsgIds)).Length;
            textAPILabels = new HudAPIv2.SpaceMessage[count];
            textAPIShadows = new HudAPIv2.SpaceMessage[count];

            ComputeCharacterSizes();
            ComputeResourceGroups();
            UpdateConfigValues();

            leakInfo = new LeakInfo();

            settings = new Settings();

            textAPI = new HudAPIv2();

            // TODO experiment - block detection without gamelogic comp
            //MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Utilities.MessageEntered += MessageEntered;
            MyAPIGateway.Gui.GuiControlRemoved += GuiControlRemoved;
            return true;
        }

        private void DisposeComponent()
        {
            Log.Close();
            instance = null;
            SetUpdateOrder(MyUpdateOrder.NoUpdate); // this throws exceptions if called in an update method, which is why the InvokeOnGameThread() is needed.
            MyAPIGateway.Session.UnregisterComponent(this);
        }

        protected override void UnloadData()
        {
            if(instance == null)
                return;

            instance = null;

            try
            {
                if(init)
                {
                    init = false;

                    // TODO experiment - block detection without gamelogic comp
                    //MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
                    MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
                    MyAPIGateway.Gui.GuiControlRemoved -= GuiControlRemoved;

                    if(settings != null)
                    {
                        settings.Close();
                        settings = null;
                    }

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

        // TODO experiment - block detection without gamelogic comp
#if false
        private readonly Dictionary<MyObjectBuilderType, Type> blockTypesMonitor = new Dictionary<MyObjectBuilderType, Type>(MyObjectBuilderType.Comparer)
        {
            [typeof(MyObjectBuilder_Thrust)] = typeof(BlockDataThrust),
            [typeof(MyObjectBuilder_ShipWelder)] = typeof(BlockDataShipWelder),
        };

        private void OnEntityAdd(IMyEntity ent)
        {
            try
            {
                var grid = ent as MyCubeGrid;

                if(grid == null)
                    return;

                foreach(var block in grid.GetFatBlocks())
                {
                    var defId = block.BlockDefinition.Id;
                    Type type;

                    if(blockTypesMonitor.TryGetValue(defId.TypeId, out type))
                    {
                        BlockDataBase.SetData(type, block);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
#endif

        public void MessageEntered(string msg, ref bool send)
        {
            try
            {
                const string CMD_NAME = "/buildinfo";

                if(msg.StartsWith(CMD_NAME, StringComparison.OrdinalIgnoreCase))
                {
                    send = false;
                    msg = msg.Substring(CMD_NAME.Length).Trim();

                    if(msg.StartsWith("reload", StringComparison.OrdinalIgnoreCase))
                    {
                        ReloadConfig();
                        return;
                    }

                    if(msg.StartsWith("clearcache", StringComparison.OrdinalIgnoreCase))
                    {
                        cachedInfoNotification.Clear();
                        cachedInfoTextAPI.Clear();
                        MyVisualScriptLogicProvider.SendChatMessage("Emptied block info cache.", Log.modName, 0, MyFontEnum.Green);
                        return;
                    }

                    if(msg.StartsWith("help", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowHelp();
                        return;
                    }

                    // no arguments
                    if(selectedDef != null)
                        showMenu = true;
                    else
                        ShowHelp();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void ReloadConfig()
        {
            if(settings.Load())
                MyVisualScriptLogicProvider.SendChatMessage("Reloaded and re-saved config.", Log.modName, 0, MyFontEnum.Green);
            else
                MyVisualScriptLogicProvider.SendChatMessage("Config created with the current settings.", Log.modName, 0, MyFontEnum.Green);

            settings.Save();

            HideText();
            cachedInfoTextAPI.Clear();

            if(textObject != null)
            {
                if(settings.alwaysVisible)
                {
                    textObject.Options &= ~HudAPIv2.Options.HideHud;
                    bgObject.Options &= ~HudAPIv2.Options.HideHud;
                }
                else
                {
                    textObject.Options |= HudAPIv2.Options.HideHud;
                    bgObject.Options |= HudAPIv2.Options.HideHud;
                }
            }
        }

        private void ShowHelp()
        {
            MyAPIGateway.Utilities.ShowMissionScreen("BuildInfo Mod", "", "Various help topics", HELP, null, "Close");
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
            hudBackgroundOpacity = cfg.HUDBkOpacity;

            var viewportSize = MyAPIGateway.Session.Camera.ViewportSize;
            aspectRatio = viewportSize.X / viewportSize.Y;

            bool newRotationHints = cfg.RotationHints;

            if(rotationHints != newRotationHints)
            {
                rotationHints = newRotationHints;
                HideText();
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
                        if(MyAPIGateway.Input.IsNewKeyPressed(MyKeys.Escape))
                        {
                            showMenu = false;
                            menuNeedsUpdate = true;
                            return;
                        }

                        bool canUseTextAPI = (textAPI != null && textAPI.Heartbeat);
                        int usableMenuItems = (canUseTextAPI ? 8 : 7);
                        var move = Vector3.Round(input.GetPositionDelta(), 1);

                        if(Math.Abs(previousMove.Z) < 0.01f)
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

                        if(Math.Abs(previousMove.X) < 0.01f && Math.Abs(move.X) > 0.2f)
                        {
                            menuNeedsUpdate = true;

                            switch(menuSelectedItem)
                            {
                                case 0:
                                    showMenu = false;
                                    menuNeedsUpdate = true;
                                    break;
                                case 1:
                                    showMenu = false;
                                    menuNeedsUpdate = true;
                                    ShowHelp();
                                    break;
                                case 2:
                                    settings.showTextInfo = !settings.showTextInfo;
                                    settings.Save();

                                    if(buildInfoNotification == null)
                                        buildInfoNotification = MyAPIGateway.Utilities.CreateNotification("");
                                    buildInfoNotification.Text = (settings.showTextInfo ? "Text info ON (saved to config)" : "Text info OFF (saved to config)");
                                    buildInfoNotification.Show();
                                    break;
                                case 3:
                                    CycleDrawVolume();
                                    break;
                                case 4:
                                    MyCubeBuilder.Static.UseTransparency = !MyCubeBuilder.Static.UseTransparency;
                                    break;
                                case 5:
                                    SetFreezeGizmo(!MyAPIGateway.CubeBuilder.FreezeGizmo);
                                    break;
                                case 6:
                                    ReloadConfig();
                                    break;
                                case 7:
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
                            CycleDrawVolume();
                            menuNeedsUpdate = true;

                            if(mountPointsNotification == null)
                                mountPointsNotification = MyAPIGateway.Utilities.CreateNotification("");

                            mountPointsNotification.Text = "Block Volumes: " + DRAW_VOLUME_TYPE[drawVolumeType];
                            mountPointsNotification.Show();
                        }
                        else if(input.IsAnyAltKeyPressed())
                        {
                            SetFreezeGizmo(!MyAPIGateway.CubeBuilder.FreezeGizmo);
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

        private void CycleDrawVolume()
        {
            if(++drawVolumeType >= DRAW_VOLUME_TYPE.Length)
                drawVolumeType = 0;
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
                // HACK using this method instead of MyAPIGateway.CubeBuilder.FreezeGizmo's setter because that one ignores the value and sets it to true.
                MyCubeBuilder.Static.FreezeGizmo = freeze;

                freezeGizmoNotification.Text = (freeze ? "Freeze placement position ON" : "Freeze placement position OFF");
                freezeGizmoNotification.Font = MyFontEnum.White;

                if(freeze)
                    MyCubeBuilder.Static.GetAddPosition(out lastGizmoPosition);
            }

            freezeGizmoNotification.Show();
        }

        public override void Draw()
        {
            try
            {
                if(!init)
                    return;

                DrawBlockVolumes();

                if(!hudVisible && !settings.alwaysVisible)
                    return;

                if(leakInfo != null)
                    leakInfo.Draw();

                // testing real time pressurization display
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
                Log.Error(e);
            }
        }

        private void DrawBlockVolumes()
        {
            if(drawVolumeType > 0 && (hudVisible || settings.alwaysVisible) && selectedDef != null)
            {
                var def = selectedDef;

                // needed to hide text messages that are no longer used while other stuff still draws
                for(int i = 0; i < textAPILabels.Length; ++i)
                {
                    var msgObj = textAPILabels[i];

                    if(msgObj != null)
                    {
                        msgObj.Visible = false;
                        textAPIShadows[i].Visible = false;
                    }
                }

                blockDataDraw = true;

                var camera = MyAPIGateway.Session.Camera;

                #region Draw mount points
                var box = MyCubeBuilder.Static.GetBuildBoundingBox();
                var drawMatrix = MatrixD.CreateFromQuaternion(box.Orientation);

                if(MyCubeBuilder.Static.DynamicMode)
                    drawMatrix.Translation = MyCubeBuilder.Static.FreePlacementTarget; // HACK: required for the position to be 100% accurate when the block is not aimed at anything
                else
                    drawMatrix.Translation = box.Center;

                var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);

                if(TextAPIEnabled)
                {
                    DrawMountPointAxixText(def, gridSize, ref drawMatrix);
                }
                else
                {
                    // HACK re-assigning mount points temporarily to prevent the original mountpoint wireframe from being drawn while keeping the axis information
                    var mp = def.MountPoints;
                    def.MountPoints = BLANK_MOUNTPOINTS;
                    MyCubeBuilder.DrawMountPoints(gridSize, def, ref drawMatrix);
                    def.MountPoints = mp;
                }

                // draw custom mount point styling
                {
                    var minSize = (def.CubeSize == MyCubeSize.Large ? 0.05 : 0.02); // a minimum size to have some thickness
                    var center = def.Center;
                    var mainMatrix = MatrixD.CreateTranslation((center - (def.Size * 0.5f)) * gridSize) * drawMatrix;
                    var mountPoints = def.GetBuildProgressModelMountPoints(1f);
                    bool drawLabel = settings.allLabels && TextAPIEnabled;

                    if(mountPoints != null)
                    {
                        if(drawVolumeType == 1)
                        {
                            if(def.IsAirTight)
                            {
                                var halfExtents = def.Size * (gridSize * 0.5);
                                var localBB = new BoundingBoxD(-halfExtents, halfExtents).Inflate(MOUNTPOINT_THICKNESS * 0.5);
                                MySimpleObjectDraw.DrawTransparentBox(ref drawMatrix, ref localBB, ref AIRTIGHT_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE);
                            }
                            else
                            {
                                var half = Vector3D.One * -(0.5f * gridSize);
                                var corner = (Vector3D)def.Size * -(0.5f * gridSize);
                                var transformMatrix = MatrixD.CreateTranslation(corner - half) * drawMatrix;

                                foreach(var kv in def.IsCubePressurized) // precomputed: [position][normal] = is airtight
                                {
                                    foreach(var kv2 in kv.Value)
                                    {
                                        if(!kv2.Value) // pos+normal not airtight
                                            continue;

                                        var pos = Vector3D.Transform((Vector3D)(kv.Key * gridSize), transformMatrix);
                                        var dirForward = Vector3.TransformNormal(kv2.Key, drawMatrix);
                                        var dirIndex = (int)Base6Directions.GetDirection(kv2.Key);
                                        var dirUp = Vector3.TransformNormal(DIRECTIONS[((dirIndex + 2) % 6)], drawMatrix);

                                        var m = MatrixD.Identity;
                                        m.Translation = pos + dirForward * (gridSize * 0.5f);
                                        m.Forward = dirForward;
                                        m.Backward = -dirForward;
                                        m.Left = Vector3D.Cross(dirForward, dirUp);
                                        m.Right = -m.Left;
                                        m.Up = dirUp;
                                        m.Down = -dirUp;
                                        var scale = new Vector3D(gridSize, gridSize, MOUNTPOINT_THICKNESS);
                                        MatrixD.Rescale(ref m, ref scale);

                                        MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE, onlyFrontFaces: true);

                                        //var colorWire = AIRTIGHT_COLOR * 4; 
                                        //MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref colorWire, MySimpleObjectRasterizer.Wireframe, 1, lineWidth: 0.01f, lineMaterial: MATERIAL_SQUARE, onlyFrontFaces: true);

                                        // makes an X
                                        //var v0 = (m.Translation + m.Down * 0.5 + m.Left * 0.5);
                                        //var v1 = (m.Translation + m.Up * 0.5 + m.Right * 0.5);
                                        //var v2 = (m.Translation + m.Down * 0.5 + m.Right * 0.5);
                                        //var v3 = (m.Translation + m.Up * 0.5 + m.Left * 0.5);
                                        //MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, colorWire, v0, (v1 - v0), 1f, 0.05f);
                                        //MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, colorWire, v2, (v3 - v2), 1f, 0.05f);
                                    }
                                }
                            }
                        }
                        else if(drawVolumeType == 2)
                        {
                            for(int i = 0; i < mountPoints.Length; i++)
                            {
                                var mountPoint = mountPoints[i];

                                if(!mountPoint.Enabled)
                                    continue; // ignore all disabled mount points as airtight ones are rendered separate

                                var colorFace = (mountPoint.Default ? MOUNTPOINT_DEFAULT_COLOR : MOUNTPOINT_COLOR);

                                DrawMountPoint(mountPoint, gridSize, ref center, ref mainMatrix, ref colorFace, minSize);

                                if(drawLabel)
                                {
                                    drawLabel = false; // only draw for the first mount point

                                    // TODO use? needs fixing for multi-cell blocks...

                                    //var cubeSize = def.Size * (gridSize * 0.5f);
                                    //var dirIndex = (int)Base6Directions.GetDirection(mountPoint.Normal);
                                    //var dirForward = Vector3D.TransformNormal(DIRECTIONS[dirIndex], drawMatrix);
                                    //var dirLeft = Vector3D.TransformNormal(DIRECTIONS[((dirIndex + 4) % 6)], drawMatrix);
                                    //var dirUp = Vector3D.TransformNormal(DIRECTIONS[((dirIndex + 2) % 6)], drawMatrix);

                                    //var pos = drawMatrix.Translation + dirForward * cubeSize.GetDim((i % 6) / 2);
                                    //float width = cubeSize.GetDim(((i + 4) % 6) / 2);
                                    //float height = cubeSize.GetDim(((i + 2) % 6) / 2);

                                    //var labelPos = pos + dirLeft * width + dirUp * height;
                                    //var labelDir = dirUp;

                                    //DrawLineLabelAlternate(TextAPIMsgIds.MOUNT, labelPos, labelPos + labelDir * 0.3, "Mount/pressure face", MOUNTPOINT_COLOR);
                                    //DrawLineLabelAlternate(TextAPIMsgIds.MOUNT_ROTATE, labelPos, labelPos + labelDir * 0.5, "Auto-rotate face", MOUNTPOINT_DEFAULT_COLOR);
                                    //DrawLineLabelAlternate(TextAPIMsgIds.MOUNT_DISABLED, labelPos, labelPos + labelDir * 0.7, "Pressure only face", MOUNTPOINT_DISABLED_COLOR);
                                }
                            }
                        }
                    }
                }
                #endregion

                #region Door airtightness toggle blinking
                if(def is MyDoorDefinition || def is MyAdvancedDoorDefinition || def is MyAirtightDoorGenericDefinition)
                {
                    if(!MyParticlesManager.Paused && ++doorAirtightBlinkTick >= 60)
                    {
                        doorAirtightBlinkTick = 0;
                        doorAirtightBlink = !doorAirtightBlink;
                    }

                    var cubeSize = def.Size * (gridSize * 0.5f);
                    bool drawLabel = settings.allLabels && TextAPIEnabled;

                    if(drawLabel || doorAirtightBlink)
                    {
                        for(int i = 0; i < 6; ++i)
                        {
                            var normal = DIRECTIONS[i];

                            if(Pressurization.IsDoorFacePressurized(def, (Vector3I)normal, true))
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
                                    MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_TOGGLE_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE, onlyFrontFaces: true);
                                }

                                if(drawLabel) // only label the first one
                                {
                                    drawLabel = false;

                                    var labelPos = pos + dirLeft * width + dirUp * height;
                                    DrawLineLabelAlternate(TextAPIMsgIds.DOOR_AIRTIGHT, labelPos, labelPos + dirLeft * 0.5, "Airtight when closed", AIRTIGHT_TOGGLE_COLOR, underlineLength: 1.7f);

                                    if(!doorAirtightBlink) // no need to iterate further if no faces need to be rendered
                                        break;
                                }
                            }
                        }
                    }
                }
                #endregion

                #region Draw drill mining/cutout radius
                var drill = def as MyShipDrillDefinition;
                if(drill != null)
                {
                    const float lineHeight = 0.3f;
                    const int wireDivRatio = 20;
                    var colorMine = Color.Lime;
                    var colorMineFace = colorMine * 0.3f;
                    var colorCut = Color.Red;
                    var colorCutFace = colorCut * 0.3f;
                    bool drawLabels = settings.allLabels && TextAPIEnabled;

                    var mineMatrix = drawMatrix;
                    mineMatrix.Translation += mineMatrix.Forward * drill.SensorOffset;
                    MySimpleObjectDraw.DrawTransparentSphere(ref mineMatrix, drill.SensorRadius, ref colorMineFace, MySimpleObjectRasterizer.Solid, wireDivRatio, faceMaterial: MATERIAL_SQUARE);

                    bool showCutOut = (Math.Abs(drill.SensorRadius - drill.CutOutRadius) > 0.0001f || Math.Abs(drill.SensorOffset - drill.CutOutOffset) > 0.0001f);

                    if(drawLabels)
                    {
                        var labelDir = mineMatrix.Down;
                        var sphereEdge = mineMatrix.Translation + (labelDir * drill.SensorRadius);
                        DrawLineLabel(TextAPIMsgIds.DRILL_MINE, sphereEdge, labelDir, (showCutOut ? "Mining radius" : "Mining/cutout radius"), colorMine, constantTextUpdate: true, lineHeight: lineHeight, underlineLength: (showCutOut ? 0.75f : 1f));
                    }

                    if(showCutOut)
                    {
                        var cutMatrix = drawMatrix;
                        cutMatrix.Translation += cutMatrix.Forward * drill.CutOutOffset;
                        MySimpleObjectDraw.DrawTransparentSphere(ref cutMatrix, drill.CutOutRadius, ref colorCutFace, MySimpleObjectRasterizer.Solid, wireDivRatio, faceMaterial: MATERIAL_SQUARE);

                        if(drawLabels)
                        {
                            var labelDir = cutMatrix.Left;
                            var sphereEdge = cutMatrix.Translation + (labelDir * drill.CutOutRadius);
                            DrawLineLabel(TextAPIMsgIds.DRILL_CUTOUT, sphereEdge, labelDir, "Cutout radius", colorCut, lineHeight: lineHeight);
                        }
                    }
                }
                #endregion

                #region Welder and grinder sensors
                bool isShipWelder = def is MyShipWelderDefinition;
                if(isShipWelder || def is MyShipGrinderDefinition)
                {
                    var data = BlockDataBase.TryGetDataCached<BlockDataShipToolBase>(def);

                    if(data != null)
                    {
                        const float lineHeight = 0.3f;
                        const int wireDivRatio = 20;
                        var color = Color.Lime;
                        var colorFace = color * 0.3f;
                        var radius = data.sphereDummy.Radius;

                        var sphereMatrix = drawMatrix;
                        sphereMatrix.Translation = Vector3D.Transform(data.sphereDummy.Center, drawMatrix);

                        MySimpleObjectDraw.DrawTransparentSphere(ref sphereMatrix, radius, ref colorFace, MySimpleObjectRasterizer.Solid, wireDivRatio, faceMaterial: MATERIAL_SQUARE);

                        if(settings.allLabels && TextAPIEnabled)
                        {
                            var labelDir = sphereMatrix.Down;
                            var sphereEdge = sphereMatrix.Translation + (labelDir * radius);
                            DrawLineLabel(TextAPIMsgIds.SHIP_TOOL, sphereEdge, labelDir, (isShipWelder ? "Welding radius" : "Grinding radius"), color, constantTextUpdate: true, lineHeight: lineHeight, underlineLength: 0.75f);
                        }
                    }
                }
                #endregion

                #region Weapon accuracy indicator
                var weapon = def as MyWeaponBlockDefinition;
                if(weapon != null)
                {
                    var data = BlockDataBase.TryGetDataCached<BlockDataWeapons>(def);

                    if(data != null)
                    {
                        const int wireDivideRatio = 12;
                        const float lineHeight = 0.5f;
                        var color = Color.Red;
                        var colorFace = color * 0.5f;

                        var wepDef = MyDefinitionManager.Static.GetWeaponDefinition(weapon.WeaponDefinitionId);
                        var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wepDef.AmmoMagazinesId[0]);
                        var ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);

                        var height = ammo.MaxTrajectory;
                        var tanShotAngle = (float)Math.Tan(wepDef.DeviateShotAngle);
                        var accuracyAtMaxRange = tanShotAngle * (height * 2);
                        var coneMatrix = data.muzzleLocalMatrix * drawMatrix;

                        MyTransparentGeometry.AddPointBillboard(MATERIAL_WHITEDOT, color, coneMatrix.Translation, 0.025f, 0);
                        MySimpleObjectDraw.DrawTransparentCone(ref coneMatrix, accuracyAtMaxRange, height, ref colorFace, wireDivideRatio, faceMaterial: MATERIAL_SQUARE);

                        //const int circleWireDivideRatio = 20;
                        //var accuracyAt100m = tanShotAngle * (100 * 2);
                        //var color100m = Color.Green.ToVector4();
                        //var circleMatrix = MatrixD.CreateWorld(coneMatrix.Translation + coneMatrix.Forward * 3 + coneMatrix.Left * 3, coneMatrix.Down, coneMatrix.Forward);
                        //MySimpleObjectDraw.DrawTransparentCylinder(ref circleMatrix, accuracyAt100m, accuracyAt100m, 0.1f, ref color100m, true, circleWireDivideRatio, 0.05f, MATERIAL_SQUARE);

                        if(settings.allLabels && TextAPIEnabled)
                        {
                            var labelDir = coneMatrix.Up;
                            var labelLineStart = coneMatrix.Translation + coneMatrix.Forward * 3;
                            DrawLineLabel(TextAPIMsgIds.ACCURACY_MAX, labelLineStart, labelDir, $"Accuracy cone - {height} m", color, constantTextUpdate: true, lineHeight: lineHeight, underlineLength: 1.75f);

                            //var lineStart = circleMatrix.Translation + coneMatrix.Down * accuracyAt100m;
                            //var labelStart = lineStart + coneMatrix.Down * 0.3f;
                            //DrawLineLabelAlternate(TextAPIMsgIds.ACCURACY_100M, lineStart, labelStart, "At 100m (zoomed)", color100m, underlineLength: 1.5f);
                        }
                    }
                }
                #endregion

                #region Thruster damage visualization
                var thrust = def as MyThrustDefinition;
                if(thrust != null)
                {
                    var data = BlockDataBase.TryGetDataCached<BlockDataThrust>(def);

                    if(data != null)
                    {
                        const int wireDivideRatio = 12;
                        const float lineHeight = 0.3f;
                        var color = Color.Red;
                        var colorFace = color * 0.5f;
                        var capsuleMatrix = MatrixD.CreateWorld(Vector3D.Zero, drawMatrix.Up, drawMatrix.Backward); // capsule is rotated weirdly (pointing up), needs adjusting
                        bool drawLabel = settings.allLabels && TextAPIEnabled;

                        foreach(var flame in data.damageFlames)
                        {
                            var start = Vector3D.Transform(flame.LocalFrom, drawMatrix);

                            capsuleMatrix.Translation = start + (drawMatrix.Forward * (flame.Height * 0.5));
                            MySimpleObjectDraw.DrawTransparentCapsule(ref capsuleMatrix, flame.Radius, flame.Height, ref colorFace, wireDivideRatio, MATERIAL_SQUARE);

                            if(drawLabel)
                            {
                                drawLabel = false; // label only on the first flame
                                var labelDir = drawMatrix.Down;
                                var labelLineStart = Vector3D.Transform(flame.LocalTo, drawMatrix) + labelDir * flame.Radius;
                                DrawLineLabel(TextAPIMsgIds.THRUST_DAMAGE, labelLineStart, labelDir, "Thrust damage", color, lineHeight: lineHeight, underlineLength: 1.1f);
                            }
                        }
                    }
                }
                #endregion

                #region Landing gear magnet points
                if(def is MyLandingGearDefinition)
                {
                    var data = BlockDataBase.TryGetDataCached<BlockLandingGear>(def);

                    if(data != null)
                    {
                        var color = new Color(20, 255, 155);
                        var colorFace = color * 0.5f;
                        bool drawLabel = settings.allLabels && TextAPIEnabled;

                        foreach(var obb in data.magents)
                        {
                            var localBB = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);
                            var m = MatrixD.CreateFromQuaternion(obb.Orientation);
                            m.Translation = obb.Center;
                            m *= drawMatrix;

                            MySimpleObjectDraw.DrawTransparentBox(ref m, ref localBB, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE);

                            if(drawLabel)
                            {
                                drawLabel = false; // only label the first one
                                var labelDir = drawMatrix.Down;
                                var labelLineStart = m.Translation + (m.Down * localBB.HalfExtents.Y) + (m.Backward * localBB.HalfExtents.Z) + (m.Left * localBB.HalfExtents.X);
                                DrawLineLabel(TextAPIMsgIds.MAGNET, labelLineStart, labelDir, "Magnet", color, lineHeight: 0.5f, underlineLength: 0.7f);
                            }
                        }
                    }
                }
                #endregion
            }
            else
            {
                // no block equipped

                if(blockDataDraw)
                {
                    blockDataDraw = false;

                    for(int i = 0; i < textAPILabels.Length; ++i)
                    {
                        var msgObj = textAPILabels[i];

                        if(msgObj != null)
                        {
                            msgObj.Visible = false;
                            textAPIShadows[i].Visible = false;
                        }
                    }
                }
            }
        }

        private void DrawLineLabelAlternate(TextAPIMsgIds id, Vector3D start, Vector3D end, string text, Color color, bool constantTextUpdate = false, float lineThick = 0.005f, float underlineLength = 0.75f)
        {
            var cm = MyAPIGateway.Session.Camera.WorldMatrix;
            var direction = (end - start);

            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, start, direction, 1f, lineThick);
            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, TEXT_SHADOW_COLOR, start + cm.Right * TEXT_SHADOW_OFFSET + cm.Down * TEXT_SHADOW_OFFSET + cm.Forward * TEXT_SHADOW_OFFSET_Z, direction, 1f, lineThick);

            if(!settings.allLabels || (!settings.axisLabels && (int)id < 3))
                return;

            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, end, cm.Right, underlineLength, lineThick);
            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, TEXT_SHADOW_COLOR, end + cm.Right * TEXT_SHADOW_OFFSET + cm.Down * TEXT_SHADOW_OFFSET + cm.Forward * TEXT_SHADOW_OFFSET_Z, cm.Right, underlineLength, lineThick);

            DrawSimpleLabel(id, end, text, color, constantTextUpdate);
        }

        private void DrawLineLabel(TextAPIMsgIds id, Vector3D start, Vector3D direction, string text, Color color, bool constantTextUpdate = false, float lineHeight = 0.3f, float lineThick = 0.005f, float underlineLength = 0.75f)
        {
            var cm = MyAPIGateway.Session.Camera.WorldMatrix;
            var end = start + direction * lineHeight;

            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, start, direction, lineHeight, lineThick);
            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, TEXT_SHADOW_COLOR, start + cm.Right * TEXT_SHADOW_OFFSET + cm.Down * TEXT_SHADOW_OFFSET + cm.Forward * TEXT_SHADOW_OFFSET_Z, direction, lineHeight, lineThick);

            if(!settings.allLabels || (!settings.axisLabels && (int)id < 3))
                return;

            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, end, cm.Right, underlineLength, lineThick);
            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, TEXT_SHADOW_COLOR, end + cm.Right * TEXT_SHADOW_OFFSET + cm.Down * TEXT_SHADOW_OFFSET + cm.Forward * TEXT_SHADOW_OFFSET_Z, cm.Right, underlineLength, lineThick);

            DrawSimpleLabel(id, end, text, color, constantTextUpdate);
        }

        private void DrawSimpleLabel(TextAPIMsgIds id, Vector3D worldPos, string text, Color textColor, bool updateText = false)
        {
            var i = (int)id;
            var camera = MyAPIGateway.Session.Camera;
            var msgObj = textAPILabels[i];
            HudAPIv2.SpaceMessage shadowObj = textAPIShadows[i];

            if(msgObj == null)
            {
                textAPILabels[i] = msgObj = new HudAPIv2.SpaceMessage(new StringBuilder(), worldPos, Vector3D.Up, Vector3D.Left, 0.1);
                msgObj.Offset = new Vector2D(0.1, 0.1);

                textAPIShadows[i] = shadowObj = new HudAPIv2.SpaceMessage(new StringBuilder(), worldPos, Vector3D.Up, Vector3D.Left, 0.1);
                shadowObj.Offset = msgObj.Offset + new Vector2D(TEXT_SHADOW_OFFSET, -TEXT_SHADOW_OFFSET);

                updateText = true;
            }

            if(updateText)
            {
                msgObj.Message.Clear().Append(textColor.ToTextAPIColor()).Append(text);
                shadowObj.Message.Clear().Append(TEXT_SHADOW_COLOR.ToTextAPIColor()).Append(text);
            }

            msgObj.Visible = true;
            msgObj.WorldPosition = worldPos;
            msgObj.Left = camera.WorldMatrix.Left;
            msgObj.Up = camera.WorldMatrix.Up;

            shadowObj.Visible = true;
            shadowObj.WorldPosition = worldPos;
            shadowObj.Left = camera.WorldMatrix.Left;
            shadowObj.Up = camera.WorldMatrix.Up;
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

            MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE, onlyFrontFaces: true);

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
            var camera = MyAPIGateway.Session.Camera;
            var dir = Vector3D.TransformNormal(direction * 0.5f, matrix);
            var textPos = drawMatrix.Translation + dir * 1.25;
            var text = AXIS_LABELS[(int)id];
            DrawLineLabel(id, drawMatrix.Translation, dir, text, color, lineHeight: 1.5f, underlineLength: text.Length * 0.1f);
        }

        private StringBuilder AddMenuItemLine(int item, bool enabled = true)
        {
            AddLine(font: (menuSelectedItem == item ? MyFontEnum.Green : (enabled ? MyFontEnum.White : MyFontEnum.Red)));

            if(menuSelectedItem == item)
                GetLine().SetTextAPIColor(COLOR_GOOD).Append("  > ");
            else
                GetLine().SetTextAPIColor(enabled ? COLOR_NORMAL : COLOR_UNIMPORTANT).Append(' ', 6);

            return GetLine();
        }

        private void GenerateMenuText()
        {
            ResetLines();

            bool canUseTextAPI = (textAPI != null && textAPI.Heartbeat);
            var inputName = MyControlsSpace.VOXEL_HAND_SETTINGS.GetControlAssignedName();

            AddLine(MyFontEnum.Blue).SetTextAPIColor(COLOR_BLOCKTITLE).Append("Build info settings:").ResetTextAPIColor().EndLine();

            #region Menu items
            int i = 0;

            AddMenuItemLine(i++).Append("Close menu");
            if(inputName != null)
                GetLine().Append("   (").Append(inputName).Append(")");
            GetLine().ResetTextAPIColor().EndLine();

            AddMenuItemLine(i++).Append("Open help window").SetTextAPIColor(COLOR_UNIMPORTANT).Append("   (/buildinfo help)").ResetTextAPIColor().EndLine();

            AddMenuItemLine(i++).Append("Text info: ").Append(settings.showTextInfo ? "ON" : "OFF").ResetTextAPIColor().EndLine();

            AddMenuItemLine(i++).Append("Draw block volumes: ").Append(DRAW_VOLUME_TYPE[drawVolumeType]);
            if(inputName != null)
                GetLine().Append("   (Ctrl+" + inputName + ")");
            GetLine().ResetTextAPIColor().EndLine();

            AddMenuItemLine(i++).Append("Transparent model: ").Append(MyCubeBuilder.Static.UseTransparency ? "ON" : "OFF");
            if(inputName != null)
                GetLine().Append("   (Shift+" + inputName + ")");
            GetLine().ResetTextAPIColor().EndLine();

            AddMenuItemLine(i++).Append("Freeze in position: ").Append(MyAPIGateway.CubeBuilder.FreezeGizmo ? "ON" : "OFF");
            if(inputName != null)
                GetLine().Append("   (Alt+" + inputName + ")");
            GetLine().ResetTextAPIColor().EndLine();

            AddMenuItemLine(i++).Append("Reload settings file").SetTextAPIColor(COLOR_UNIMPORTANT).Append("   (/buildinfo reload)").ResetTextAPIColor().EndLine();

            AddMenuItemLine(i++, canUseTextAPI).Append("Use TextAPI: ");
            if(canUseTextAPI)
                GetLine().Append(useTextAPI ? "ON" : "OFF");
            else
                GetLine().Append("OFF (Mod not detected)");
            GetLine().ResetTextAPIColor().EndLine();
            #endregion

            AddLine(MyFontEnum.Blue).SetTextAPIColor(COLOR_WARNING).Append("Use movement controls to navigate and edit settings.").ResetTextAPIColor().EndLine();

            if(inputName == null)
                AddLine(MyFontEnum.ErrorMessageBoxCaption).SetTextAPIColor(COLOR_BAD).Append("The 'Open voxel hand settings' control is not assigned!").ResetTextAPIColor().EndLine();

            EndAddedLines();
        }

        private void GenerateBlockText(MyCubeBlockDefinition def)
        {
            ResetLines();

            #region Block name line only for textAPI
            if(TextAPIEnabled)
            {
                AddLine().SetTextAPIColor(COLOR_BLOCKTITLE).Append(def.DisplayNameText);

                var stages = def.BlockStages;

                if(stages != null && stages.Length > 0)
                {
                    GetLine().Append("  ").SetTextAPIColor(COLOR_BLOCKVARIANTS).Append("(Variant 1 of ").Append(stages.Length + 1).Append(")");
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

                        GetLine().Append("  ").SetTextAPIColor(COLOR_BLOCKVARIANTS).Append("(Variant ").Append(num).Append(" of ").Append(stages.Length + 1).Append(")");
                    }
                }

                GetLine().ResetTextAPIColor().EndLine();
            }
            #endregion

            AppendBasics(def, part: false);

            #region Optional - different item gain on grinding
            foreach(var comp in def.Components)
            {
                if(comp.DeconstructItem != comp.Definition)
                {
                    AddLine(MyFontEnum.ErrorMessageBoxCaption).SetTextAPIColor(COLOR_WARNING).Append("When grinding: ").Append(comp.Definition.DisplayNameText).Append(" turns into ").Append(comp.DeconstructItem.DisplayNameText).ResetTextAPIColor().EndLine();
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
            if(MyAPIGateway.Session.CreativeMode || MyAPIGateway.Session.EnableCopyPaste) // HACK Session.EnableCopyPaste used as spacemaster check
            {
                if(def.MirroringBlock != null)
                {
                    MyCubeBlockDefinition mirrorDef;
                    if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(new MyDefinitionId(def.Id.TypeId, def.MirroringBlock), out mirrorDef))
                        AddLine(MyFontEnum.Blue).SetTextAPIColor(COLOR_GOOD).Append("Mirrors with: ").Append(mirrorDef.DisplayNameText).EndLine();
                    else
                        AddLine(MyFontEnum.Red).SetTextAPIColor(COLOR_BAD).Append("Mirrors with: ").Append(def.MirroringBlock).Append(" (Error: not found)").EndLine();
                }
            }
            #endregion

            #region Details on last lines
            if(def.Id.TypeId != typeof(MyObjectBuilder_CubeBlock)) // anything non-decorative
                GenerateAdvancedBlockText(def);

            if(!def.Context.IsBaseGame)
                AddLine(MyFontEnum.Blue).SetTextAPIColor(COLOR_MOD).Append("Mod: ").ModFormat(def.Context).ResetTextAPIColor().EndLine();

            EndAddedLines();
            #endregion
        }

        private void AppendBasics(MyCubeBlockDefinition def, bool part = false)
        {
            int airTightFaces = 0;
            int totalFaces = 0;
            var airTight = IsAirTight(def, ref airTightFaces, ref totalFaces);
            var deformable = (def.BlockTopology == MyBlockTopology.Cube && def.UsesDeformation);
            var assembleTime = (int)(def.MaxIntegrity / def.IntegrityPointsPerSec);
            var buildModels = (def.BuildProgressModels != null && def.BuildProgressModels.Length > 0);
            var weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
            var grindRatio = def.DisassembleRatio;

            if(def is MyDoorDefinition || def is MyAdvancedDoorDefinition) // HACK hardcoded; from MyDoor & MyAdvancedDoor's overridden DisassembleRatio
                grindRatio *= 3.3f;

            string padding = (part ? (TextAPIEnabled ? "        - " : "       - ") : "");

            if(part)
                AddLine(MyFontEnum.Blue).SetTextAPIColor(COLOR_PART).Append("Part: ").Append(def.DisplayNameText).ResetTextAPIColor().EndLine();

            #region Line 1
            AddLine().Append(padding).SetTextAPIColor(Color.Yellow).MassFormat(def.Mass).ResetTextAPIColor().Separator()
                .VectorFormat(def.Size).Separator()
                .TimeFormat(assembleTime / weldMul).SetTextAPIColor(COLOR_UNIMPORTANT).MultiplierFormat(weldMul).ResetTextAPIColor();

            if(Math.Abs(grindRatio - 1) >= 0.0001f)
                GetLine().Separator().SetTextAPIColor(grindRatio > 1 ? COLOR_BAD : (grindRatio < 1 ? COLOR_GOOD : COLOR_NORMAL)).Append("Deconstructs: ").PercentFormat(1f / grindRatio).ResetTextAPIColor();

            if(!buildModels)
                GetLine().Separator().SetTextAPIColor(COLOR_WARNING).Append("(No construction models)").ResetTextAPIColor();

            GetLine().EndLine();
            #endregion

            #region Line 2
            AddLine().Append(padding).Append("Integrity: ").AppendFormat("{0:#,###,###,###,###}", def.MaxIntegrity).Separator();

            GetLine().SetTextAPIColor(deformable ? COLOR_WARNING : COLOR_NORMAL).Append("Deformable: ");
            if(deformable)
                GetLine().Append("Yes (").PercentFormat(def.DeformationRatio).Append(")");
            else
                GetLine().Append("No");

            GetLine().ResetTextAPIColor();

            if(Math.Abs(def.GeneralDamageMultiplier - 1) >= 0.0001f)
            {
                GetLine().Separator()
                    .SetTextAPIColor(def.GeneralDamageMultiplier > 1 ? COLOR_BAD : (def.GeneralDamageMultiplier < 1 ? COLOR_GOOD : COLOR_NORMAL))
                    .Append("Damage intake: ").PercentFormat(def.GeneralDamageMultiplier)
                    .ResetTextAPIColor();
            }

            GetLine().EndLine();
            #endregion

            #region Line 3
            AddLine(font: (airTight ? MyFontEnum.Green : (airTightFaces == 0 ? MyFontEnum.Red : MyFontEnum.Blue))).Append(padding)
                .SetTextAPIColor(airTight ? COLOR_GOOD : (airTightFaces == 0 ? COLOR_BAD : COLOR_WARNING)).Append("Air-tight faces: ");

            if(airTight)
                GetLine().Append("all");
            else
                GetLine().Append(airTightFaces).Append(" of ").Append(totalFaces);

            if(!part)
                GetLine().SetTextAPIColor(COLOR_UNIMPORTANT).Append(" (/buildinfo help)");

            GetLine().ResetTextAPIColor().EndLine();
            #endregion
        }

        private void GenerateAdvancedBlockText(MyCubeBlockDefinition def)
        {
            // TODO convert these if conditions to 'as' checking when their interfaces are not internal anymore

            var defTypeId = def.Id.TypeId;

            if(defTypeId == typeof(MyObjectBuilder_TerminalBlock)) // control panel block
            {
                // HACK hardcoded
                AddLine(MyFontEnum.Green).SetTextAPIColor(COLOR_GOOD).Append("Power required*: No").EndLine();
                return;
            }

            // HACK conveyor blocks have no definition
            if(defTypeId == typeof(MyObjectBuilder_Conveyor) || defTypeId == typeof(MyObjectBuilder_ConveyorConnector)) // conveyor hubs and tubes
            {
                // HACK hardcoded; from MyGridConveyorSystem
                float requiredPower = MyEnergyConstants.REQUIRED_INPUT_CONVEYOR_LINE;
                AddLine().Append("Power required*: ").PowerFormat(requiredPower).Separator().ResourcePriority("Conveyors", hardcoded: true).EndLine();
                return;
            }

            var shipDrill = def as MyShipDrillDefinition;
            if(shipDrill != null)
            {
                // HACK hardcoded; from MyShipDrill
                float requiredPower = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_DRILL;
                AddLine().Append("Power required*: ").PowerFormat(requiredPower).Separator().ResourcePriority(shipDrill.ResourceSinkGroup).EndLine();

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

                AddLine().Append("Mining radius: ").DistanceFormat(shipDrill.SensorRadius).Separator().Append("Front offset: ").DistanceFormat(shipDrill.SensorOffset).EndLine();
                AddLine().Append("Cutout radius: ").DistanceFormat(shipDrill.CutOutRadius).Separator().Append("Front offset: ").DistanceFormat(shipDrill.CutOutOffset).EndLine();

                // TODO find a way to refresh this?
                //if(!drawBlockVolumes)
                {
                    var inputName = MyControlsSpace.VOXEL_HAND_SETTINGS.GetControlAssignedName();
                    AddLine(MyFontEnum.DarkBlue).SetTextAPIColor(COLOR_UNIMPORTANT).Append("(Ctrl+").Append(inputName).Append(" to visualize sensors)").ResetTextAPIColor().EndLine();
                }

                return;
            }

            // HACK ship connector has no definition
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

                var data = BlockDataBase.TryGetDataCached<BlockDataShipToolBase>(def);

                if(shipWelder != null)
                {
                    float weld = 2; // HACK hardcoded; from MyShipWelder.WELDER_AMOUNT_PER_SECOND
                    var mul = MyAPIGateway.Session.WelderSpeedMultiplier;
                    AddLine().Append("Weld speed*: ").PercentFormat(weld * mul).Append(" split accross targets").MultiplierFormat(mul).EndLine();

                    if(data != null)
                        AddLine().Append("Welding radius: ").DistanceFormat(data.sphereDummy.Radius).EndLine();
                }
                else
                {
                    float grind = MyShipGrinderConstants.GRINDER_AMOUNT_PER_SECOND;
                    var mul = MyAPIGateway.Session.GrinderSpeedMultiplier;
                    AddLine().Append("Grind speed: ").PercentFormat(grind * mul).Append(" split accross targets").MultiplierFormat(mul).EndLine();

                    if(data != null)
                        AddLine().Append("Grinding radius: ").DistanceFormat(data.sphereDummy.Radius).EndLine();
                }

                // TODO find a way to refresh this?
                //if(!drawBlockVolumes)
                {
                    var inputName = MyControlsSpace.VOXEL_HAND_SETTINGS.GetControlAssignedName();
                    AddLine(MyFontEnum.DarkBlue).SetTextAPIColor(COLOR_UNIMPORTANT).Append("(Ctrl+").Append(inputName).Append(" to visualize sensors)").ResetTextAPIColor().EndLine();
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
                    AddLine().Append("Extended length: ").DistanceFormat(piston.Maximum).Separator().Append("Max velocity: ").DistanceFormat(piston.MaxVelocity).EndLine();
                }

                if(motor != null)
                {
                    AddLine().Append("Power required: ").PowerFormat(motor.RequiredPowerInput).Separator().ResourcePriority(motor.ResourceSinkGroup).EndLine();

                    var suspension = def as MyMotorSuspensionDefinition;

                    if(suspension == null)
                    {
                        AddLine().Append("Max torque: ").TorqueFormat(motor.MaxForceMagnitude).EndLine();

                        if(motor.RotorDisplacementMin < motor.RotorDisplacementMax)
                            AddLine().Append("Displacement: ").DistanceFormat(motor.RotorDisplacementMin).Append(" to ").DistanceFormat(motor.RotorDisplacementMax).EndLine();
                    }
                    else
                    {
                        AddLine().Append("Max torque: ").TorqueFormat(suspension.PropulsionForce).Separator().Append("Axle Friction: ").TorqueFormat(suspension.AxleFriction).EndLine();
                        AddLine().Append("Steering - Max angle: ").AngleFormat(suspension.MaxSteer).Separator().Append("Speed base: ").RotationSpeed(suspension.SteeringSpeed * 60).EndLine();
                        AddLine().Append("Ride height: ").DistanceFormat(suspension.MinHeight).Append(" to ").DistanceFormat(suspension.MaxHeight).EndLine();
                    }
                }

                var topPart = (motor != null ? motor.TopPart : piston.TopPart);
                var group = MyDefinitionManager.Static.TryGetDefinitionGroup(topPart);

                if(group == null)
                    return;

                var partDef = (def.CubeSize == MyCubeSize.Large ? group.Large : group.Small);

                AppendBasics(partDef, part: true);

#if false
                var airTightFaces = 0;
                var totalFaces = 0;
                var airTight = IsAirTight(partDef, ref airTightFaces, ref totalFaces);
                var deformable = def.BlockTopology == MyBlockTopology.Cube;
                var buildModels = def.BuildProgressModels != null && def.BuildProgressModels.Length > 0;
                var weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
                var weldTime = ((def.MaxIntegrity / def.IntegrityPointsPerSec) / weldMul);
                var grindRatio = def.DisassembleRatio;

                AddLine(MyFontEnum.Blue).SetTextAPIColor(COLOR_PART).Append("Part: ").Append(partDef.DisplayNameText).EndLine();

                string padding = (TextAPIEnabled ? "        - " : "       - ");

                AddLine().Append(padding).MassFormat(partDef.Mass).Separator().VectorFormat(partDef.Size).Separator().TimeFormat(weldTime).SetTextAPIColor(COLOR_UNIMPORTANT).MultiplierFormat(weldMul).ResetTextAPIColor();

                if(grindRatio > 1)
                    GetLine().Separator().Append("Deconstruct: ").PercentFormat(1f / grindRatio);

                if(!buildModels)
                    GetLine().Append(" (No construction models)");

                GetLine().EndLine();

                AddLine().Append(padding).Append("Integrity: ").AppendFormat("{0:#,###,###,###,###}", partDef.MaxIntegrity);

                if(deformable)
                    GetLine().Separator().Append("Deformable (").NumFormat(partDef.DeformationRatio, 3).Append(")");

                GetLine().Separator().Append("Air-tight faces: ").Append(airTight ? "All" : (airTightFaces == 0 ? "None" : airTightFaces + " of " + totalFaces));
                GetLine().EndLine();
#endif
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

                var cryo = def as MyCryoChamberDefinition;
                if(cryo != null)
                {
                    AddLine().Append("Power required: ").PowerFormat(cryo.IdlePowerConsumption).Separator().ResourcePriority(cryo.ResourceSinkGroup).EndLine();
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
                        GetLine().Append("Yes, Oxygen capacity: ").VolumeFormat(cockpit.OxygenCapacity);
                    else
                        GetLine().Append("No");

                    GetLine().EndLine();

                    if(cockpit.HUD != null)
                    {
                        MyDefinitionBase defHUD;
                        if(MyDefinitionManager.Static.TryGetDefinition(new MyDefinitionId(typeof(MyObjectBuilder_HudDefinition), cockpit.HUD), out defHUD))
                        {
                            // HACK MyHudDefinition is not whitelisted; also GetObjectBuilder() is useless because it doesn't get filled in
                            //var hudDefObj = (MyObjectBuilder_HudDefinition)defBase.GetObjectBuilder();
                            AddLine(MyFontEnum.Green).SetTextAPIColor(COLOR_GOOD).Append("Custom HUD: ").Append(cockpit.HUD).ResetTextAPIColor().Separator().SetTextAPIColor(COLOR_MOD).Append("Mod: ").ModFormat(defHUD.Context).EndLine();
                        }
                        else
                        {
                            AddLine(MyFontEnum.Red).SetTextAPIColor(COLOR_BAD).Append("Custom HUD: ").Append(cockpit.HUD).Append("  (Error: not found)").EndLine();
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

                    AddLine(thrust.EffectivenessAtMaxInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White).SetTextAPIColor(thrust.EffectivenessAtMaxInfluence < 1f ? COLOR_BAD : COLOR_GOOD)
                        .PercentFormat(thrust.EffectivenessAtMaxInfluence).Append(" max thrust ").ResetTextAPIColor();
                    if(thrust.MaxPlanetaryInfluence < 1f)
                        GetLine().Append("in ").PercentFormat(thrust.MaxPlanetaryInfluence).Append(" atmosphere");
                    else
                        GetLine().Append("in atmosphere");
                    GetLine().EndLine();

                    AddLine(thrust.EffectivenessAtMinInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White).SetTextAPIColor(thrust.EffectivenessAtMinInfluence < 1f ? COLOR_BAD : COLOR_GOOD)
                        .PercentFormat(thrust.EffectivenessAtMinInfluence).Append(" max thrust ").ResetTextAPIColor();
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

                var data = BlockDataBase.TryGetDataCached<BlockDataThrust>(def);

                if(data != null)
                {
                    var flameDistance = data.distance * Math.Max(1, thrust.SlowdownFactor); // if dampeners are stronger than normal thrust then the flame will be longer... not sure if this scaling is correct though

                    // HACK hardcoded; from MyThrust.DamageGrid() and MyThrust.ThrustDamage()
                    var damage = thrust.FlameDamage * data.flamesCount;
                    var flameShipDamage = damage * 30f;
                    var flameDamage = damage * 10f * data.radius;

                    AddLine();

                    if(data.flamesCount > 1)
                        GetLine().Append("Flames: ").Append(data.flamesCount).Separator().Append("Max distance: ");
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
                AddLine(MyFontEnum.Green).SetTextAPIColor(COLOR_GOOD).Append("Power required*: No").ResetTextAPIColor().EndLine();
                AddLine().Append("Max differential velocity for locking: ").SpeedFormat(lg.MaxLockSeparatingVelocity).EndLine();

                // TODO find a way to refresh this?
                //if(!drawBlockVolumes)
                {
                    var inputName = MyControlsSpace.VOXEL_HAND_SETTINGS.GetControlAssignedName();
                    AddLine(MyFontEnum.DarkBlue).SetTextAPIColor(COLOR_UNIMPORTANT).Append("(Ctrl+").Append(inputName).Append(" to visualize magnets)").ResetTextAPIColor().EndLine();
                }
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

                // TODO find a way to refresh this?
                //if(!drawBlockVolumes)
                {
                    var inputName = MyControlsSpace.VOXEL_HAND_SETTINGS.GetControlAssignedName();
                    AddLine(MyFontEnum.DarkBlue).SetTextAPIColor(COLOR_UNIMPORTANT).Append("(Ctrl+").Append(inputName).Append(" to visualize closed airtightness)").ResetTextAPIColor().EndLine();
                }
                return;
            }

            var airTightDoor = def as MyAirtightDoorGenericDefinition; // does not extend MyDoorDefinition
            if(airTightDoor != null)
            {
                float moveTime = (1f / ((MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS / 1000f) * airTightDoor.OpeningSpeed)) / MyEngineConstants.UPDATE_STEPS_PER_SECOND; // computed after MyDoor.UpdateCurrentOpening()

                AddLine().Append("Power: ").PowerFormat(airTightDoor.PowerConsumptionMoving).Separator().Append("Idle: ").PowerFormat(airTightDoor.PowerConsumptionIdle).Separator().ResourcePriority(airTightDoor.ResourceSinkGroup).EndLine();
                AddLine().Append("Move time: ").TimeFormat(moveTime).EndLine();

                // TODO find a way to refresh this?
                //if(!drawBlockVolumes)
                {
                    var inputName = MyControlsSpace.VOXEL_HAND_SETTINGS.GetControlAssignedName();
                    AddLine(MyFontEnum.DarkBlue).SetTextAPIColor(COLOR_UNIMPORTANT).Append("(Ctrl+").Append(inputName).Append(" to visualize closed airtightness)").ResetTextAPIColor().EndLine();
                }
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

                // TODO find a way to refresh this?
                //if(!drawBlockVolumes)
                {
                    var inputName = MyControlsSpace.VOXEL_HAND_SETTINGS.GetControlAssignedName();
                    AddLine(MyFontEnum.DarkBlue).SetTextAPIColor(COLOR_UNIMPORTANT).Append("(Ctrl+").Append(inputName).Append(" to visualize closed airtightness)").ResetTextAPIColor().EndLine();
                }
                return;
            }

            var parachute = def as MyParachuteDefinition;
            if(parachute != null)
            {
                float gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);

                // HACK formulas from MyParachute.UpdateParachute()
                float atmosphere = 1.0f;
                float atmosMod = 10.0f * (atmosphere - parachute.ReefAtmosphereLevel);

                if(atmosMod <= 0.5f || double.IsNaN(atmosMod))
                {
                    atmosMod = 0.5f;
                }
                else
                {
                    atmosMod = (float)Math.Log(atmosMod - 0.99f) + 5.0f;

                    if(atmosMod < 0.5f || double.IsNaN(atmosMod))
                        atmosMod = 0.5f;
                }

                // basically the atmosphere level at which atmosMod is above 0.5; finds real atmosphere level at which chute starts to fully open
                // thanks to Equinox for helping with the math here and at maxMass :}
                float disreefAtmosphere = ((float)Math.Exp(-4.5) + 1f) / 10 + parachute.ReefAtmosphereLevel;

                float chuteSize = (atmosMod * parachute.RadiusMultiplier * gridSize) / 2.0f;
                float chuteArea = MathHelper.Pi * chuteSize * chuteSize;
                float realAirDensity = (atmosphere * 1.225f);
                //Vector3 velocity = Vector3.One * 10;
                //float force = 2.5f * airDensity * velocity.LengthSquared() * area * parachute.DragCoefficient;
                //AddLine().Append("Force in normal atmosphere: ").ForceFormatWithLift(force).EndLine();

                //float mass = 100000f;
                //float g = 9.81f;
                //float D2 = (chuteX2 * chuteX2);
                //float descentVelocity = (float)Math.Sqrt((8 * mass * g) / (MathHelper.Pi * airDensity * parachute.DragCoefficient * D2));
                //AddLine().Append("Descent Velocity: ").SpeedFormat(descentVelocity).EndLine();

                const float TARGET_DESCEND_VEL = 10;
                float maxMass = 2.5f * realAirDensity * (TARGET_DESCEND_VEL * TARGET_DESCEND_VEL) * chuteArea * parachute.DragCoefficient / 9.81f;

                //var aimedGrid = MyCubeBuilder.Static.FindClosestGrid();
                //float mass = (aimedGrid != null ? aimedGrid.Physics.Mass : 0f);
                //float descentVel = (float)Math.Sqrt((mass * 9.81f) / (2.5f * realAirDensity * parachute.DragCoefficient * chuteArea));

                AddLine().Append("Power - Deploy: ").PowerFormat(parachute.PowerConsumptionMoving).Separator().Append("Idle: ").PowerFormat(parachute.PowerConsumptionIdle).Separator().ResourcePriority(parachute.ResourceSinkGroup).EndLine();
                AddLine().Append("Required item to deploy: ").Append(parachute.MaterialDeployCost).Append("x ").IdTypeSubtypeFormat(parachute.MaterialDefinitionId).EndLine();
                AddLine().Append("Required atmosphere - Minimum: ").NumFormat(parachute.MinimumAtmosphereLevel, 2).Separator().Append("Fully open: ").NumFormat(disreefAtmosphere, 2).EndLine();
                AddLine().Append("Drag coefficient: ").Append(parachute.DragCoefficient).EndLine();
                AddLine().Append("Load estimate: ").MassFormat(maxMass).Append(" falling at ").SpeedFormat(TARGET_DESCEND_VEL).Append(" in 9.81m/s² and 1.0 air density.").EndLine();
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
                    AddLine().Append("Stores: ").Append(gasTank.StoredGasId.SubtypeName).Separator().Append("Capacity: ").VolumeFormat(gasTank.Capacity).EndLine();
                }

                var oxygenGenerator = def as MyOxygenGeneratorDefinition;
                if(oxygenGenerator != null)
                {
                    AddLine().Append("Ice consumption: ").MassFormat(oxygenGenerator.IceConsumptionPerSecond).Append("/s").EndLine();

                    if(oxygenGenerator.ProducedGases.Count > 0)
                    {
                        AddLine().Append("Produces: ");

                        foreach(var gas in oxygenGenerator.ProducedGases)
                        {
                            GetLine().Append(gas.Id.SubtypeName).Append(" (").VolumeFormat(oxygenGenerator.IceConsumptionPerSecond * gas.IceToGasRatio).Append("/s), ");
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
                            var name = bp.DisplayNameText;
                            var newLineIndex = name.IndexOf('\n');

                            if(newLineIndex != -1) // name contains a new line, ignore everything after that
                            {
                                for(int i = 0; i < newLineIndex; ++i)
                                {
                                    GetLine().Append(name[i]);
                                }

                                GetLine().TrimEndWhitespace();
                            }
                            else
                            {
                                GetLine().Append(name);
                            }

                            GetLine().Append(", ");
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
                        AddLine(MyFontEnum.Blue).SetTextAPIColor(COLOR_WARNING).Append("Inventory items ").Append(invLimit.IsWhitelist ? "allowed" : "NOT allowed").Append(":").ResetTextAPIColor().EndLine();

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
                    AddLine().Append("Discharge time: ").TimeFormat((battery.MaxStoredPower / battery.MaxPowerOutput) * 3600f).Separator().Append("Recharge time: ").TimeFormat((battery.MaxStoredPower / battery.RequiredPowerInput) * 3600f);
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
                AddLine().Append("Produces: ").NumFormat(oxygenFarm.MaxGasOutput, 3).Append(" ").Append(oxygenFarm.ProducedGas.SubtypeName).Append(" l/s").Separator().ResourcePriority(oxygenFarm.ResourceSourceGroup).EndLine();
                AddLine(oxygenFarm.IsTwoSided ? MyFontEnum.White : MyFontEnum.Red).Append(oxygenFarm.IsTwoSided ? "Two-sided" : "One-sided").EndLine();
                return;
            }

            var vent = def as MyAirVentDefinition;
            if(vent != null)
            {
                AddLine().Append("Idle: ").PowerFormat(vent.StandbyPowerConsumption).Separator().Append("Operational: ").PowerFormat(vent.OperationalPowerConsumption).Separator().ResourcePriority(vent.ResourceSinkGroup).EndLine();
                AddLine().Append("Output - Rate: ").VolumeFormat(vent.VentilationCapacityPerSecond).Append("/s").Separator().ResourcePriority(vent.ResourceSourceGroup).EndLine();
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
                float requiredPowerInput = (radioAntenna.MaxBroadcastRadius / 500f) * 0.002f;

                AddLine().Append("Max required power*: ").PowerFormat(requiredPowerInput).Separator().ResourcePriority(radioAntenna.ResourceSinkGroup).EndLine();
                AddLine().Append("Max radius: ").DistanceFormat(radioAntenna.MaxBroadcastRadius).EndLine();
                return;
            }

            var laserAntenna = def as MyLaserAntennaDefinition;
            if(laserAntenna != null)
            {
                float powerUsage;
                {
                    // HACK copied from MyLaserAntenna.UpdatePowerInput()
                    const double LINEAR_UP_TO = 200000;
                    Vector3D headPos = Vector3D.Zero;
                    Vector3D targetCoords = new Vector3D(0, 0, 1000);

                    double powerInputLasing = (double)laserAntenna.PowerInputLasing;
                    double range = (laserAntenna.MaxRange < 0 ? double.MaxValue : laserAntenna.MaxRange);
                    double rangeSq = Math.Min(Vector3D.DistanceSquared(targetCoords, headPos), (range * range));

                    if(rangeSq > (LINEAR_UP_TO * LINEAR_UP_TO)) // 200km
                    {
                        double n2 = powerInputLasing / 2.0 / LINEAR_UP_TO;
                        double n3 = powerInputLasing * LINEAR_UP_TO - n2 * LINEAR_UP_TO * LINEAR_UP_TO;
                        powerUsage = (float)(rangeSq * n2 + n3) / 1000000f;
                    }
                    else
                    {
                        powerUsage = (float)(powerInputLasing * Math.Sqrt(rangeSq)) / 1000000f;
                    }
                }

                AddLine().Append("Power - Active: ").PowerFormat(powerUsage).Append("/km[1]").Separator().Append("Turning: ").PowerFormat(laserAntenna.PowerInputTurning).Separator().Append("Idle: ").PowerFormat(laserAntenna.PowerInputIdle).Separator().ResourcePriority(laserAntenna.ResourceSinkGroup).EndLine();

                AddLine(laserAntenna.RequireLineOfSight ? MyFontEnum.White : MyFontEnum.Green)
                    .SetTextAPIColor(laserAntenna.MaxRange < 0 ? COLOR_GOOD : COLOR_NORMAL).Append("Range: ");

                if(laserAntenna.MaxRange < 0)
                    GetLine().Append("Infinite");
                else
                    GetLine().DistanceFormat(laserAntenna.MaxRange);

                GetLine().ResetTextAPIColor().Separator().SetTextAPIColor(laserAntenna.RequireLineOfSight ? COLOR_WARNING : COLOR_GOOD).Append("Line-of-sight: ").Append(laserAntenna.RequireLineOfSight ? "Required" : "Not required").ResetTextAPIColor().EndLine();

                AddLine().Append("Rotation Pitch: ").AngleFormatDeg(laserAntenna.MinElevationDegrees).Append(" to ").AngleFormatDeg(laserAntenna.MaxElevationDegrees).Separator().Append("Yaw: ").AngleFormatDeg(laserAntenna.MinAzimuthDegrees).Append(" to ").AngleFormatDeg(laserAntenna.MaxAzimuthDegrees).EndLine();
                AddLine().Append("Rotation Speed: ").RotationSpeed(laserAntenna.RotationRate * 60).EndLine();

                // TODO visualize angle limits?
                return;
            }

            var beacon = def as MyBeaconDefinition;
            if(beacon != null)
            {
                // HACK hardcoded; from MyBeacon
                float requiredPowerInput = (beacon.MaxBroadcastRadius / 100000f) * 0.02f;

                AddLine().Append("Max required power*: ").PowerFormat(requiredPowerInput).Separator().ResourcePriority(beacon.ResourceSinkGroup).EndLine();
                AddLine().Append("Max radius: ").DistanceFormat(beacon.MaxBroadcastRadius).EndLine();
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

                // TODO visualize max area?
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
                AddLine(MyFontEnum.Green).SetTextAPIColor(COLOR_GOOD).Append("Power required*: No").ResetTextAPIColor().EndLine();
                AddLine().Append("Max artificial mass: ").MassFormat(spaceBall.MaxVirtualMass).EndLine();
                return;
            }

            var warhead = def as MyWarheadDefinition;
            if(warhead != null)
            {
                // HACK hardcoded
                AddLine(MyFontEnum.Green).SetTextAPIColor(COLOR_GOOD).Append("Power required*: No").ResetTextAPIColor().EndLine();
                AddLine().Append("Radius: ").DistanceFormat(warhead.ExplosionRadius).EndLine();
                AddLine().Append("Damage: ").AppendFormat("{0:#,###,###,###,##0.##}", warhead.WarheadExplosionDamage).EndLine();

                // TODO visualize damage radius?
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
                AddLine().Append("Font size limits - Min: ").Append(lcd.MinFontSize).Separator().Append("Max: ").Append(lcd.MaxFontSize).EndLine();
                return;
            }

            var camera = def as MyCameraBlockDefinition;
            if(camera != null)
            {
                AddLine().Append("Power - Normal use: ").PowerFormat(camera.RequiredPowerInput).Separator().Append("Raycast charging: ").PowerFormat(camera.RequiredChargingInput).Separator().ResourcePriority(camera.ResourceSinkGroup).EndLine();
                AddLine().Append("Field of view: ").AngleFormat(camera.MinFov).Append(" to ").AngleFormat(camera.MaxFov).EndLine();
                AddLine().Append("Raycast - Cone limit: ").AngleFormatDeg(camera.RaycastConeLimit).Separator().Append("Distance limit: ");

                if(camera.RaycastDistanceLimit < 0)
                    GetLine().Append("Infinite");
                else
                    GetLine().DistanceFormat((float)camera.RaycastDistanceLimit);

                GetLine().Separator().Append("Time multiplier: ").NumFormat(camera.RaycastTimeMultiplier, 3).EndLine();

                //var index = Math.Max(camera.OverlayTexture.LastIndexOf('/'), camera.OverlayTexture.LastIndexOf('\\')); // last / or \ char
                //AddLine().Append("Overlay texture: " + camera.OverlayTexture.Substring(index + 1));

                // TODO visualize angle limits?
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

                // TODO visualize field?
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
                AddLine(MyFontEnum.Green).SetTextAPIColor(COLOR_GOOD).Append("Power required*: No").ResetTextAPIColor().EndLine();
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
                    AddLine().SetTextAPIColor(largeTurret.AiEnabled ? COLOR_GOOD : COLOR_BAD).Append("Auto-target: ").BoolFormat(largeTurret.AiEnabled).ResetTextAPIColor().Append(largeTurret.IdleRotation ? " (With idle rotation)" : "(No idle rotation)").Separator().SetTextAPIColor(COLOR_WARNING).Append("Max range: ").DistanceFormat(largeTurret.MaxRangeMeters).ResetTextAPIColor().EndLine();
                    AddLine().Append("Rotation - ");

                    if(largeTurret.MinElevationDegrees <= -180 && largeTurret.MaxElevationDegrees >= 180)
                        GetLine().SetTextAPIColor(COLOR_GOOD).Append("Pitch: ").AngleFormatDeg(360);
                    else
                        GetLine().SetTextAPIColor(COLOR_WARNING).Append("Pitch: ").AngleFormatDeg(largeTurret.MinElevationDegrees).Append(" to ").AngleFormatDeg(largeTurret.MaxElevationDegrees);

                    GetLine().ResetTextAPIColor().Append(" @ ").RotationSpeed(largeTurret.ElevationSpeed * 60).Separator();

                    if(largeTurret.MinAzimuthDegrees <= -180 && largeTurret.MaxAzimuthDegrees >= 180)
                        GetLine().SetTextAPIColor(COLOR_GOOD).Append("Yaw: ").AngleFormatDeg(360);
                    else
                        GetLine().SetTextAPIColor(COLOR_WARNING).Append("Yaw: ").AngleFormatDeg(largeTurret.MinAzimuthDegrees).Append(" to ").AngleFormatDeg(largeTurret.MaxAzimuthDegrees);

                    GetLine().ResetTextAPIColor().Append(" @ ").RotationSpeed(largeTurret.RotationSpeed * 60).EndLine();

                    // TODO visualize angle limits?
                }

                AddLine().Append("Accuracy: ").DistanceFormat((float)Math.Tan(wepDef.DeviateShotAngle) * 200).Append(" group at 100m").Separator().Append("Reload: ").TimeFormat(wepDef.ReloadTime / 1000);

                // TODO find a way to refresh this?
                //if(!drawBlockVolumes)
                //{
                //    var inputName = MyControlsSpace.VOXEL_HAND_SETTINGS.GetControlAssignedName();
                //    GetLine().SetTextAPIColor(COLOR_UNIMPORTANT).Append(" (Ctrl+").Append(inputName).Append(" to see accuracy)").ResetTextAPIColor();
                //}

                GetLine().EndLine();

                var ammoProjectiles = new List<MyTuple<MyAmmoMagazineDefinition, MyProjectileAmmoDefinition>>();
                var ammoMissiles = new List<MyTuple<MyAmmoMagazineDefinition, MyMissileAmmoDefinition>>();

                for(int i = 0; i < wepDef.AmmoMagazinesId.Length; i++)
                {
                    var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wepDef.AmmoMagazinesId[i]);
                    var ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                    int ammoType = (int)ammo.AmmoType;

                    if(wepDef.WeaponAmmoDatas[ammoType] != null)
                    {
                        switch(ammoType)
                        {
                            case 0: ammoProjectiles.Add(MyTuple.Create(mag, (MyProjectileAmmoDefinition)ammo)); break;
                            case 1: ammoMissiles.Add(MyTuple.Create(mag, (MyMissileAmmoDefinition)ammo)); break;
                        }
                    }
                }

                var projectilesData = wepDef.WeaponAmmoDatas[0];
                var missileData = wepDef.WeaponAmmoDatas[1];

                if(ammoProjectiles.Count > 0)
                {
                    // HACK hardcoded; from Sandbox.Game.Weapons.MyProjectile.Start()
                    const float MIN_RANGE = 0.8f;
                    const float MAX_RANGE = 1.2f;

                    AddLine().Append("Projectiles - Fire rate: ").Append(Math.Round(projectilesData.RateOfFire / 60f, 3)).Append(" rounds/s")
                        .Separator().SetTextAPIColor(projectilesData.ShotsInBurst == 0 ? COLOR_GOOD : COLOR_WARNING).Append("Magazine: ");
                    if(projectilesData.ShotsInBurst == 0)
                        GetLine().Append("No reloading");
                    else
                        GetLine().Append(projectilesData.ShotsInBurst);
                    GetLine().ResetTextAPIColor().EndLine();

                    AddLine().Append("Projectiles - ").SetTextAPIColor(COLOR_PART).Append("Type").ResetTextAPIColor().Append(" (")
                        .SetTextAPIColor(COLOR_STAT_SHIPDMG).Append("ship").ResetTextAPIColor().Append(", ")
                        .SetTextAPIColor(COLOR_STAT_CHARACTERDMG).Append("character").ResetTextAPIColor().Append(", ")
                        .SetTextAPIColor(COLOR_STAT_HEADSHOTDMG).Append("headshot").ResetTextAPIColor().Append(", ")
                        .SetTextAPIColor(COLOR_STAT_SPEED).Append("speed").ResetTextAPIColor().Append(", ")
                        .SetTextAPIColor(COLOR_STAT_TRAVEL).Append("travel").ResetTextAPIColor().Append(")").EndLine();

                    for(int i = 0; i < ammoProjectiles.Count; ++i)
                    {
                        var data = ammoProjectiles[i];
                        var mag = data.Item1;
                        var ammo = data.Item2;

                        AddLine().Append("      - ").SetTextAPIColor(COLOR_PART).Append(mag.Id.SubtypeName).ResetTextAPIColor().Append(" (");

                        if(ammo.ProjectileCount > 1)
                            GetLine().SetTextAPIColor(COLOR_STAT_PROJECTILECOUNT).Append(ammo.ProjectileCount).Append("x ");

                        GetLine().SetTextAPIColor(COLOR_STAT_SHIPDMG).Append(ammo.ProjectileMassDamage).ResetTextAPIColor().Append(", ")
                            .SetTextAPIColor(COLOR_STAT_CHARACTERDMG).Append(ammo.ProjectileHealthDamage).ResetTextAPIColor().Append(", ")
                            .SetTextAPIColor(COLOR_STAT_HEADSHOTDMG).Append(ammo.HeadShot ? ammo.ProjectileHeadShotDamage : ammo.ProjectileHealthDamage).ResetTextAPIColor().Append(", ");

                        if(ammo.SpeedVar > 0)
                            GetLine().SetTextAPIColor(COLOR_STAT_SPEED).NumFormat(ammo.DesiredSpeed * (1f - ammo.SpeedVar), 2).Append("~").NumFormat(ammo.DesiredSpeed * (1f + ammo.SpeedVar), 2).Append(" m/s");
                        else
                            GetLine().SetTextAPIColor(COLOR_STAT_SPEED).SpeedFormat(ammo.DesiredSpeed);

                        GetLine().ResetTextAPIColor().Append(", ")
                            .SetTextAPIColor(COLOR_STAT_TRAVEL).DistanceRangeFormat(ammo.MaxTrajectory * MIN_RANGE, ammo.MaxTrajectory * MAX_RANGE).ResetTextAPIColor().Append(")").EndLine();
                    }
                }

                if(ammoMissiles.Count > 0)
                {
                    // HACK hardcoded; from Sandbox.Game.Weapons.MyMissile.UpdateBeforeSimulation()
                    const float MAX_TRAJECTORY_NO_ACCEL = 0.7f;

                    AddLine().Append("Missiles - Fire rate: ").Append(Math.Round(missileData.RateOfFire / 60f, 3)).Append(" rounds/s")
                        .Separator().SetTextAPIColor(missileData.ShotsInBurst == 0 ? COLOR_GOOD : COLOR_WARNING).Append("Magazine: ");
                    if(missileData.ShotsInBurst == 0)
                        GetLine().Append("No reloading");
                    else
                        GetLine().Append(missileData.ShotsInBurst);
                    GetLine().ResetTextAPIColor().EndLine();

                    AddLine().Append("Missiles - ").SetTextAPIColor(COLOR_PART).Append("Type").ResetTextAPIColor().Append(" (")
                        .SetTextAPIColor(COLOR_STAT_SHIPDMG).Append("damage").ResetTextAPIColor().Append(", ")
                        .SetTextAPIColor(COLOR_STAT_CHARACTERDMG).Append("radius").ResetTextAPIColor().Append(", ")
                        .SetTextAPIColor(COLOR_STAT_SPEED).Append("speed").ResetTextAPIColor().Append(", ")
                        .SetTextAPIColor(COLOR_STAT_TRAVEL).Append("travel").ResetTextAPIColor().Append(")").EndLine();

                    for(int i = 0; i < ammoMissiles.Count; ++i)
                    {
                        var data = ammoMissiles[i];
                        var mag = data.Item1;
                        var ammo = data.Item2;

                        AddLine().Append("      - ").SetTextAPIColor(COLOR_PART).Append(mag.Id.SubtypeName).ResetTextAPIColor().Append(" (")
                            .SetTextAPIColor(COLOR_STAT_SHIPDMG).Append(ammo.MissileExplosionDamage).ResetTextAPIColor().Append(", ")
                            .SetTextAPIColor(COLOR_STAT_CHARACTERDMG).DistanceFormat(ammo.MissileExplosionRadius).ResetTextAPIColor().Append(", ");

                        // SpeedVar is not used for missiles

                        GetLine().SetTextAPIColor(COLOR_STAT_SPEED);

                        if(!ammo.MissileSkipAcceleration)
                            GetLine().SpeedFormat(ammo.MissileInitialSpeed).Append(" + ").SpeedFormat(ammo.MissileAcceleration).Append("²");
                        else
                            GetLine().SpeedFormat(ammo.DesiredSpeed * MAX_TRAJECTORY_NO_ACCEL);

                        GetLine().ResetTextAPIColor().Append(", ").SetTextAPIColor(COLOR_STAT_TRAVEL).DistanceFormat(ammo.MaxTrajectory)
                            .ResetTextAPIColor().Append(")").EndLine();
                    }
                }

                return;
            }
        }

        private void PostProcessText(MyDefinitionId id)
        {
            if(TextAPIEnabled)
            {
                var text = textAPIlines.ToString();
                var textSize = UpdateTextAPIvisuals(text);
                cache = new CacheTextAPI(text, textObject.Origin, textSize);

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

        private Vector2D UpdateTextAPIvisuals(string text, Vector2D textSize = default(Vector2D))
        {
            if(textObject == null)
                textObject = new HudAPIv2.HUDMessage(textSB, Vector2D.Zero, HideHud: !settings.alwaysVisible);

            textObject.Visible = true;
            textObject.Scale = TextAPIScale;

            textSB.Clear().Append(text);

            var textPos = Vector2D.Zero;
            var textOffset = Vector2D.Zero;

            if(Math.Abs(textSize.X) <= 0.0001 && Math.Abs(textSize.Y) <= 0.0001)
                textSize = textObject.GetTextLength();

            if(showMenu) // in the menu
            {
                textOffset = new Vector2D(-textSize.X, textSize.Y / -2);
            }
            else if(settings.textAPIUseCustomStyling) // custom alignment and position
            {
                textPos = settings.textAPIScreenPos;

                if(settings.textAPIAlignRight)
                    textOffset.X = -textSize.X;

                if(settings.textAPIAlignBottom)
                    textOffset.Y = -textSize.Y;
            }
            else if(!rotationHints) // right side autocomputed
            {
                textPos = (aspectRatio > 5 ? TEXT_HUDPOS_RIGHT_WIDE : TEXT_HUDPOS_RIGHT);
                textOffset = new Vector2D(-textSize.X, 0);
            }
            else // left side autocomputed
            {
                textPos = (aspectRatio > 5 ? TEXT_HUDPOS_WIDE : TEXT_HUDPOS);
            }

            textObject.Origin = textPos;
            textObject.Offset = textOffset;

            if(bgObject == null)
                bgObject = new HudAPIv2.BillBoardHUDMessage(MATERIAL_BACKGROUND, Vector2D.Zero, Color.White, Scale: 1, HideHud: !settings.alwaysVisible, Shadowing: true);

            float edge = BACKGROUND_EDGE * TextAPIScale;

            bgObject.BillBoardColor = Color.White * (settings.textAPIBackgroundOpacity >= 0 ? settings.textAPIBackgroundOpacity : hudBackgroundOpacity);
            bgObject.Origin = textPos;
            bgObject.Width = (float)Math.Abs(textSize.X) + edge;
            bgObject.Height = (float)Math.Abs(textSize.Y) + edge;
            bgObject.Offset = textOffset + (textSize / 2);
            bgObject.Visible = true;

            textShown = true;
            return textSize;
        }

        private void UpdateVisualText()
        {
            if(TextAPIEnabled)
            {
                if(!settings.showTextInfo && !showMenu)
                {
                    HideText();
                    return;
                }

                // show last generated block info message
                if(!textShown && textObject != null)
                {
                    var cacheTextAPI = (CacheTextAPI)cache;
                    cacheTextAPI.ResetExpiry();
                    UpdateTextAPIvisuals(cacheTextAPI.Text, cacheTextAPI.TextSize);
                }
            }
            else
            {
                if(!settings.showTextInfo && !showMenu)
                    return;

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

                    if(!Init())
                        return;
                }

                if(!textAPIresponded && textAPI.Heartbeat)
                {
                    textAPIresponded = true;
                    HideText(); // force a re-check to make the HUD -> textAPI transition
                }

                // HUD toggle monitor; required here because it gets the previous value if used in HandleInput()
                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOGGLE_HUD))
                    hudVisible = !MyAPIGateway.Session.Config.MinimalHud;

                unchecked // global ticker
                {
                    ++tick;
                }

                if(leakInfo != null) // update the leak info component
                    leakInfo.Update();

                #region Cubebuilder monitor
                selectedDef = null;
                var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;

                if(def != null && MyCubeBuilder.Static.IsActivated)
                {
                    var hit = MyCubeBuilder.Static.HitInfo as IHitInfo;
                    var grid = hit?.HitEntity as IMyCubeGrid;

                    if(grid != null && grid.GridSizeEnum != def.CubeSize) // if aimed grid is supported by definition size
                    {
                        if(unsupportedGridSizeNotification == null)
                            unsupportedGridSizeNotification = MyAPIGateway.Utilities.CreateNotification("", 100, MyFontEnum.Red);

                        unsupportedGridSizeNotification.Text = $"{def.DisplayNameText} can't be placed on {grid.GridSizeEnum} grid size.";
                        unsupportedGridSizeNotification.Show();
                    }
                    else
                    {
                        selectedDef = def;
                    }
                }

                if(selectedDef != null)
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
                        if(settings.showTextInfo && def.Id != lastDefId)
                        {
                            lastDefId = def.Id;
                            selectedBlockData = null;

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

                    // turn off frozen block preview if camera is too far away from it
                    if(MyAPIGateway.CubeBuilder.FreezeGizmo && Vector3D.DistanceSquared(MyAPIGateway.Session.Camera.WorldMatrix.Translation, lastGizmoPosition) > FREEZE_MAX_DISTANCE_SQ)
                        SetFreezeGizmo(false);
                }
                else // no block equipped
                {
                    selectedBlockData = null;
                    showMenu = false;

                    if(MyAPIGateway.CubeBuilder.FreezeGizmo)
                        SetFreezeGizmo(false);

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
                if(textObject != null)
                    textObject.Visible = false;

                if(bgObject != null)
                    bgObject.Visible = false;

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
            addLineCalled = false;
        }

        private StringBuilder AddLine(string font = MyFontEnum.White)
        {
            EndAddedLines();
            addLineCalled = true;

            ++line;

            if(TextAPIEnabled)
            {
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
                textAPIlines.Append('\n');
            }
            else
            {
                var px = GetStringSizeNotif(notificationLines[line].str);

                largestLineWidth = Math.Max(largestLineWidth, px);

                notificationLines[line].lineWidthPx = px;
            }
        }

        private StringBuilder GetLine()
        {
            return (TextAPIEnabled ? textAPIlines : notificationLines[line].str);
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
            public string Text = null;
            public Vector2D TextPos;
            public Vector2D TextSize;

            public CacheTextAPI(string text, Vector2D textPos, Vector2D textSize)
            {
                ResetExpiry();
                Text = text;
                TextPos = textPos;
                TextSize = textSize;
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
}