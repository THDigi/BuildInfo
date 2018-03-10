using System.Collections.Generic;
using System.Text;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

using Draygo.API;

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

        private string[] DRAW_OVERLAY_NAME = new string[]
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
        public LeakInfo leakInfo = null; // leak info component
        private short tick = 0; // global incrementing gamelogic tick
        private int drawOverlay = 0;
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
    }
}
