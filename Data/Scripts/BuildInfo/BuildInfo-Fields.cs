using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Blocks;
using Draygo.API;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum; // HACK allows the use of BlendTypeEnum which is whitelisted but bypasses accessing MyBillboard which is not whitelisted

namespace Digi.BuildInfo
{
    public partial class BuildInfo
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
        private readonly MyStringId MATERIAL_BACKGROUND = MyStringId.GetOrCompute("BuildInfo_UI_Background");
        private readonly MyStringId MATERIAL_TOPRIGHTCORNER = MyStringId.GetOrCompute("BuildInfo_UI_TopRightCorner");
        private readonly MyStringId MATERIAL_SQUARE = MyStringId.GetOrCompute("BuildInfo_Square");
        private readonly MyStringId MATERIAL_VANILLA_DOT = MyStringId.GetOrCompute("WhiteDot");
        private readonly MyStringId MATERIAL_VANILLA_SQUARE = MyStringId.GetOrCompute("Square");

        private const int MENU_TOTAL_ITEMS = 9;
        private readonly MyDefinitionId DEFID_MENU = new MyDefinitionId(typeof(MyObjectBuilder_GuiScreen)); // just a random non-block type to use as the menu's ID
        private readonly MyCubeBlockDefinition.MountPoint[] BLANK_MOUNTPOINTS = new MyCubeBlockDefinition.MountPoint[0];
        private BoundingBoxD unitBB = new BoundingBoxD(Vector3D.One / -2d, Vector3D.One / 2d);
        private readonly MyStringHash COMPUTER = MyStringHash.GetOrCompute("Computer");

        private const BlendTypeEnum BLOCKINFO_BLEND_TYPE = BlendTypeEnum.SDR; // allows sprites to be rendered on HUD-level, unaffected by flares or post processing
        private readonly Color BLOCKINFO_BG_COLOR = new Vector4(0.20784314f, 0.266666681f, 0.298039228f, 1f);
        private const float BLOCKINFO_ITEM_HEIGHT = 0.037f; // component height in the vanilla block info
        private const float BLOCKINFO_ITEM_HEIGHT_UNDERLINE = BLOCKINFO_ITEM_HEIGHT * 0.88f;
        private const float BLOCKINFO_TEXT_PADDING = 0.001f;
        private const float BLOCKINFO_COMPONENT_LIST_WIDTH = 0.01777f;
        private const float BLOCKINFO_COMPONENT_LIST_SELECT_HEIGHT = 0.0015f;
        private const float BLOCKINFO_RED_LINE_HEIGHT = 0.0001f;
        private const float BLOCKINFO_BLUE_LINE_HEIGHT = 0.0002f; // slightly taller to be visible when they overlap
        private const float BLOCKINFO_Y_OFFSET = 0.12f;
        private const float BLOCKINFO_Y_OFFSET_2 = 0.0102f;
        private const float ASPECT_RATIO_54_FIX = 0.938f;

        private const double MOUNTPOINT_THICKNESS = 0.05;
        private const float MOUNTPOINT_ALPHA = 0.65f;
        private const BlendTypeEnum MOUNTPOINT_BLEND_TYPE = BlendTypeEnum.Standard;
        private Color MOUNTPOINT_COLOR = new Color(255, 255, 0) * MOUNTPOINT_ALPHA;
        private Color MOUNTPOINT_DEFAULT_COLOR = new Color(255, 200, 0) * MOUNTPOINT_ALPHA;
        private Color AIRTIGHT_COLOR = new Color(0, 155, 255) * MOUNTPOINT_ALPHA;
        private Color AIRTIGHT_TOGGLE_COLOR = new Color(0, 255, 155) * MOUNTPOINT_ALPHA;

        private const double LABEL_SHADOW_OFFSET = 0.007;
        private const double LABEL_SHADOW_OFFSET_Z = 0.01;
        private readonly Color LABEL_SHADOW_COLOR = Color.Black * 0.9f;
        private const BlendTypeEnum LABELS_BLEND_TYPE = BlendTypeEnum.Standard;

        public readonly Color COLOR_BLOCKTITLE = new Color(50, 155, 255);
        public readonly Color COLOR_BLOCKVARIANTS = new Color(255, 233, 55);
        public readonly Color COLOR_NORMAL = Color.White;
        public readonly Color COLOR_GOOD = Color.Lime;
        public readonly Color COLOR_BAD = Color.Red;
        public readonly Color COLOR_WARNING = Color.Yellow;
        public readonly Color COLOR_UNIMPORTANT = Color.Gray;
        public readonly Color COLOR_PART = new Color(55, 255, 155);
        public readonly Color COLOR_MOD = Color.DeepSkyBlue;
        public readonly Color COLOR_OWNER = new Color(55, 255, 255);

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

        private string[] DRAW_OVERLAY_NAME = new string[]
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
            "  /bi clearcache\n" +
            "    clears the block info cache, not for normal use.\n" +
            "  /bi laserpower <km>\n" +
            "    Calculates power needed for equipped/aimed laser antenna\n" +
            "    at the specified range.\n" +
            "  /bi getblock [1~9]\n" +
            "    Picks the aimed block to be placed in toolbar.\n" +
            "\n" +
            "\n" +
            "Hotkeys:\n" +
            "  Ctrl+{0} and block equipped\n" +
            "    Cycles overlay draw.\n" +
            "  Shift+{0} and block equipped\n" +
            "    Toggle transparent model.\n" +
            "  Alt+{0} and block equipped\n" +
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
            "\n" +
            "  Airtightness also uses the mount points system, if a\n" +
            "    mount point spans accross an entire grid cell face\n" +
            "    then that face is airtight.\n" +
            "\n" +
            "\n" +
            "[1] Laser antenna power usage is linear up to 200km, after\n" +
            "   that it's a quadratic ecuation.\n" +
            "\n  To calculate it at your needed distance, hold a laser antenna\n" +
            "    and type in chat: /laserpower <km>" +
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
        public LeakInfoComponent leakInfoComp = null;
        public short tick = 0; // global incrementing gamelogic tick
        private int drawOverlay = 0;
        private bool overlaysDrawn = false;
        private bool doorAirtightBlink = false;
        private int doorAirtightBlinkTick = 0;
        private MyDefinitionId lastDefId; // last selected definition ID, can be set to MENU_DEFID too!
        public IMySlimBlock selectedBlock = null; // non-null only when welder/grinder aims at a block
        public MyCubeBlockDefinition selectedDef = null; // non-null when cubebuilder has a block AND welder/grinder aims at a block
        public MyDefinitionId selectedToolDefId; // used to regenerate the text when changing equipped tools
        public IMyEngineerToolBase selectedHandTool;
        private MyObjectBuilder_ShipController shipControllerObj = null;
        private MyCasterComponent prevCasterComp = null;
        public bool isToolSelected = false;
        public bool IsGrinder => (selectedToolDefId.TypeId == typeof(MyObjectBuilder_AngleGrinder) || selectedToolDefId.TypeId == typeof(MyObjectBuilder_ShipGrinder));
        public bool canShowMenu = false;
        public bool aimInfoNeedsUpdate = false;
        public BlockDataBase blockDataCache = null;
        private bool useTextAPI = true; // the user's preference for textAPI or notification; use TextAPIEnabled to determine if you need to use textAPI or not!
        private bool textAPIresponded = false; // if textAPI.Heartbeat returned true yet
        public bool TextAPIEnabled { get { return (useTextAPI && textAPI != null && textAPI.Heartbeat); } }
        private Cache cache = null; // currently selected cache to avoid another dictionary lookup in Draw()
        private bool textShown = false;
        private Vector3D lastGizmoPosition;
        private MatrixD viewProjInv;
        private MyCubeBlockDefinition pickBlockDef = null;
        private IMyHudNotification buildInfoNotification = null;
        private IMyHudNotification overlayNotification = null;
        private IMyHudNotification transparencyNotification = null;
        private IMyHudNotification freezeGizmoNotification = null;
        private IMyHudNotification unsupportedGridSizeNotification = null;
        private string voxelHandSettingsInput;

        // menu specific stuff
        private bool showMenu = false;
        private bool menuNeedsUpdate = true;
        private int menuSelectedItem = 0;

        // used by the textAPI view mode
        public HudAPIv2 textAPI = null;
        private bool rotationHints = true;
        private bool hudVisible = true;
        private double aspectRatio = 1;
        private float hudBackgroundOpacity = 1f;
        private int lines = 0;
        private StringBuilder textAPIlines = new StringBuilder();
        private HudAPIv2.HUDMessage textObject = null;
        private HudAPIv2.BillBoardHUDMessage bgObject = null;
        private HudAPIv2.SpaceMessage[] textAPILabels;
        private HudAPIv2.SpaceMessage[] textAPIShadows;
        private readonly Dictionary<MyDefinitionId, Cache> cachedBuildInfoTextAPI = new Dictionary<MyDefinitionId, Cache>();
        private float TextAPIScale => settings.textAPIScale * TEXTAPI_SCALE_BASE;
        private const float TEXTAPI_SCALE_BASE = 1.2f;

        // used by the HUD notification view mode
        private int atLine = SCROLL_FROM_LINE;
        private long lastScroll = 0;
        private int largestLineWidth = 0;
        private List<HudLine> notificationLines = new List<HudLine>();
        private readonly Dictionary<MyDefinitionId, Cache> cachedBuildInfoNotification = new Dictionary<MyDefinitionId, Cache>();
        public readonly List<IMyHudNotification> hudNotifLines = new List<IMyHudNotification>();

        // used in generating the block info text or menu for either view mode
        private int line = -1;
        private bool addLineCalled = false;

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
    }
}
