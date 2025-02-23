using System;
using System.Text;
using Digi.BuildInfo.Features.ChatCommands;
using Digi.BuildInfo.Features.HUD;
using Digi.BuildInfo.Features.Tooltips;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Digi.Input;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Input;
using VRage.Library.Utils;
using VRage.ModAPI;
using VRageMath;
using static Digi.Input.InputLib;

namespace Digi.BuildInfo.Features.Config
{
    // TODO: default everything off and ask first time users if they want to configure things, then open a quick features UI to turn things on?
    public class Config : ModComponent
    {
        public readonly ConfigHandler Handler;

        public const string FileName = "config.ini";
        public const string KillswitchName = "Killswitch";
        public const int ConfigVersion = 12;

        public BoolSetting Killswitch;

        public TextShowModeSetting TextShow;
        public BoolSetting TextAlwaysVisible;

        public FlagsSetting<PlaceInfoFlags> PlaceInfo;
        public FlagsSetting<AimInfoFlags> AimInfo;

        public BoolSetting BlockInfoAdditions;
        public BoolSetting ScrollableComponentsList;
        public BoolSetting SelectAllProjectedBlocks;
        public BoolSetting OverrideToolSelectionDraw;
        public BoolSetting CubeBuilderDrawSubparts;
        public EnumSetting<CubeBuilderSelectionInfo> CubeBuilderSelectionInfoMode;
        public BoolSetting UnderCrosshairMessages;

        public BoolSetting ItemTooltipAdditions;
        public BoolSetting ItemSymbolAdditions;

        public BoolSetting BlockIconOverlays;

        public BoolSetting TurretHUD;
        public BoolSetting RelativeDampenerInfo;
        public BoolSetting SpectatorControlInfo;

        public BoolSetting BackpackBarOverride;
        public BoolSetting HealthOverride;
        public EnumSetting<MassFormat> MassOverride;
        public BoolSetting CockpitBuildHideRightHud;

        public BoolSetting HudFontOverride;

        public BoolSetting ShipToolInvBarShow;
        public Vector2DSetting ShipToolInvBarPosition;
        public Vector2DSetting ShipToolInvBarScale;

        public Vector2DSetting WeaponModeIndicatorPosition;
        public FloatSetting WeaponModeIndicatorScale;

        public EnumSetting<ToolbarLabelsMode> ToolbarLabels;
        public FloatSetting ToolbarLabelsEnterCockpitTime;
        public EnumSetting<ToolbarNameMode> ToolbarItemNameMode;
        public BoolSetting ToolbarLabelsHeader;
        public EnumSetting<ToolbarStyle> ToolbarStyleMode;
        public Vector2DSetting ToolbarLabelsPosition;
        public FloatSetting ToolbarLabelsScale;
        public Vector2DSetting ToolbarLabelsOffsetForInvBar;

        public Vector2DSetting ToolbarLabelsMenuPosition;
        public FloatSetting ToolbarLabelsMenuScale;

        public BoolSetting ToolbarActionStatus;
        public EnumSetting<ActionIconsMode> ToolbarActionIcons;
        public BoolSetting ToolbarStatusFontOverride;
        public FloatSetting ToolbarStatusTextScaleOverride;

        public BoolSetting EventToolbarInfo;
        public Vector2DSetting EventToolbarInfoPosition;
        public FloatSetting EventToolbarInfoScale;

        public BoolSetting TerminalDetailInfoAdditions;
        public BoolSetting TerminalDetailInfoHeader;
        public Vector2DSetting TerminalButtonsPosition;
        public FloatSetting TerminalButtonsScale;
        public Vector2DSetting TerminalMultiDetailedInfoPosition;

        public BoolSetting TextAPICustomStyling;
        public Vector2DSetting TextAPIScreenPosition;
        public TextAlignSetting TextAPIAlign;
        public FloatSetting TextAPIScale;
        public BackgroundOpacitySetting TextAPIBackgroundOpacity;

        public BoolSetting ScientificNotation;
        public BoolSetting TextReplace;

        public BoolSetting OverlaysAlwaysVisible;
        public FlagsSetting<OverlayLabelsFlags> OverlayLabels;
        public BoolSetting OverlaysShowLabelsWithBind;
        // TODO ^ rebindable key?
        public BoolSetting OverlayLockRememberMode;

        public BoolSetting AdjustBuildDistanceSurvival;
        public BoolSetting AdjustBuildDistanceShipCreative;

        public BoolSetting SpectatorAllowRotationModifier;

        public EnumSetting<ForceControllerMode> ForceControllerHUD;

        public InputCombinationSetting MenuBind;
        public InputCombinationSetting TextShowBind;
        public InputCombinationSetting CycleOverlaysBind;
        public InputCombinationSetting ToggleTransparencyBind;
        public InputCombinationSetting FreezePlacementBind;
        public InputCombinationSetting BlockPickerBind;
        public InputCombinationSetting LockOverlayBind;
        public InputCombinationSetting ShowToolbarInfoBind;
        public InputCombinationSetting ShowCubeBuilderSelectionInfoBind;
        public InputCombinationSetting ConstructionModelPreviewBind;

        public BoolSetting ModderHelpAlerts;
        public BoolSetting InternalInfo;

        public BoolSetting Debug;

        public IntegerSetting ModVersion;

        public const string MENU_BIND_INPUT_NAME = "bi.menu";
        public const string TEXT_SHOW_INPUT_NAME = "bi.textShow";
        public const string CYCLE_OVERLAYS_INPUT_NAME = "bi.cycleOverlays";
        public const string TOGGLE_TRANSPARENCY_INPUT_NAME = "bi.toggleTransparency";
        public const string FREEZE_PLACEMENT_INPUT_NAME = "bi.freezePlacement";
        public const string BLOCK_PICKER_INPUT_NAME = "bi.blockPicker";
        public const string LOCK_OVERLAY_INPUT_NAME = "bi.lockOverlay";
        public const string SHOW_TOOLBAR_INFO_INPUT_NAME = "bi.showToolbarInfo";
        public const string SHOW_CB_SELECTION_INFO_INPUT_NAME = "bi.showCBSelectionInfo";
        public const string CONSTRUCTION_MODEL_PREVIEW_INPUT_NAME = "bi.constructionModelPreview";

        public Config(BuildInfoMod main) : base(main)
        {
            const int ConfigSizeByteEst = 17500;

            Handler = new ConfigHandler(FileName, ConfigVersion, ConfigSizeByteEst / 2);
            Handler.SettingsLoaded += SettingsLoaded;

            InitSettings();

            Load();
            Save();

            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            Handler.SettingsLoaded -= SettingsLoaded;

            if(!Main.ComponentsRegistered)
                return;
        }

        public override void UpdateAfterSim(int tick)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

            foreach(ISetting setting in Handler.Settings.Values)
            {
                setting.TriggerValueSetEvent();
            }
        }

        void SettingsLoaded()
        {
            if(MenuBind.Value.CombinationString.Equals("c.VoxelHandSettings", StringComparison.OrdinalIgnoreCase))
            {
                MyKeys voxelHandSettingsKey = MyAPIGateway.Input.GetGameControl(MyControlsSpace.VOXEL_HAND_SETTINGS).GetKeyboardControl();
                MyKeys terminalInventoryKey = MyAPIGateway.Input.GetGameControl(MyControlsSpace.TERMINAL).GetKeyboardControl();

                if(voxelHandSettingsKey != MyKeys.None && voxelHandSettingsKey == terminalInventoryKey)
                {
                    MenuBind.ResetToDefault();

                    Log.Info("Reset MenuBind in config to default because VoxelHandSettings and Terminal/Inventory are on the same key.");
                }
            }

            ConfigVersionTweaks();
        }

        void ConfigVersionTweaks()
        {
            int cfgv = Handler.ConfigVersion.Value;
            if(cfgv <= 0 || cfgv >= ConfigVersion)
                return;

            if(cfgv == 2)
            {
                // check if existing mod users have the VoxelHandSettings key not colliding and keep using that

                IMyControl voxelHandSettingsControl = MyAPIGateway.Input.GetGameControl(MyControlsSpace.VOXEL_HAND_SETTINGS);
                IMyControl terminalInventoryControl = MyAPIGateway.Input.GetGameControl(MyControlsSpace.TERMINAL);

                // if VoxelHandSettings isn't colliding, then set the defaults like the user is used to
                if(voxelHandSettingsControl.GetKeyboardControl() != MyKeys.None && terminalInventoryControl.GetKeyboardControl() != voxelHandSettingsControl.GetKeyboardControl())
                {
                    MenuBind.SetValue(Combination.Create(MENU_BIND_INPUT_NAME, "c.VoxelHandSettings"));

                    Log.Info("NOTE: Configurable binds were added and it seems your VoxelHandSettings isn't colliding so I'm setting MenuBind to that instead so you don't need to change anything.");
                }

                return;
            }

            if(cfgv == 4)
            {
                // change default position of the labels box

                if(Vector2D.DistanceSquared(ToolbarLabelsPosition.Value, new Vector2D(-0.716, -0.707)) <= 0.001)
                {
                    ToolbarLabelsPosition.ResetToDefault();

                    Log.Info($"NOTE: Default value for '{ToolbarLabelsPosition.Name}' changed and yours was the old default, setting reset to new default.");
                }
            }

            if(cfgv <= 5)
            {
                // old defaults were in the wrong value ranges, convert values to new space.
                // e.g. 0.5, 0.84 => 0.0, -0.68

                Vector2D oldVec = ShipToolInvBarPosition.Value;
                ShipToolInvBarPosition.SetValue(new Vector2D((oldVec.X * 2) - 1, 1 - (oldVec.Y * 2)));

                Log.Info($"NOTE: Value for '{ShipToolInvBarPosition.Name}' was changed into a different space (the proper one), your setting was automatically calculated into it so no changes are necessary.");

                // old in-menu default wasn't adjusted for recent title addition
                if(Vector2D.DistanceSquared(ToolbarLabelsMenuPosition.Value, new Vector2D(0.128, -0.957)) <= 0.001)
                {
                    ToolbarLabelsMenuPosition.ResetToDefault();

                    Log.Info($"NOTE: Default value for '{ToolbarLabelsMenuPosition.Name}' changed and yours was the old default, setting reset to new default.");
                }
            }

            if(cfgv == 6)
            {
                if(ShowToolbarInfoBind.Value?.CombinationString == "alt")
                {
                    ShowToolbarInfoBind.ResetToDefault();

                    Log.Info($"NOTE: '{ShowToolbarInfoBind.Name}' is the previous default (alt), resetting to new default ({ShowToolbarInfoBind.Value}).");
                }
            }

            if(cfgv <= 8)
            {
                //if(TerminalButtonsPosition.Value == new Vector2D(0.715, -0.986))
                //{
                //    TerminalButtonsPosition.ResetToDefault();
                //
                //    Log.Info($"NOTE: '{TerminalButtonsPosition.Name}' is the previous default (0.715, -0.986), resetting to new default ({TerminalButtonsPosition.Value.X.ToString()}, {TerminalButtonsPosition.Value.Y.ToString()}).");
                //}
            }

            if(cfgv <= 9)
            {
                if(Math.Abs(ToolbarStatusTextScaleOverride.Value - 0.46f) < 0.0001f)
                {
                    ToolbarStatusTextScaleOverride.ResetToDefault();

                    Log.Info($"NOTE: '{ToolbarStatusTextScaleOverride.Name}' is the previous default (0.46), resetting to new default ({ToolbarStatusTextScaleOverride.Value.ToString()}).");
                }
            }

            if(cfgv <= 10)
            {
                if(Vector2D.DistanceSquared(TerminalButtonsPosition.Value, new Vector2D(0.731, -0.988)) < 0.0001)
                {
                    TerminalButtonsPosition.ResetToDefault();

                    Log.Info($"NOTE: '{TerminalButtonsPosition.Name}' is the previous default (0.731, -0.988), resetting to new default ({TerminalButtonsPosition.Value.X.ToString()}, {TerminalButtonsPosition.Value.Y.ToString()}).");
                }
            }

            if(cfgv <= 11)
            {
                if(MassOverride.ValueEnum == MassFormat.CustomSI)
                {
                    MassOverride.ResetToDefault();

                    Log.Info($"NOTE: '{MassOverride.Name}' is the previous default (RealCustomSuffix), resetting to new default ({MassOverride.ValueEnum.ToString()}).");
                }
            }
        }

        private void InitSettings()
        {
            Handler.HeaderComments.Add($"You can reload this while in game by typing in chat: {ChatCommandHandler.ModCommandPrefix} reload");

            StringBuilder sb = new StringBuilder(8000);
            InputLib.AppendInputBindingInstructions(sb, ConfigHandler.COMMENT_PREFIX);
            Handler.FooterComments.Add(sb.ToString());

            Killswitch = new BoolSetting(Handler, KillswitchName, false,
                "Prevents the most of the mod scripts from loading.",
                "Requires world reload/rejoin to work.",
                "NOTE: If you're testing an issue, please rejoin/reload before you try this killswitch, to ensure that rejoin/reload doesn't fix your issue before blaming my mod, thanks :P");

            const string SubHeaderFormat = "—————— {0} ————————————————————————————————————————————————————————————";

            new Comment(Handler, string.Format(SubHeaderFormat, "Text Box"));

            #region TextBox 
            TextShow = new TextShowModeSetting(Handler, "TextBox: Show Mode", TextShowMode.AlwaysOn, new string[]
            {
                "These settings affect the mod's text box that has the equipped/aimed block information.",
                "",
                "Toggle if the text box is shown or not."
            });
            TextShow.SetEnumComment(TextShowMode.ShowOnPress, $"input can be configured in 'Bind: Text Show'"); // can't use field as it's not yet assigned
            TextShow.SetEnumComment(TextShowMode.HudHints, $"shown when vanilla HUD is in most detailed mode. Includes {nameof(TextShowMode.ShowOnPress)}'s behavior.");
            TextShow.AddCompatibilityNames("Text: Show", "TextBox: Show");

            TextAlwaysVisible = new BoolSetting(Handler, "TextBox: Show when HUD is off", false,
                "If true, text box is shown in all HUD states including hidden HUD.");
            TextAlwaysVisible.AddCompatibilityNames("Text: Always Visible", "TextBox: Always Visible");

            PlaceInfo = new FlagsSetting<PlaceInfoFlags>(Handler, "TextBox: Block-Place Info Filtering", PlaceInfoFlags.All,
                "Choose what information is shown in the text box when placing a block. Disabling all of them will effectively hide the box.");
            PlaceInfo.AddCompatibilityNames("Text: Block-Place Info Filtering");

            AimInfo = new FlagsSetting<AimInfoFlags>(Handler, "TextBox: Block-Aim Info Filtering", AimInfoFlags.All,
                "Choose what information is shown in the text box when aiming at a block with a tool. Disabling all of them will effectively hide the box.");
            AimInfo.AddCompatibilityNames("Text: Block-Aim Info Filtering");

            TextAPIScale = new FloatSetting(Handler, "TextBox: Scale", defaultValue: 1.0f, min: 0.1f, max: 3f, commentLines: new string[]
            {
                "The overall scale of the box."
            });
            TextAPIScale.AddCompatibilityNames("TextAPI: Scale");

            TextAPIBackgroundOpacity = new BackgroundOpacitySetting(Handler, "TextBox: Background Opacity", BackgroundOpacitySetting.ValueHUD,
                "Set background opacity where 1.0 is fully opaque and 0.0 is fully transparent, or set to HUD to use game HUD opacity.");
            TextAPIBackgroundOpacity.AddCompatibilityNames("TextAPI: Background Opacity");

            TextAPICustomStyling = new BoolSetting(Handler, "TextBox: Custom Styling", false,
                "Enables the use of Scren Position and Alignment settings below, which allows you to place the text info box anywhere you want.",
                "If false, the text info box will be placed according to rotation hints (the cube and key hints top right).",
                "(If false) With rotation hints off, text info will be set top-left, otherwise top-right.");
            TextAPICustomStyling.AddCompatibilityNames("TextAPI: Custom Styling");

            TextAPIScreenPosition = new Vector2DSetting(Handler, "TextBox: Screen Position", defaultValue: new Vector2D(0.9692, 0.26), min: new Vector2D(-1, -1), max: new Vector2D(1, 1), commentLines: new string[]
            {
                "Screen position in X and Y coordinates where 0,0 is the screen center. Positive values are right and up, while negative ones are opposite of that.",
                $"NOTE: Requires {TextAPICustomStyling.Name} = true"
            });
            TextAPIScreenPosition.AddCompatibilityNames("TextAPI: Screen Position");

            TextAPIAlign = new TextAlignSetting(Handler, "TextBox: Anchor", TextAlignFlags.Bottom | TextAlignFlags.Right,
                "Determine the pivot point of the text box. Stretches in opposite directions.",
               $"NOTE: Requires {TextAPICustomStyling.Name} = true");
            TextAPIAlign.AddCompatibilityNames("TextAPI: Anchor", "TextAPI: Alignment");
            #endregion

            new Comment(Handler, string.Format(SubHeaderFormat, "HUD"));

            #region HUD
            BlockInfoAdditions = new BoolSetting(Handler, "HUD: Block Info Additions", true,
                "Adds various things to the vanilla block info UI:",
                "- red and blue lines in the component list to better match where the critical/ownership lines are.",
                "- shows what the component grinds to if it's different than the component itself (e.g. Battery's Power Cells), or just highlights it yellow if TextAPI is turned off.",
                "- allows scrolling of the components list if the list is too tall to fit in the current HUD mode.");
            BlockInfoAdditions.AddCompatibilityNames("HUD: Block Info Stages");

            ScrollableComponentsList = new BoolSetting(Handler, "HUD: Scrollable Block Components List", true,
                "For blocks that have more components than fit on screen in the current HUD mode.",
                "Scrolling happens automatically as you weld/grind and can also be done with shift+mousewheel.");

            SelectAllProjectedBlocks = new BoolSetting(Handler, "HUD: Tools Select All Projected Blocks", true,
                "This feature allows you to select projected blocks that are not buildable yet.",
                "HUD will show components and text info box will also show what block needs to become weldable.");

            OverrideToolSelectionDraw = new BoolSetting(Handler, "HUD: Override Tool Selection draw", true,
                "Replaces block selection with a model-shrink-wrapped-box and a bit thicker, for welder and grinder.",
                "For example, a large-grid camera block would show selection over the camera model itself, instead of the entire grid cell.",
                "CubeBuilder's block selection for paint/removal is affected, but the box around your ghost block is not (because it's inaccessible).");

            CubeBuilderDrawSubparts = new BoolSetting(Handler, "HUD: CubeBuilder Draw Subparts", true,
                "Game by default only draws the first layer of subparts when holding the block.",
                "This setting draws the other layers as well as top-part/wheel for rotor/piston/hinge/suspension.",
                "Additionally:",
                "- Suspension wheel 'collides' with world so you can see how much it can travel in the frame.",
                "- Interior turret has whacky rotations on subparts, the existing one I can't do anything about but the ones I spawn I can rotate properly.");

            CubeBuilderSelectionInfoMode = new EnumSetting<CubeBuilderSelectionInfo>(Handler, "HUD: CubeBuilder Selection Info Mode", CubeBuilderSelectionInfo.Off, new string[]
            {
                "When holding a block (CubeBuilder tool), aiming at blocks allows you to paint or remove them.",
                "This setting shows the terminal name or definition name for the aimed block.",
                $"The selection box is controlled by '{OverrideToolSelectionDraw.Name}' instead.",
            });
            CubeBuilderSelectionInfoMode.SetEnumComment(CubeBuilderSelectionInfo.ShowOnPress, $"input can be configured in 'Bind: Show CubeBuilder Selection Info'"); // can't use field as it's not yet assigned
            CubeBuilderSelectionInfoMode.SetEnumComment(CubeBuilderSelectionInfo.HudHints, $"shown when vanilla HUD is in most detailed mode. Includes {nameof(CubeBuilderSelectionInfo.ShowOnPress)}'s behavior.");

            UnderCrosshairMessages = new BoolSetting(Handler, "HUD: Under-Crosshair Messages", false,
                "An opt-in feature for moving certain easily missable messages from text box to under the crosshair.",
                $"Currently only 'Grid will split if block is removed' is affected by this, which sill requires {nameof(AimInfoFlags.GrindGridSplit)} to be enabled in '{AimInfo.Name}' setting.");

            RelativeDampenerInfo = new BoolSetting(Handler, "HUD: Relative Dampeners Info", true,
                "Shows a centered HUD message when relative dampeners are set to a target and when they're disengaged from one.",
                "Only shows if relative damps are enabled for new controlled entity (character, ship, etc).");

            SpectatorControlInfo = new BoolSetting(Handler, "HUD: Spectator Control Info", true,
                "Shows a box top-right with the current input multipliers, speed and some control hints.",
                "Only shows up when you're controlling the spectator camera. Any other spectator mode is ignored");

            TurretHUD = new BoolSetting(Handler, "HUD: Show HUD+Ammo in Turret", true,
                "Shows HUD, ammo and ship orientation while controlling a turret.");
            TurretHUD.AddCompatibilityNames("HUD: Turret Ammo", "HUD: Turret Info");

            HudFontOverride = new BoolSetting(Handler, "HUD: Font override", false,
                "Changes the font on all HUD elements to the black-outlined one from this mod. This offers better readability when bright things are behind the HUD.");

            BackpackBarOverride = new BoolSetting(Handler, "HUD: Backpack Bar Override", true,
                "This affects the vanilla inventory bar (with a backpack icon), if enabled:",
                "When in a seat, it shows the filled ratio of ship's Cargo Containers.",
                $"If a group named {BackpackBarStat.GroupName} exists, the bar will show filled ratio of all blocks there (regardless of type).");

            HealthOverride = new BoolSetting(Handler, "HUD: Health stat override", true,
                "Shows actual health instead of percentage. It also tries to maintain the number within 4 characters width by using k suffix if it makes it shorter.");

            MassOverride = new EnumSetting<MassFormat>(Handler, "HUD: Mass stat override", MassFormat.CustomKg,
                "Changes the ship mass format on the HUD.");
            MassOverride.BackwardsCompatCallback = MassOverrideBackwardsCompat;
            MassOverride.SetEnumComment(MassFormat.Vanilla, "Game's original mass formatting which does not include station mass, it does show physical mass since SE v205");
            MassOverride.SetEnumComment(MassFormat.CustomKg, "Physical mass + station mass, shown only as kg, with scientific notation past e12");
            MassOverride.SetEnumComment(MassFormat.CustomMetric, "Physical mass + station mass, formatted with metric multipliers: kg, t, kt, Mt, Gt then scientific notation past e20");
            MassOverride.SetEnumComment(MassFormat.CustomSI, "Physical mass + station mass, formatted with SI unit multipliers: kg, Mg, Gg, Tg, Pg then scientific notation past e20");

            CockpitBuildHideRightHud = new BoolSetting(Handler, "HUD: Cockpit build mode hide right panel", true,
                "Hide the bottom-right ship HUD panel when in cockpit build mode because the block info overlaps it.");

            ShipToolInvBarShow = new BoolSetting(Handler, "HUD: Ship Tool Inventory Bar", true,
                "Shows an inventory bar when a ship grinder or ship drill is selected which represents the highest filled tool inventory.");

            ShipToolInvBarPosition = new Vector2DSetting(Handler, "HUD: Ship Tool Inventory Bar Position", defaultValue: new Vector2D(0f, -0.68f), min: new Vector2D(-1, -1), max: new Vector2D(1, 1), commentLines: new string[]
            {
                "The screen position (center pivot) of the ship tool inventory bar.",
                "Screen position in X and Y coordinates where 0,0 is the screen center. Positive values are right and up, while negative ones are opposite of that.",
            });

            ShipToolInvBarScale = new Vector2DSetting(Handler, "HUD: Ship Tool Inventory Bar Scale", defaultValue: new Vector2D(1, 1), min: new Vector2D(0.1, 0.1), max: new Vector2D(3, 3), commentLines: new string[]
            {
                "The width and height scale of the ship tool inventory bar.",
            });

            WeaponModeIndicatorPosition = new Vector2DSetting(Handler, "HUD: Weapon Mode Indicator Position", defaultValue: new Vector2D(0.742708, -0.622223), min: new Vector2D(-1, -1), max: new Vector2D(1, 1), commentLines: new string[]
            {
                "Position for the weapon mode indicator on the HUD.",
                "Weapon mode cycles between shooting a single weapon or all weapons, uses the same bind as 'Repaint Block' but while in a cockpit.",
            });

            WeaponModeIndicatorScale = new FloatSetting(Handler, "HUD: Weapon Mode Indicator Scale", defaultValue: 1, min: -1f, max: 5f, commentLines: new string[]
            {
                "Scale the weapon mode indicator icon. Set to 0 or lower to remove this icon entirely.",
            });
            #endregion

            new Comment(Handler, string.Format(SubHeaderFormat, "Terminal/Inventory/GUI"));

            #region Terminal
            TerminalDetailInfoAdditions = new BoolSetting(Handler, "Terminal: Detail Info Additions", true,
                "Adds some extra info bottom-right in terminal of certain blocks.",
                "Does not (and cannot) replace any vanilla info.");

            TerminalDetailInfoHeader = new BoolSetting(Handler, "Terminal: Detail Info Header", true,
                "Adds a \"--- (BuildInfo | /bi) ---\" before this mod's detail info additions to more easily identify them.");

            TerminalButtonsPosition = new Vector2DSetting(Handler, "Terminal: Detail Info Buttons Position", new Vector2D(0.761458, -0.922222), -Vector2D.One, Vector2D.One,
                "UI position of the Refresh and Copy buttons in the terminal (top-right corner of the right-most button)",
                "Can also be moved in the menu by holding right mouse button on it.");
            TerminalButtonsPosition.AddCompatibilityNames("Terminal: Refresh Info Button Position");

            TerminalButtonsScale = new FloatSetting(Handler, "Terminal: Detail Info Buttons Scale", 1f, 0.01f, 3f,
                "Scale offset for the Refresh and Copy buttons in the terminal.");
            TerminalButtonsScale.AddCompatibilityNames("Terminal: Refresh Info Button Scale");

            TerminalMultiDetailedInfoPosition = new Vector2DSetting(Handler, "Terminal: Multi-Detailed-Info Position", new Vector2D(0.253, -0.16), -Vector2D.One, Vector2D.One,
                "This mod shows info on multiple blocks selected in terminal, this setting changes its position on the GUI.",
                "NOTE: Can also be moved ingame by aiming at the vertical line and dragging with RMB.");

            ItemTooltipAdditions = new BoolSetting(Handler, "Terminal: Item Tooltip Additions", true,
                "Adds magazine capacity, what blocks craft the item, where the item can be used, mod that added the item and whether it requires large conveyor.",
                "Does not affect 'Internal Info' setting's ability to add item id to tooltip.");
            ItemTooltipAdditions.AddCompatibilityNames("Item Tooltip Additions", "HUD: Item Tooltip Additions");

            ItemSymbolAdditions = new BoolSetting(Handler, "Terminal: Item Symbol Additions", true,
                $"Currently adds the '{ItemTooltips.ReqLargeConveyorSymbol}' on top-right of item icons, only for items that require large conveyors (includes modded items too).");
            #endregion

            #region GUI
            BlockIconOverlays = new BoolSetting(Handler, "GUI: Block Icon Overlays", true,
                $"Adds some overlays to blocks to improve identification, currently only adds a weight icon onto vanilla heavy armor blocks.");
            #endregion

            new Comment(Handler, string.Format(SubHeaderFormat, "Toolbar & ToolbarInfo box"));

            #region Toolbar
            ToolbarLabels = new EnumSetting<ToolbarLabelsMode>(Handler, "Toolbar: ToolbarInfo Mode", ToolbarLabelsMode.HudHints, new string[]
            {
                "Customize ship toolbar block action's labels.",
                "Turning this off turns off the rest of the ToolbarInfo stuff (except status override)."
            });
            ToolbarLabels.SetEnumComment(ToolbarLabelsMode.ShowOnPress, $"input can be configured in 'Bind: Show Toolbar Info'"); // can't use field as it's not yet assigned
            ToolbarLabels.SetEnumComment(ToolbarLabelsMode.HudHints, $"shown when vanilla HUD is in most detailed mode. Includes {nameof(ToolbarLabelsMode.ShowOnPress)}'s behavior.");
            ToolbarLabels.AddCompatibilityNames("Toolbar: Labels Mode");

            ToolbarLabelsEnterCockpitTime = new FloatSetting(Handler, "Toolbar: ToolbarInfo Show on Cockpit Enter", defaultValue: 3, min: 0, max: 15, commentLines: new string[]
            {
                "Show toolbar info for this many seconds upon entering a cockpit.",
                $"Only works if '{ToolbarLabels.Name}' is set to {nameof(ToolbarLabelsMode.ShowOnPress)} or {nameof(ToolbarLabelsMode.HudHints)}."
            });
            ToolbarLabelsEnterCockpitTime.AddCompatibilityNames("Toolbar: Enter Cockpit Time");

            ToolbarItemNameMode = new EnumSetting<ToolbarNameMode>(Handler, "Toolbar: ToolbarInfo Name Mode", ToolbarNameMode.AlwaysShow, new string[]
            {
                "Pick what blocks should have their custom name printed in the action label.",
                "Visibility of this is affected by the above setting."
            });
            ToolbarItemNameMode.SetEnumComment(ToolbarNameMode.InMenuOnly, "only shown when toolbar menu is open");
            ToolbarItemNameMode.SetEnumComment(ToolbarNameMode.GroupsOnly, "only block group names");
            ToolbarItemNameMode.AddCompatibilityNames("Toolbar: Item Name Mode");

            ToolbarLabelsHeader = new BoolSetting(Handler, "Toolbar: ToolbarInfo Header+Page", true,
                "Toggles if the 'Toolbar Info - Page <N>   (BuildInfo Mod)' header is shown on the box.",
                "This exists so that people can know what that box is from so they can know which mod to lookup/configure.");
            ToolbarLabelsHeader.AddCompatibilityNames("Toolbar: Toolbar Labels Show Title");

            ToolbarStyleMode = new EnumSetting<ToolbarStyle>(Handler, "Toolbar: ToolbarInfo Style", ToolbarStyle.TwoColumns, new string[]
            {
                "Changes the visual layout of the toolbar labels box.",
            });
            ToolbarStyleMode.AddCompatibilityNames("Toolbar: Label Box Style");

            ToolbarLabelsPosition = new Vector2DSetting(Handler, "Toolbar: ToolbarInfo Position", defaultValue: new Vector2D(-0.321, -0.721), min: new Vector2D(-1, -1), max: new Vector2D(1, 1), commentLines: new string[]
            {
                "The position (bottom-left corner pivot) of the toolbar labels on the HUD.",
                "Screen position in X and Y coordinates where 0,0 is the screen center.",
                "It can fit nicely in the bottom-left side of the HUD aswell if you don't use shields, position for that: -0.716, -0.707",
                "Positive values are right and up, while negative ones are opposite of that.",
            });
            ToolbarLabelsPosition.AddCompatibilityNames("Toolbar: Labels Box Position");

            ToolbarLabelsScale = new FloatSetting(Handler, "Toolbar: ToolbarInfo Scale", defaultValue: 1.0f, min: 0.1f, max: 3f, commentLines: new string[]
            {
                "The scale of the toolbar labels box."
            });
            ToolbarLabelsScale.AddCompatibilityNames("Toolbar: Labels Box Scale");

            ToolbarLabelsOffsetForInvBar = new Vector2DSetting(Handler, "Toolbar: ToolbarInfo Offset for InvBar", defaultValue: new Vector2D(0, 0.06), min: new Vector2D(-1, -1), max: new Vector2D(1, 1), commentLines: new string[]
            {
                "When the 'Ship Tool Inventory Bar' is visible this vector is added to the HUD position defined above." +
                "Useful when you want to place the labels box in the center over the toolbar.",
            });
            ToolbarLabelsOffsetForInvBar.AddCompatibilityNames("Toolbar: Labels Box Offset for InvBar");

            ToolbarLabelsMenuPosition = new Vector2DSetting(Handler, "Toolbar: ToolbarInfo In-Menu Position", defaultValue: new Vector2D(0.128, -0.995), min: new Vector2D(-1, -1), max: new Vector2D(1, 1), commentLines: new string[]
            {
                "The position (bottom-left corner pivot) of the toolbar labels when in toolbar config menu, somewhere to the right side is recommended.",
                "Screen position in X and Y coordinates where 0,0 is the screen center.",
                "Positive values are right and up, while negative ones are opposite of that.",
            });
            ToolbarLabelsMenuPosition.AddCompatibilityNames("Toolbar: Labels Box Position In-Menu");

            ToolbarLabelsMenuScale = new FloatSetting(Handler, "Toolbar: ToolbarInfo In-Menu Scale", defaultValue: 1.0f, min: 0.1f, max: 3f, commentLines: new string[]
            {
                "The scale of the toolbar info box when in toolbar config menu."
            });

            const string ToolbarStatusFontOverride_Label = "Toolbar: Action Status Font Override";

            ToolbarActionStatus = new BoolSetting(Handler, "Toolbar: Improve Action Status", true,
                "Adds some statuses to some toolbar actions, overwrite some others.",
                "Few examples of what this adds: PB's Run shows 2 lines of echo, timer block shows countdown, weapons shoot once/on/off shows ammo, on/off for groups show how many are on and off, and quite a few more.",
                "This is independent of the ToolbarInfo or EventToolbar features.",
                $"Requires '{ToolbarStatusFontOverride_Label}' to be enabled to function, because of the custom characters as icons in the changed status."
            );
            ToolbarActionStatus.AddCompatibilityNames("HUD: Toolbar action status");

            ToolbarStatusFontOverride = new BoolSetting(Handler, ToolbarStatusFontOverride_Label, true,
               $"Replaces HUD definition's font for toolbar status with a white monospace font with black outline ({HUDEditor.SetFont}), this will affect modded HUDs aswell.",
               $"This is required for '{ToolbarActionStatus.Name}' to work properly, aside from greatly helping readability of text on the blueish block icons in the toolbar."
            );

            ToolbarStatusTextScaleOverride = new FloatSetting(Handler, "Toolbar: Action Status Text Scale Override", defaultValue: HUDEditor.CustomToolbarTextScale, min: 0, max: 2f, commentLines: new string[]
            {
                "Overrides the HUD definition's toolbar status text scale because custom status overrides rely on a consistent size to not bug out.",
               $"Not recommended to go higher than {HUDEditor.VanillaToolbarTextScale.ToString("0.######")} because full wide status text will vanish if it's longer than the box.",
                "Set to 0 to not edit the HUD definition at all (not recommended)."
            });

            ToolbarActionIcons = new EnumSetting<ActionIconsMode>(Handler, "Toolbar: Action Icons", ActionIconsMode.Custom, new string[]
            {
                "The blocks in toolbars with actions (on/off, run, etc) have little icons top-right of the block icon to indicate the icon.",
                "These icons are often very generic and sometimes misleading, this setting aims to fix that to better recognize different actions.",
                "NOTE: changing this during gameplay does not refresh them right away, a world rejoin would be required for that.",
                "This is independent of the ToolbarInfo or EventToolbar features."
            });
            ToolbarActionIcons.SetEnumComment(ActionIconsMode.Custom, "override with new distinct icons.");
            ToolbarActionIcons.SetEnumComment(ActionIconsMode.Hidden, "hides all action icons.");
            ToolbarActionIcons.SetEnumComment(ActionIconsMode.Original, "doesn't touch the action icons.");

            EventToolbarInfo = new BoolSetting(Handler, "Toolbar: EventToolbarInfo", true,
                "Blocks that have an event toolbar (like sensors, airvents, etc) don't have any representation on what the slots do.",
                "This setting shows a small box on the left of those slots to inform you of what they do.",
                "It also tells you what button you aimed at when you opened its toolbar config menu, as well as show the button panel's overlays in the background.");

            EventToolbarInfoPosition = new Vector2DSetting(Handler, "Toolbar: EventToolbarInfo Position", defaultValue: new Vector2D(-0.50625, -0.741111), min: new Vector2D(-1, -1), max: new Vector2D(1, 1), commentLines: new string[]
            {
                "The position (bottom-right corner pivot) of the event toolbar information, somewhere on the left of the toolbar is recommended.",
                "Screen position in X and Y coordinates where 0,0 is the screen center.",
                "Positive values are right and up, while negative ones are opposite of that.",
            });

            EventToolbarInfoScale = new FloatSetting(Handler, "Toolbar: EventToolbarInfo Scale", defaultValue: 1.0f, min: 0.1f, max: 3f, commentLines: new string[]
            {
                "The scale of the event toolbar info box."
            });
            #endregion

            new Comment(Handler, string.Format(SubHeaderFormat, "Block Overlays"));

            #region Overlays
            OverlaysAlwaysVisible = new BoolSetting(Handler, "Overlays: Show when HUD is off", false,
                "Setting to true causes the block overlays to be visible regardless of HUD state.");
            OverlaysAlwaysVisible.AddCompatibilityNames("Overlays: Always Visible");

            OverlayLabels = new FlagsSetting<OverlayLabelsFlags>(Handler, "Overlay: Labels", OverlayLabelsFlags.All,
                "Pick what labels can be shown for overlays.",
                "Not yet fully expanded to include detailed settings, just axes and eveything else for now.");

            IMyControl lookaroundControl = MyAPIGateway.Input.GetGameControl(MyControlsSpace.LOOKAROUND);
            string lookaroundBind = "(unbound)";
            if(lookaroundControl.GetKeyboardControl() != MyKeys.None)
                lookaroundBind = MyAPIGateway.Input.GetKeyName(lookaroundControl.GetKeyboardControl());
            else if(lookaroundControl.GetSecondKeyboardControl() != MyKeys.None)
                lookaroundBind = MyAPIGateway.Input.GetKeyName(lookaroundControl.GetSecondKeyboardControl());

            OverlaysShowLabelsWithBind = new BoolSetting(Handler, "Overlays: Show Labels with Look-around key", true,
                $"Turning off labels above and having this setting on allows you to see labels when holding the look-around bind (last seen bound to: {lookaroundBind}).");
            OverlaysShowLabelsWithBind.AddCompatibilityNames("Overlays: Show Labels with Alt key");

            OverlayLockRememberMode = new BoolSetting(Handler, "Overlay Lock: Remember mode", false,
                "Setting to true will make locked overlays remain in the mode they were locked-on as.");
            #endregion

            new Comment(Handler, string.Format(SubHeaderFormat, "Air Leak Detector"));

            new Comment(Handler, string.Format(SubHeaderFormat, "Key/button binds"));

            #region Binds
            MenuBind = new InputCombinationSetting(Handler, "Bind: Menu", Combination.Create(MENU_BIND_INPUT_NAME, "plus"),
                "For accessing the quick menu.");

            TextShowBind = new InputCombinationSetting(Handler, "Bind: Text Show", Combination.Create(TEXT_SHOW_INPUT_NAME, "c.lookaround"),
                $"Shows block text info while holding a block or aiming at one with welder/grinder.",
                $"Only works if '{TextShow.Name}' is set to {nameof(TextShowMode.ShowOnPress)} or {nameof(TextShowMode.HudHints)}.");

            CycleOverlaysBind = new InputCombinationSetting(Handler, "Bind: Cycle Overlays", Combination.Create(CYCLE_OVERLAYS_INPUT_NAME, "ctrl " + MENU_BIND_INPUT_NAME),
                $"For cycling through block overlays ({string.Join(", ", Overlays.Overlays.OverlayNames)}).");

            ToggleTransparencyBind = new InputCombinationSetting(Handler, "Bind: Toggle Transparency", Combination.Create(TOGGLE_TRANSPARENCY_INPUT_NAME, "shift " + MENU_BIND_INPUT_NAME),
                "For toggling block transparency when equipped.");

            FreezePlacementBind = new InputCombinationSetting(Handler, "Bind: Freeze Placement", Combination.Create(FREEZE_PLACEMENT_INPUT_NAME, "alt " + MENU_BIND_INPUT_NAME),
                "For locking the block ghost in place.");

            BlockPickerBind = new InputCombinationSetting(Handler, "Bind: Block Picker", Combination.Create(BLOCK_PICKER_INPUT_NAME, "ctrl c.cubesizemode"),
                "The bind for adding the aimed block to the toolbar.",
                "NOTE: It does request a number press afterwards.");

            LockOverlayBind = new InputCombinationSetting(Handler, "Bind: Lock Overlay", Combination.Create(LOCK_OVERLAY_INPUT_NAME, "shift c.cubesizemode"),
                "When aiming at a block with a tool it locks overlays to that block so you can move around.",
                "You still have to cycle overlays (see above) in order to see them.");

            ShowToolbarInfoBind = new InputCombinationSetting(Handler, "Bind: Show Toolbar Info", Combination.Create(SHOW_TOOLBAR_INFO_INPUT_NAME, "c.lookaround"),
                $"Shows ToolbarInfo while held when in a cockpit.",
                $"Only works if '{ToolbarLabels.Name}' is set to {nameof(ToolbarLabelsMode.ShowOnPress)} or {nameof(ToolbarLabelsMode.HudHints)}.");

            ShowCubeBuilderSelectionInfoBind = new InputCombinationSetting(Handler, "Bind: Show CubeBuilder Selection Info", Combination.Create(SHOW_CB_SELECTION_INFO_INPUT_NAME, "shift"),
                $"When holding a cube and aiming at a block, holding this bind would show what block you're selecting for removal/paint.",
                $"Only works if '{CubeBuilderSelectionInfoMode.Name}' is set to {nameof(CubeBuilderSelectionInfo.ShowOnPress)} or {nameof(CubeBuilderSelectionInfo.HudHints)}.");

            ConstructionModelPreviewBind = new InputCombinationSetting(Handler, "Bind: Construction Model Preview", Combination.Create(CONSTRUCTION_MODEL_PREVIEW_INPUT_NAME, "alt c.cubesizemode"),
                "When holding a cube you can see all its construction models using this bind.");
            #endregion

            new Comment(Handler, string.Format(SubHeaderFormat, "Misc."));

            #region Misc
            AdjustBuildDistanceSurvival = new BoolSetting(Handler, "Adjust Build Distance Survival", true,
                "Enable ctrl+scroll to change block placement distance in survival (for both character and ship)");

            AdjustBuildDistanceShipCreative = new BoolSetting(Handler, "Adjust Build Distance Ship Creative", true,
                "Enable ctrl+scroll to change block placement distance when in cockpit build mode in creative.",
                "The game currently doesn't allow this and it might get it fixed, that's why this exist as a separate setting.");

            SpectatorAllowRotationModifier = new BoolSetting(Handler, "Spectator: Allow Rotation Modifier", false,
                "Whether to allow ctrl+scroll to adjust view sensitivity in spectator (game feature).",
                "Mind that ctrl is used as a modifier for many other things, making it very easy to accidentally change this, which is why this setting exists to block it.");

            ForceControllerHUD = new EnumSetting<ForceControllerMode>(Handler, "Force Controller HUD", ForceControllerMode.Off,
                "Normally the HUD automatically changes depending on the last used input type (kb/m vs gamepad).",
                "This setting allows you to force it to one of the HUD types if you're using a mix of both.");

            ScientificNotation = new BoolSetting(Handler, "Scientific Notation", false,
                "Enabling this will make most numbers provided by this mod turn into scientific notation for larger than 1e4 or lower than 1e-4.",
                "With this off, scientific notation will still be used for numbers past 1e12 and some past 1e18.");

            TextReplace = new BoolSetting(Handler, "Text Replace", true,
                "The mod replaces some texts around the game for better context.",
                "Currently it only affects Event Controller's Event names to be more consistent and clear.",
                "This feature will not do anything if the game language is set to anything but English.");

            ModderHelpAlerts = new BoolSetting(Handler, "ModderHelp: Show problems/hints", true,
                "Toggles wether this mod looks at definitions from local mods for sneaky problems or hints." +
                "Also if any mod has compile errors it shows F11 menu in offline mode or shows a chat message in non-offline worlds.");

            InternalInfo = new BoolSetting(Handler, "Internal Info", false,
                "Enables various info useful for server admins, PB scripters and modders.",
                "Currently it adds:",
                "- In block overlay: ModelOffset as pink dot+line coming out of the BB center, only if it's not 0,0,0.",
                "- In block aim&place info: Type+SubType, BlockPairName, ModelIntersection, ModelOffset (place only and only if not 0,0,0).",
                "- In block description: Type+Subtype.",
                "- In terminal info: Piston extended position, Rotor angle - because they differ in API compared to what game prints there already.",
                "- In item's tooltip: Type+Subtype.",
                "- In blueprint's tooltip: Type+Subtype.",
                "- In /bi serverinfo: Setting name for sandbox_config.sbc (in tooltips).",
                "- In info.log: verbose API usage (other mods using this mod's API).");

            Debug = new BoolSetting(Handler, "Debug", false,
                "For debugging purposes only, not for normal use!",
                "Debug info shown for: PlacementDistance, EquipmentMonitor");
            #endregion

            ModVersion = new IntegerSetting(Handler, "Mod Version", Constants.ModVersion, 0, int.MaxValue,
                "Latest version loaded for notifying you of notable changes.",
                "Do not edit!");
            ModVersion.AddDefaultValueComment = false;
            ModVersion.AddValidRangeComment = false;
        }

        MassFormat? MassOverrideBackwardsCompat(string valueString)
        {
            valueString = valueString.Trim();
            MassFormat? result = null;

            if(valueString.Equals("RealKg", StringComparison.OrdinalIgnoreCase))
            {
                result = MassFormat.CustomKg;
            }
            else if(valueString.Equals("RealCustomSuffix", StringComparison.OrdinalIgnoreCase))
            {
                result = MassFormat.CustomSI;
            }

            if(result != null)
                Log.Info($"[Config] Converted '{MassOverride.Name}' value '{valueString}' to the new '{MyEnum<MassFormat>.GetName(result.Value)}'");

            return result;
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
