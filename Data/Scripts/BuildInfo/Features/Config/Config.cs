using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.ChatCommands;
using Digi.BuildInfo.Features.HUD;
using Digi.ConfigLib;
using Digi.Input;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Input;
using VRageMath;
using static Digi.Input.InputLib;

namespace Digi.BuildInfo.Features.Config
{
    public class Config : ModComponent
    {
        public readonly ConfigHandler Handler;

        public const string FileName = "config.ini";
        public const string KillswitchName = "Killswitch";
        public const int ConfigVersion = 6;
        public const int VersionCompat_ShipToolInvBar_FixPosition = 5;
        public const int VersionCompat_ToolbarLabels_Redesign = 4;
        public const int VersionCompat_MenuBind = 2;

        public BoolSetting Killswitch;

        public BoolSetting TextShow;
        public BoolSetting TextAlwaysVisible;

        public FlagsSetting<PlaceInfoFlags> PlaceInfo;
        public FlagsSetting<AimInfoFlags> AimInfo;

        public BoolSetting BlockInfoAdditions;

        public BoolSetting ShipToolInvBarShow;
        public Vector2DSetting ShipToolInvBarPosition;
        public Vector2DSetting ShipToolInvBarScale;

        public BoolSetting BackpackBarOverride;

        public BoolSetting RelativeDampenerInfo;

        public BoolSetting ItemTooltipAdditions;

        public BoolSetting HudStatOverrides;

        public BoolSetting TurretHUD;

        public IntegerSetting ToolbarLabels;
        public IntegerSetting ToolbarItemNameMode;
        public BoolSetting ToolbarLabelsShowTitle;
        public IntegerSetting ToolbarStyleMode;
        public Vector2DSetting ToolbarLabelsPosition;
        public Vector2DSetting ToolbarLabelsInMenuPosition;
        public FloatSetting ToolbarLabelsScale;
        public Vector2DSetting ToolbarLabelsOffsetForInvBar;
        public BoolSetting ToolbarActionStatus;

        public BoolSetting TerminalDetailInfoAdditions;

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
        public InputCombinationSetting LockOverlayBind;

        public BoolSetting InternalInfo;

        public BoolSetting Debug;

        public IntegerSetting ModVersion;

        public const string MENU_BIND_INPUT_NAME = "bi.menu";
        public const string CYCLE_OVERLAYS_INPUT_NAME = "bi.cycleOverlays";
        public const string TOGGLE_TRANSPARENCY_INPUT_NAME = "bi.toggleTransparency";
        public const string FREEZE_PLACEMENT_INPUT_NAME = "bi.freezePlacement";
        public const string BLOCK_PICKER_INPUT_NAME = "bi.blockPicker";
        public const string LOCK_OVERLAY_INPUT_NAME = "bi.lockOverlay";

        public Config(BuildInfoMod main) : base(main)
        {
            Handler = new ConfigHandler(FileName, ConfigVersion);
        }

        protected override void RegisterComponent()
        {
            Handler.SettingsLoaded += SettingsLoaded;

            InitSettings();

            Load();
            Save();
        }

        protected override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Handler.SettingsLoaded -= SettingsLoaded;
        }

        private void SettingsLoaded()
        {
            if(MenuBind.Value.CombinationString.Equals("c.VoxelHandSettings", StringComparison.OrdinalIgnoreCase))
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
            if(cfgv <= 0 || cfgv >= ConfigVersion)
                return;

            if(cfgv <= VersionCompat_ShipToolInvBar_FixPosition)
            {
                // old defaults were in the wrong value ranges, convert values to new space.
                // e.g. 0.5, 0.84 => 0.0, -0.68

                var oldVec = ShipToolInvBarPosition.Value;
                ShipToolInvBarPosition.Value = new Vector2D((oldVec.X * 2) - 1, 1 - (oldVec.Y * 2));

                Log.Info($"NOTE: Value for '{ShipToolInvBarPosition.Name}' was changed into a different space (the proper one), your setting was automatically calculated into it so no changes are necessary.");

                // old in-menu default wasn't adjusted for recent title addition
                if(Vector2D.DistanceSquared(ToolbarLabelsInMenuPosition.Value, new Vector2D(0.128, -0.957)) <= 0.001)
                {
                    ToolbarLabelsInMenuPosition.ResetToDefault();

                    Log.Info($"NOTE: Default value for '{ToolbarLabelsInMenuPosition.Name}' changed and yours was the old default, setting reset to new default.");
                }
            }

            if(cfgv == VersionCompat_ToolbarLabels_Redesign)
            {
                // change default position of the labels box

                if(Vector2D.DistanceSquared(ToolbarLabelsPosition.Value, new Vector2D(-0.716, -0.707)) <= 0.001)
                {
                    ToolbarLabelsPosition.ResetToDefault();

                    Log.Info($"NOTE: Default value for '{ToolbarLabelsPosition.Name}' changed and yours was the old default, setting reset to new default.");
                }
            }

            if(cfgv == VersionCompat_MenuBind)
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
            Handler.HeaderComments.Add($"You can reload this while in game by typing in chat: {ChatCommandHandler.MAIN_COMMAND} reload");

            var sb = new StringBuilder(8000);
            InputLib.AppendInputBindingInstructions(sb, ConfigHandler.COMMENT_PREFIX);
            Handler.FooterComments.Add(sb.ToString());

            Killswitch = new BoolSetting(Handler, KillswitchName, false,
                "Prevents the most of the mod scripts from loading.",
                "Requires world reload/rejoin to work.");

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

            BlockInfoAdditions = new BoolSetting(Handler, "HUD: Block Info Additions", true,
                "Shows red line for functional and blue line for ownership on components list in block info panel.",
                "Also shows components that grind into something else (e.g. battery's power cells) if textAPI is allowed, otherwise it highlights the component yellow.");
            BlockInfoAdditions.AddCompatibilityNames("HUD: Block Info Stages");

            ShipToolInvBarShow = new BoolSetting(Handler, "HUD: Ship Tool Inventory Bar", true,
                "Shows an inventory bar when a ship tool is selected.");

            ShipToolInvBarPosition = new Vector2DSetting(Handler, "HUD: Ship Tool Inventory Bar Position", defaultValue: new Vector2D(0f, -0.68f), min: new Vector2D(-1, -1), max: new Vector2D(1, 1), commentLines: new string[]
            {
                "The screen position (center pivot) of the ship tool inventory bar.",
                "Screen position in X and Y coordinates where 0,0 is the screen center.",
                "Positive values are right and up, while negative ones are opposite of that.",
            });

            ShipToolInvBarScale = new Vector2DSetting(Handler, "HUD: Ship Tool Inventory Bar Scale", defaultValue: new Vector2D(1, 1), min: new Vector2D(0.1, 0.1), max: new Vector2D(3, 3), commentLines: new string[]
            {
                "The width and height scale of the ship tool inventory bar.",
            });

            BackpackBarOverride = new BoolSetting(Handler, "HUD: Backpack Bar Override", true,
                "This affects the vanilla inventory bar (with a backpack icon), if enabled:",
                "When in a seat, it shows the filled ratio of ship's Cargo Containers.",
                $"If a group named {BackpackBarStat.GroupName} exists, the bar will show filled ratio of all blocks there (regardless of type).");

            TurretHUD = new BoolSetting(Handler, "HUD: Turret Info", true,
                "Shows turret ammo and some ship stats because the entire HUD is missing.");
            TurretHUD.AddCompatibilityNames("HUD: Turret Ammo");

            HudStatOverrides = new BoolSetting(Handler, "HUD: Stat Overrides", true,
                "Overrides some values from the HUD (health, energy, hydrogen, etc), showing units and some behaviors slightly altered.");

            ItemTooltipAdditions = new BoolSetting(Handler, "Item Tooltip Additions", true,
                "Info about mod and conveyor tube size requirements for item tooltips, seen in inventories.",
                "Includes the '*' from the icon when large conveyor is required.");

            RelativeDampenerInfo = new BoolSetting(Handler, "HUD: Relative Dampeners Info", true,
                "Shows a centered HUD message when relative dampeners are set to a target and when they're disengaged from one.",
                "Only shows if relative damps are enabled for new controlled entity (character, ship, etc).");

            ToolbarLabels = CreateEnumSetting("Toolbar: Labels Mode", ToolbarLabelsMode.AltKey, new string[]
            {
                "Customize ship toolbar block action's labels.",
                "Turning this off turns off the rest of the toolbar action stuff.",
                $"Also, upon entering a cockpit labels are shown for {(ToolbarInfo.ToolbarLabelRender.ShowForTicks / Constants.TICKS_PER_SECOND).ToString()} seconds for HudHints&AltKey modes."
            },
            new Dictionary<ToolbarLabelsMode, string>()
            {
                [ToolbarLabelsMode.HudHints] = "can also be shown with ALT in this mode",
            });

            ToolbarItemNameMode = CreateEnumSetting("Toolbar: Item Name Mode", ToolbarNameMode.AlwaysShow, new string[]
            {
                "Pick what blocks should have their custom name printed in the action label.",
                "Visibility of this is affected by the above setting."
            },
            new Dictionary<ToolbarNameMode, string>()
            {
                [ToolbarNameMode.InMenuOnly] = "only shown when toolbar menu is open",
                [ToolbarNameMode.GroupsOnly] = "only block group names",
            });

            ToolbarLabelsShowTitle = new BoolSetting(Handler, "Toolbar: Toolbar Labels Show Title", true,
                "Toggles if the 'Toolbar Info  (BuildInfo Mod)' title is shown on the box.",
                "This exists so that people can know what that box is from so they can know which mod to lookup/configure.");

            ToolbarStyleMode = CreateEnumSetting("Toolbar: Label Box Style", ToolbarStyle.TwoColumns, new string[]
            {
                "Changes the visual layout of the toolbar labels box.",
            });

            ToolbarLabelsPosition = new Vector2DSetting(Handler, "Toolbar: Labels Box Position", defaultValue: new Vector2D(-0.321, -0.721), min: new Vector2D(-1, -1), max: new Vector2D(1, 1), commentLines: new string[]
            {
                "The position (bottom-left corner pivot) of the toolbar labels on the HUD.",
                "Screen position in X and Y coordinates where 0,0 is the screen center.",
                "It can fit nicely in the bottom-left side of the HUD aswell if you don't use shields, position for that: -0.716, -0.707",
                "Positive values are right and up, while negative ones are opposite of that.",
            });

            ToolbarLabelsInMenuPosition = new Vector2DSetting(Handler, "Toolbar: Labels Box Position In-Menu", defaultValue: new Vector2D(0.128, -0.995), min: new Vector2D(-1, -1), max: new Vector2D(1, 1), commentLines: new string[]
            {
                "The position (bottom-left corner pivot) of the toolbar labels when in toolbar config menu, somewhere to the right side is recommended.",
                "Screen position in X and Y coordinates where 0,0 is the screen center.",
                "Positive values are right and up, while negative ones are opposite of that.",
            });

            ToolbarLabelsScale = new FloatSetting(Handler, "Toolbar: Labels Box Scale", defaultValue: 1.0f, min: 0.1f, max: 3f, commentLines: new string[]
            {
                "The scale of the toolbar labels box."
            });

            ToolbarLabelsOffsetForInvBar = new Vector2DSetting(Handler, "Toolbar: Labels Box Offset for InvBar", defaultValue: new Vector2D(0, 0.06), min: new Vector2D(-1, -1), max: new Vector2D(1, 1), commentLines: new string[]
            {
                "When the 'Ship Tool Inventory Bar' is visible this vector is added to the HUD position defined above." +
                "Useful when you want to place the labels box in the center over the toolbar.",
            });

            ToolbarActionStatus = new BoolSetting(Handler, "Toolbar: Improve Action Status", true,
                "Adds some statuses to some toolbar actions, overwrite some others.",
                "Few examples of what this adds: PB's Run shows 2 lines of echo, timer block shows countdown, weapons shoot once/on/off shows ammo, on/off for groups show how many are on and off, and quite a few more.",
                "This is independent of the toolbar labels feature."
            );
            ToolbarActionStatus.AddCompatibilityNames("HUD: Toolbar action status");

            TerminalDetailInfoAdditions = new BoolSetting(Handler, "Terminal: Detail Info Additions", true,
                "Adds some extra info bottom-right in terminal of certain blocks.",
                "Does not (and cannot) replace any vanilla info.");

            TextAPIScale = new FloatSetting(Handler, "TextAPI: Scale", defaultValue: 1.0f, min: 0.1f, max: 3f, commentLines: new string[]
            {
                "The overall text info panel scale."
            });

            // TODO: rename prefix on these to be Block Info or something
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

            TextAPIAlign = new TextAlignSetting(Handler, "TextAPI: Anchor", TextAlignFlags.Bottom | TextAlignFlags.Right,
                "Determine the pivot point of the text info box. Stretches in opposite direction of that.",
                $"NOTE: Requires {TextAPICustomStyling.Name} = true");
            TextAPIAlign.AddCompatibilityNames("TextAPI: Alignment");

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
                (!Constants.BLOCKPICKER_IN_MP ? Constants.BLOCKPICKER_DISABLED_CONFIG : ""));

            LockOverlayBind = new InputCombinationSetting(Handler, "Bind: Lock Overlay", Combination.Create(LOCK_OVERLAY_INPUT_NAME, "shift c.cubesizemode"),
                "When aiming at a block with a tool it locks overlays to that block so you can move around.",
                "You still have to cycle overlays (see above) in order to see them.");

            InternalInfo = new BoolSetting(Handler, "Internal Info", false,
                "Enables various info useful for server admins, PB scripters and modders.",
                "Currently it adds:",
                "- Block Type+SubType and BlockPairName in aim&place info.",
                "- Rotor angle from API in terminal info as it differs from what game already prints.",
                "- Item Type+Subtype in item's tooltip.",
                "- Piston extended position from API in terminal info in case it differs in some cases.");

            Debug = new BoolSetting(Handler, "Debug", false,
                "For debugging purposes only, not for normal use!",
                "Debug info shown for: PlacementDistance, EquipmentMonitor");

            ModVersion = new IntegerSetting(Handler, "Mod Version", 0, 0, int.MaxValue,
                "Latest version loaded for notifying you of notable changes.",
                "Do not edit!");
            ModVersion.AddDefaultValueComment = false;
            ModVersion.AddValidRangeComment = false;
        }

        IntegerSetting CreateEnumSetting<T>(string title, T defaultValue, string[] comments, Dictionary<T, string> enumComments = null)
        {
            string[] names = Enum.GetNames(typeof(T));
            int[] values = (int[])Enum.GetValues(typeof(T));

            string[] finalComments = new string[names.Length + comments.Length];

            for(int i = 0; i < comments.Length; i++)
            {
                finalComments[i] = comments[i];
            }

            for(int n = 0; n < names.Length; ++n)
            {
                int enumInt = values[n];
                int index = n + comments.Length;
                finalComments[index] = $"    {enumInt.ToString()} = {names[n]}";

                string comment;
                if(enumComments != null && enumComments.TryGetValue((T)(object)n, out comment))
                    finalComments[index] += "  (" + comment + ")";
            }

            return new IntegerSetting(Handler, title, defaultValue: (int)(object)defaultValue, min: values[0], max: values[values.Length - 1], commentLines: finalComments);
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
