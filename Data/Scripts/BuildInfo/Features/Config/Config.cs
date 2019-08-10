using System.Text;
using Digi.ConfigLib;
using Digi.Input;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Input;
using VRageMath;
using static Digi.Input.InputLib;

namespace Digi.BuildInfo.Features.Config
{
    public class Config : ClientComponent
    {
        public readonly ConfigHandler Handler;

        public const string FILE_NAME = "config.ini";
        public const int CFGV_MENU_BIND = 2;
        public const int CFGV_LATEST = 3;

        public BoolSetting TextShow;
        public BoolSetting TextAlwaysVisible;
        public FlagsSetting<PlaceInfoFlags> PlaceInfo;
        public FlagsSetting<AimInfoFlags> AimInfo;
        public BoolSetting BlockInfoStages;
        public BoolSetting ShipToolInventoryBar;
        public BoolSetting TurretAmmo;
        public BoolSetting TextAPICustomStyling;
        public Vector2DSetting TextAPIScreenPosition;
        public TextAlignSetting TextAPIAlign;
        public FloatSetting TextAPIScale;
        public BackgroundOpacitySetting TextAPIBackgroundOpacity;
        public BoolSetting OverlaysAlwaysVisible;
        public FlagsSetting<OverlayLabelsFlags> OverlayLabels;
        public ColorSetting LeakParticleColorWorld;
        public ColorSetting LeakParticleColorOverlay;
        public BoolSetting AdjustBuildDistanceSurvival;
        public BoolSetting AdjustBuildDistanceShipCreative;
        public InputCombinationSetting MenuBind;
        public InputCombinationSetting CycleOverlaysBind;
        public InputCombinationSetting ToggleTransparencyBind;
        public InputCombinationSetting FreezePlacementBind;
        public InputCombinationSetting BlockPickerBind;
        public BoolSetting InternalInfo;
        public BoolSetting Debug;

        public const string MENU_BIND_INPUT_NAME = "bi.menu";
        public const string CYCLE_OVERLAYS_INPUT_NAME = "bi.cycleOverlays";
        public const string TOGGLE_TRANSPARENCY_INPUT_NAME = "bi.toggleTransparency";
        public const string FREEZE_PLACEMENT_INPUT_NAME = "bi.freezePlacement";
        public const string BLOCK_PICKER_INPUT_NAME = "bi.blockPicker";

        public Config(Client mod) : base(mod)
        {
            Handler = new ConfigHandler(FILE_NAME, CFGV_LATEST);
        }

        public override void RegisterComponent()
        {
            Handler.SettingsLoaded += SettingsLoaded;

            InitSettings();

            Load();
            Save();
        }

        private void SettingsLoaded()
        {
            if(MenuBind.Value.CombinationString.Equals("c.VoxelHandSettings", System.StringComparison.OrdinalIgnoreCase))
            {
                var voxelHandSettingsKey = MyAPIGateway.Input.GetGameControl(MyControlsSpace.VOXEL_HAND_SETTINGS).GetKeyboardControl();
                var terminalInventoryKey = MyAPIGateway.Input.GetGameControl(MyControlsSpace.TERMINAL).GetKeyboardControl();

                if(voxelHandSettingsKey != MyKeys.None && voxelHandSettingsKey == terminalInventoryKey)
                {
                    MenuBind.ResetToDefault();

                    Log.Info("Reset MenuBind in config to default because VoxelHandSettings and Terminal/Inventory are on the same key.");
                }
            }

            var cfgv = Handler.ConfigVersion.Value;

            if(cfgv >= CFGV_LATEST)
                return;

            if(cfgv == CFGV_MENU_BIND)
            {
                // check if existing mod users have the VoxelHandSettings key not colliding and keep using that

                var voxelHandSettingsControl = MyAPIGateway.Input.GetGameControl(MyControlsSpace.VOXEL_HAND_SETTINGS);
                var terminalInventoryControl = MyAPIGateway.Input.GetGameControl(MyControlsSpace.TERMINAL);

                // if VoxelHandSettings isn't colliding, then set the defaults like the user is used to
                if(voxelHandSettingsControl.GetKeyboardControl() != MyKeys.None && terminalInventoryControl.GetKeyboardControl() != voxelHandSettingsControl.GetKeyboardControl())
                {
                    MenuBind.Value = Combination.Create(MENU_BIND_INPUT_NAME, "c.VoxelHandSettings");

                    Log.Info("NOTE: Configurable binds were added and it seems your VoxelHandSettings isn't colliding so I'm setting MenuBind to that instead so you don't need to change anything.");
                }

                return;
            }
        }

        private void InitSettings()
        {
            Handler.HeaderComments.Add($"You can reload this while in game by typing in chat: {ChatCommands.CMD_RELOAD}");

            var sb = new StringBuilder(8000);
            InputLib.AppendInputBindingInstructions(sb, ConfigHandler.COMMENT_PREFIX);
            Handler.FooterComments.Add(sb.ToString());

            TextShow = new BoolSetting(Handler, "Text: Show", true,
                "Toggles if both text info boxes are shown");

            TextAlwaysVisible = new BoolSetting(Handler, "Text: Always Visible", false,
                "Setting to true causes the text info box to be visible regardless of HUD state");

            PlaceInfo = new FlagsSetting<PlaceInfoFlags>(Handler, "Text: Block-Place Info Filtering", PlaceInfoFlags.All,
                "Choose what information is shown in the text box when placing a block.",
                "Disabling all of them will effectively hide the box.");

            AimInfo = new FlagsSetting<AimInfoFlags>(Handler, "Text: Block-Aim Info Filtering", AimInfoFlags.All,
                "Choose what information is shown in the text box when aiming at a block with a tool.",
                "Disabling all of them will effectively hide the box.");

            BlockInfoStages = new BoolSetting(Handler, "HUD: Block Info Stages", true,
                "Shows red line for functional and blue line for ownership on components list in block info panel.");

            ShipToolInventoryBar = new BoolSetting(Handler, "HUD: Ship Tool Inventory Bar", true,
                "Shows an inventory bar when a ship tool is selected.");

            TurretAmmo = new BoolSetting(Handler, "HUD: Turret Ammo", true,
                "Shows total ammo that's in the turret when controlling one.");

            TextAPIScale = new FloatSetting(Handler, "TextAPI: Scale", defaultValue: 1.0f, min: 0.1f, max: 3f, commentLines: new string[]
                {
                    "The overall text info panel scale."
                });

            TextAPIBackgroundOpacity = new BackgroundOpacitySetting(Handler, "TextAPI: Background Opacity", -1,
                "Text info background opacity.",
                "Set to '-1' or 'HUD' to use game HUD's background opacity.");

            TextAPICustomStyling = new BoolSetting(Handler, "TextAPI: Custom Styling", false,
                "Enables the use of ScrenPos and Alignment settings which allows you to place the text info box anywhere you want.",
                "If false, the text info box will be placed according to rotation hints (the cube and key hints top right).",
                "(If false) With rotation hints off, text info will be set top-left, otherwise top-right.");

            TextAPIScreenPosition = new Vector2DSetting(Handler, "TextAPI: Screen Position", defaultValue: new Vector2D(0.9692, 0.26), min: new Vector2D(-1, -1), max: new Vector2D(1, 1), commentLines: new string[]
                {
                    "Screen position in X and Y coordinates where 0,0 is the screen center.",
                    "Positive values are right and up, while negative ones are opposite of that.",
                    $"NOTE: Requires {TextAPICustomStyling.Name} = true"
                });

            TextAPIAlign = new TextAlignSetting(Handler, "TextAPI: Alignment", TextAlignFlags.Bottom | TextAlignFlags.Right,
                "Determine the pivot point of the text info box. Stretches in opposite direction of that.",
                $"NOTE: Requires {TextAPICustomStyling.Name} = true");

            OverlaysAlwaysVisible = new BoolSetting(Handler, "Overlays: Always Visible", false,
                "Setting to true causes the block overlays to be visible regardless of HUD state");

            OverlayLabels = new FlagsSetting<OverlayLabelsFlags>(Handler, "Overlay: Labels", OverlayLabelsFlags.All,
                "Pick what labels can be shown for overlays.",
                "Not yet fully expanded to include detailed settings, just axes and eveything else for now.");

            LeakParticleColorWorld = new ColorSetting(Handler, "LeakInfo: Particle Color World", new Color(0, 120, 255), false,
                "Color of airleak indicator particles in world, only visible if nothing is in the way.");

            LeakParticleColorOverlay = new ColorSetting(Handler, "LeakInfo: Particle Color Overlay", new Color(255, 255, 0), false,
                "Color of airleak indicator particles that are seen through walls.",
                "NOTE: This color is overlayed on top of the world ones when those are visible too, making the colors mix, so pick wisely!");

            AdjustBuildDistanceSurvival = new BoolSetting(Handler, "Adjust Build Distance Survival", true,
                "Enable ctrl+scroll to change block placement distance in survival (for both character and ship)");

            AdjustBuildDistanceShipCreative = new BoolSetting(Handler, "Adjust Build Distance Ship Creative", true,
                "Enable ctrl+scroll to change block placement distance when in cockpit build mode in creative.",
                "The game currently doesn't allow this and it might get it fixed, that's why this exist as a separate setting.");

            MenuBind = new InputCombinationSetting(Handler, "Bind: Menu", Combination.Create(MENU_BIND_INPUT_NAME, "plus"),
                "For accessing the quick menu.");

            CycleOverlaysBind = new InputCombinationSetting(Handler, "Bind: Cycle Overlays", Combination.Create(CYCLE_OVERLAYS_INPUT_NAME, "ctrl " + MENU_BIND_INPUT_NAME),
                $"For cycling through block overlays ({string.Join(", ", Overlays.NAMES)}).");

            ToggleTransparencyBind = new InputCombinationSetting(Handler, "Bind: Toggle Transparency", Combination.Create(TOGGLE_TRANSPARENCY_INPUT_NAME, "shift " + MENU_BIND_INPUT_NAME),
                "For toggling block transparency when equipped.");

            FreezePlacementBind = new InputCombinationSetting(Handler, "Bind: Freeze Placement", Combination.Create(FREEZE_PLACEMENT_INPUT_NAME, "alt " + MENU_BIND_INPUT_NAME),
                "For locking the block ghost in place.");

            BlockPickerBind = new InputCombinationSetting(Handler, "Bind: Block Picker", Combination.Create(BLOCK_PICKER_INPUT_NAME, "ctrl c.cubesizemode"),
                "The bind for adding the aimed block to the toolbar.",
                "NOTE: It does request a number press afterwards.",
                "NOTE: This feature is disabled in MP because of issues, see: https://support.keenswh.com/spaceengineers/general/topic/187-2-modapi-settoolbarslottoitem-causes-everyone-in-server-to-disconnect"); // FIXME pick block temporarily disabled in MP

            InternalInfo = new BoolSetting(Handler, "Internal Info", false,
                "Enables various info useful for server admins, PB scripters and modders.",
                "Currently it adds:",
                "- Block Type+SubType and BlockPairName in aim&place info",
                "- Rotor angle from API in terminal info as it differs from what game already prints.",
                "- Piston extended position from API in terminal info in case it differs in some cases.");

            Debug = new BoolSetting(Handler, "Debug", false,
                "For debugging purposes only, not for normal use!",
                "Debug info shown for: PlacementDistance, EquipmentMonitor");
        }

        public void Save()
        {
            Handler.SaveToFile();
        }

        public bool Load()
        {
            return Handler.LoadFromFile();
        }

        public void Reload()
        {
            Handler.LoadFromFile();
            Handler.SaveToFile();
        }
    }
}
