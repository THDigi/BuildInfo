using System;
using System.Collections.Generic;
using Digi.BuildInfo.BlockData;
using Digi.BuildInfo.LeakInfo;
using Digi.Input;
using Draygo.API;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum; // HACK allows the use of BlendTypeEnum which is whitelisted but bypasses accessing MyBillboard which is not whitelisted

namespace Digi.BuildInfo
{
    public partial class BuildInfo
    {
        #region Constants
        public const string MOD_NAME = "Build Info";
        public const long MOD_API_ID = 514062285; // API id for other mods to use, see "API Information"

        public const float CUBEBUILDER_PLACE_MINRANGE = 1;
        public const float CUBEBUILDER_PLACE_MAXRANGE = 50;
        public const float CUBEBUILDER_PLACE_DIST_ADD = 5;
        public const float CUBEBUILDER_PLACE_MIN_SIZE = 2.5f;

        public const int CACHE_EXPIRE_SECONDS = 60 * 5; // how long a cached string remains stored until it's purged, in seconds
        public const int CACHE_PURGE_TICKS = 60 * 30; // how frequent the caches are being checked for purging, in ticks
        private const double FREEZE_MAX_DISTANCE_SQ = 50 * 50; // max distance allowed to go from the frozen block preview before it gets turned off.
        private const int SPACE_SIZE = 8; // space character's width; used in HUD notification view mode.
        private const int MAX_LINES = 8; // max amount of HUD notification lines to print; used in HUD notification view mode.
        private const int SCROLL_FROM_LINE = 2; // ignore lines to this line when scrolling, to keep important stuff like mass in view at all times; used in HUD notification view mode.
        public const int MOD_NAME_MAX_LENGTH = 30;
        public const int PLAYER_NAME_MAX_LENGTH = 18;

        private readonly Vector2D TEXT_HUDPOS = new Vector2D(-0.9675, 0.49); // textAPI default left side position
        private readonly Vector2D TEXT_HUDPOS_WIDE = new Vector2D(-0.9675 / 3f, 0.49); // textAPI default left side position when using a really wide resolution
        private readonly Vector2D TEXT_HUDPOS_RIGHT = new Vector2D(0.97, 0.8); // textAPI default right side position
        private readonly Vector2D TEXT_HUDPOS_RIGHT_WIDE = new Vector2D(0.97 / 3f, 0.8); // textAPI default right side position when using a really wide resolution

        private const float BACKGROUND_EDGE = 0.02f; // added padding edge around the text boundary for the background image
        public readonly MyStringId MATERIAL_TOPRIGHTCORNER = MyStringId.GetOrCompute("BuildInfo_UI_TopRightCorner");
        public readonly MyStringId MATERIAL_SQUARE = MyStringId.GetOrCompute("BuildInfo_Square");
        public readonly MyStringId MATERIAL_VANILLA_DOT = MyStringId.GetOrCompute("WhiteDot");
        public readonly MyStringId MATERIAL_VANILLA_SQUARE = MyStringId.GetOrCompute("Square");

        private const int MENU_TOTAL_ITEMS = 10;
        private const float MENU_BG_OPACITY = 0.7f;
        private readonly MyDefinitionId DEFID_MENU = new MyDefinitionId(typeof(MyObjectBuilder_GuiScreen)); // just a random non-block type to use as the menu's ID
        private readonly MyCubeBlockDefinition.MountPoint[] BLANK_MOUNTPOINTS = new MyCubeBlockDefinition.MountPoint[0];
        private BoundingBoxD unitBB = new BoundingBoxD(Vector3D.One / -2d, Vector3D.One / 2d);
        private readonly MyStringHash COMPUTER = MyStringHash.GetOrCompute("Computer");

        private const BlendTypeEnum BLOCKINFO_FG_BLEND_TYPE = BlendTypeEnum.SDR;
        private const BlendTypeEnum BLOCKINFO_BG_BLEND_TYPE = BlendTypeEnum.Standard;
        private readonly Color BLOCKINFO_BG_COLOR = new Vector4(0.20784314f, 0.266666681f, 0.298039228f, 1f);
        private readonly Vector2 BLOCKINFO_SIZE = new Vector2(0.02164f, 0.00076f);
        private const float BLOCKINFO_TEXT_PADDING = 0.001f;
        private const float BLOCKINFO_COMPONENT_HEIGHT = 0.037f; // component height in the vanilla block info
        private const float BLOCKINFO_COMPONENT_WIDTH = 0.011f;
        private const float BLOCKINFO_COMPONENT_UNDERLINE_OFFSET = 0.0062f;
        private const float BLOCKINFO_COMPONENT_HIGHLIGHT_HEIGHT = 0.0014f;
        private const float BLOCKINFO_Y_OFFSET = 0.12f;
        private const float BLOCKINFO_Y_OFFSET_2 = 0.0102f;
        private const float BLOCKINFO_LINE_HEIGHT = 0.0001f;
        private readonly Vector4 BLOCKINFO_LINE_FUNCTIONAL = Color.Red.ToVector4();
        private readonly Vector4 BLOCKINFO_LINE_OWNERSHIP = Color.Blue.ToVector4();
        private readonly Vector4 BLOCKINFO_LINE_COMPLOSS = (Color.Yellow * 0.75f).ToVector4();
        private const float ASPECT_RATIO_54_FIX = 0.938f;

        private const double MOUNTPOINT_THICKNESS = 0.05;
        private const float MOUNTPOINT_ALPHA = 0.65f;
        private const BlendTypeEnum OVERLAY_BLEND_TYPE = BlendTypeEnum.SDR;
        private const BlendTypeEnum MOUNTPOINT_BLEND_TYPE = BlendTypeEnum.Standard;
        private Color MOUNTPOINT_COLOR = new Color(255, 255, 0) * MOUNTPOINT_ALPHA;
        private Color MOUNTPOINT_DEFAULT_COLOR = new Color(255, 200, 0) * MOUNTPOINT_ALPHA;
        private Color AIRTIGHT_COLOR = new Color(0, 155, 255) * MOUNTPOINT_ALPHA;
        private Color AIRTIGHT_TOGGLE_COLOR = new Color(0, 255, 155) * MOUNTPOINT_ALPHA;

        private const double LABEL_SHADOW_OFFSET = 0.007;
        private const double LABEL_SHADOW_OFFSET_Z = 0.01;
        private readonly Color LABEL_SHADOW_COLOR = Color.Black * 0.9f;
        private const BlendTypeEnum LABELS_BLEND_TYPE = BlendTypeEnum.Standard;

        public enum HudMode { OFF, HINTS, NO_HINTS }

        public readonly HashSet<MyObjectBuilderType> DEFAULT_ALLOWED_TYPES = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer) // used in inventory formatting if type argument is null
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
            COLLECTOR,
        }

        private readonly string[] AXIS_LABELS = new string[]
        {
            "Forward",
            "Right",
            "Up",
        };

        public readonly string[] DRAW_OVERLAY_NAME = new string[]
        {
            "OFF",
            "Airtightness",
            "Mounting",
        };

        private const string CMD_BUILDINFO = "/bi";
        private const string CMD_BUILDINFO_OLD = "/buildinfo";
        private const string CMD_HELP = "/bi help";
        private const string CMD_RELOAD = "/bi reload";
        private const string CMD_CLEARCACHE = "/bi clearcache";
        private const string CMD_MODLINK = "/bi modlink";
        private const string CMD_LASERPOWER = "/bi laserpower";
        private const string CMD_GETBLOCK = "/bi getblock";
        private const StringComparison CMD_COMPARE_TYPE = StringComparison.InvariantCultureIgnoreCase;

        private const string HELP_FORMAT =
            "Chat commands:\n" +
            "  /bi or /buildinfo\n" +
            "    shows this window or menu if you're holding a block.\n" +
            "  /bi help\n" +
            "    shows this window.\n" +
            "  /bi reload\n" +
            "    reloads the config.\n" +
            "  /bi getblock [1~9]\n" +
            "    Picks the aimed block to be placed in toolbar.\n" +
            "  /bi modlink\n" +
            "    Opens steam overlay with workshop on the selected block's mod.\n" +
            "  /bi laserpower <km>\n" +
            "    Calculates power needed for equipped/aimed laser antenna\n" +
            "    at the specified range.\n" +
            "  /bi clearcache\n" +
            "    clears the block info cache, not for normal use.\n" +
            "\n" +
            "\n" +
            "Hotkeys:\n" +
            "  {0} show/hide menu\n" +
            "    Can be changed in config.\n" +
            "  {1} with block equipped/selected\n" +
            "    Cycles overlay draw.\n" +
            "  {2} with block equipped/selected\n" +
            "    Toggle transparent model.\n" +
            "  {3} with block equipped\n" +
            "    Toggle freeze position.\n" +
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
            "  Orange mount point is the one used for auto-rotation.\n" +
            "\n" +
            "  Airtightness also uses the mount points system, if a\n" +
            "    mount point spans accross an entire grid cell face\n" +
            "    then that face is airtight.\n" +
            "\n" +
            "\n" +
            "[1] Laser antenna power usage is linear up to 200km, after\n" +
            "   that it's a quadratic ecuation.\n" +
            "   To calculate it at your needed distance, hold a laser antenna\n" +
            "   and type in chat: /bi laserpower <km>" +
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

        public enum TriState { None, Off, On }

        private readonly MyStringId[] CONTROL_SLOTS = new MyStringId[]
        {
            MyControlsSpace.SLOT0,
            MyControlsSpace.SLOT1,
            MyControlsSpace.SLOT2,
            MyControlsSpace.SLOT3,
            MyControlsSpace.SLOT4,
            MyControlsSpace.SLOT5,
            MyControlsSpace.SLOT6,
            MyControlsSpace.SLOT7,
            MyControlsSpace.SLOT8,
            MyControlsSpace.SLOT9,
        };
        #endregion

        #region Fields
        public static BuildInfo Instance;

        public bool IsInitialized = false;
        public bool IsPlayer = true;
        public InputLib InputLib;
        public Settings Settings;
        public LeakInfoComponent LeakInfoComp;
        public TerminalInfoComponent TerminalInfoComp;
        public BlockMonitorComponent BlockMonitorComp;
        public short Tick = 0; // global incrementing gamelogic tick

        private MatrixD viewProjInvCache;
        private bool viewProjInvCompute = true;

        private float scaleFovCache;
        private bool scaleFovCompute = true;

        public MyDefinitionId lastDefId; // last selected definition ID, can be set to MENU_DEFID too!
        private IMySlimBlock selectedBlock; // non-null only when welder/grinder aims at a block
        private MyCubeBlockDefinition selectedDef; // non-null when cubebuilder has a block AND welder/grinder aims at a block
        private MyDefinitionId selectedToolDefId; // used to regenerate the text when changing equipped tools
        private IMyEngineerToolBase selectedHandTool;
        private MyCubeBlockDefinition pickBlockDef; // used by the 'pick block from world' feature
        private IMyEntity prevHeldTool;
        private MyCasterComponent shipCasterComp;
        private MyCasterComponent heldCasterComp;
        private IMyControllableEntity prevControlled;
        private float selectedGridSize;
        private bool isToolSelected = false;
        private bool IsGrinder => (selectedToolDefId.TypeId == typeof(MyObjectBuilder_AngleGrinder) || selectedToolDefId.TypeId == typeof(MyObjectBuilder_ShipGrinder));
        private int computerCompIndex = -1;
        private List<CompLoss> componentLossIndexes = new List<CompLoss>();

        private class CompLoss
        {
            public readonly int Index;
            public readonly MyPhysicalItemDefinition Replaced;
            public HudAPIv2.SpaceMessage Msg;

            public CompLoss(int index, MyPhysicalItemDefinition item)
            {
                Index = index;
                Replaced = item;
            }

            public void Close()
            {
                Msg?.DeleteMessage();
            }
        }

        public bool TextAPIEnabled { get { return (useTextAPI && TextAPI != null && TextAPI.Heartbeat); } }
        public BData_Base BlockDataCache;
        public bool BlockDataCacheValid = true;

        private bool useTextAPI = true; // the user's preference for textAPI or notification; use TextAPIEnabled to determine if you need to use textAPI or not!
        private bool textAPIresponded = false; // if textAPI.Heartbeat returned true yet
        private bool textShown = false;
        private bool canShowMenu = false;
        private bool aimInfoNeedsUpdate = false;
        private Vector3D lastGizmoPosition;
        private Cache cache = null; // currently selected cache to avoid another dictionary lookup in Draw()

        private IMyHudNotification buildInfoNotification;
        private IMyHudNotification overlayNotification;
        private IMyHudNotification transparencyNotification;
        private IMyHudNotification freezeGizmoNotification;
        private IMyHudNotification unsupportedGridSizeNotification;

        // resource sink group cache
        public int resourceSinkGroups = 0;
        public int resourceSourceGroups = 0;
        public readonly Dictionary<MyStringHash, ResourceGroupData> resourceGroupPriority
                  = new Dictionary<MyStringHash, ResourceGroupData>(MyStringHash.Comparer);

        // character size cache for notifications; textAPI has its own.
        private readonly Dictionary<char, int> charSize = new Dictionary<char, int>();

        // cached block data that is inaccessible via definitions (like thruster flames)
        public readonly Dictionary<MyDefinitionId, BData_Base> BlockData = new Dictionary<MyDefinitionId, BData_Base>(MyDefinitionId.Comparer);
        public readonly HashSet<MyDefinitionId> BlockSpawnInProgress = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        // various temporary caches
        private readonly HashSet<Vector3I> cubes = new HashSet<Vector3I>(Vector3I.Comparer);
        private readonly List<MyDefinitionId> removeCacheIds = new List<MyDefinitionId>();
        public readonly Dictionary<string, IMyModelDummy> dummies = new Dictionary<string, IMyModelDummy>();
        #endregion
    }
}
