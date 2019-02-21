using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Digi.Input;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Input;
using VRageMath;

namespace Digi.BuildInfo
{
    public class Settings : IDisposable
    {
        public InputLib.Combination MenuBind;
        public InputLib.Combination CycleOverlaysBind;
        public InputLib.Combination ToggleTransparencyBind;
        public InputLib.Combination FreezePlacementBind;
        public InputLib.Combination BlockPickerBind;
        public bool showTextInfo;
        public bool alwaysVisible;
        public bool textAPIUseCustomStyling;
        public Vector2D textAPIScreenPos;
        public bool textAPIAlignRight;
        public bool textAPIAlignBottom;
        public float textAPIScale;
        public float textAPIBackgroundOpacity;
        public bool allLabels;
        public bool axisLabels;
        public bool adjustBuildDistance;
        public bool debug;
        public int configVersion;

        public readonly InputLib.Combination default_menuBind = InputLib.Combination.Create("plus");
        public readonly InputLib.Combination default_cycleOverlaysBind = InputLib.Combination.Create($"ctrl {MENU_BIND_INPUT_NAME}");
        public readonly InputLib.Combination default_toggleTransparencyBind = InputLib.Combination.Create($"shift {MENU_BIND_INPUT_NAME}");
        public readonly InputLib.Combination default_freezePlacementBind = InputLib.Combination.Create($"alt {MENU_BIND_INPUT_NAME}");
        public readonly InputLib.Combination default_blockPickerBind = InputLib.Combination.Create("ctrl c.cubesizemode");

        public const string MENU_BIND_INPUT_NAME = "bi.menu";

        public const bool default_showTextInfo = true;
        public const bool default_alwaysVisible = false;
        public const bool default_textAPIUseCustomStyling = false;
        public readonly Vector2D default_textAPIScreenPos = new Vector2D(-0.9825, 0.8);
        public const bool default_textAPIAlignRight = false;
        public const bool default_textAPIAlignBottom = false;
        public const float default_textAPIScale = 1f;
        public const float default_textAPIBackgroundOpacity = -1f;
        public const bool default_allLabels = true;
        public const bool default_axisLabels = true;
        public const bool default_adjustBuildDistance = true;
        public const bool default_debug = false;

        public const int CFGVERSION_BAD_DEFAULTS = 0;
        public const int CFGVERSION_MENU_BIND_CHANGE = 1;
        public const int LATEST_CONFIG_VERSION = 2; // controls reset/ignore of settings

        private readonly List<string> menuBindInvalidInputs = new List<string>() { MENU_BIND_INPUT_NAME };

        private void SetDefaults()
        {
            MenuBind = default_menuBind;
            CycleOverlaysBind = default_cycleOverlaysBind;
            ToggleTransparencyBind = default_toggleTransparencyBind;
            FreezePlacementBind = default_freezePlacementBind;
            BlockPickerBind = default_blockPickerBind;

            showTextInfo = default_showTextInfo;
            alwaysVisible = default_alwaysVisible;
            textAPIUseCustomStyling = default_textAPIUseCustomStyling;
            textAPIScreenPos = default_textAPIScreenPos;
            textAPIAlignRight = default_textAPIAlignRight;
            textAPIAlignBottom = default_textAPIAlignBottom;
            textAPIScale = default_textAPIScale;
            textAPIBackgroundOpacity = default_textAPIBackgroundOpacity;
            allLabels = default_allLabels;
            axisLabels = default_axisLabels;
            adjustBuildDistance = default_adjustBuildDistance;
            debug = default_debug;
            // don't reset configVersion, only read
        }

        private const string COMMAND = "/buildinfo reload";
        private const string FILE = "settings.cfg";
        private readonly char[] CHARS = { '=' };

        public Settings()
        {
            // load the settings if they exist
            Load();

            // because of an issue with new configs being created with wrong default values, they need to be wiped :(
            if(Math.Abs(textAPIScale) < 0.0001f)
            {
                if(configVersion == CFGVERSION_BAD_DEFAULTS)
                {
                    Log.Info($"NOTE: Config version is {configVersion} and scale is 0: the mod will reset all settings due to a default config generation issue.");
                    SetDefaults();
                }
                else
                {
                    textAPIScale = MathHelper.Clamp(textAPIScale, 0.0001f, 10f);
                }
            }

            // check if existing mod users have the VoxelHandSettings key not colliding and keep using that
            if(configVersion == CFGVERSION_MENU_BIND_CHANGE)
            {
                var voxelHandSettingsControl = MyAPIGateway.Input.GetGameControl(MyControlsSpace.VOXEL_HAND_SETTINGS);
                var terminalInventoryControl = MyAPIGateway.Input.GetGameControl(MyControlsSpace.TERMINAL);

                // if VoxelHandSettings isn't colliding, then set the defaults like the user is used to
                if(voxelHandSettingsControl.GetKeyboardControl() != MyKeys.None && terminalInventoryControl.GetKeyboardControl() != voxelHandSettingsControl.GetKeyboardControl())
                {
                    MenuBind = InputLib.Combination.Create("c.VoxelHandSettings");

                    Log.Info("NOTE: Configurable binds were added and it seems your VoxelHandSettings isn't colliding so I'm setting MenuBind to that instead so you don't need to change anything.");
                }
            }

            configVersion = LATEST_CONFIG_VERSION;

            Save(); // refresh config in case of any missing or extra settings
        }

        public void Dispose()
        {
        }

        public bool Load()
        {
            try
            {
                SetDefaults();

                if(MyAPIGateway.Utilities.FileExistsInLocalStorage(FILE, typeof(Settings)))
                {
                    using(var file = MyAPIGateway.Utilities.ReadFileInLocalStorage(FILE, typeof(Settings)))
                    {
                        ReadSettings(file);
                    }

                    return true;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            return false;
        }

        private void ReadSettings(TextReader file)
        {
            try
            {
                const StringComparison COMPARE_TYPE = StringComparison.InvariantCultureIgnoreCase;
                string line;
                string[] args;
                int i;
                bool b;
                float f;

                while((line = file.ReadLine()) != null)
                {
                    if(line.Length == 0)
                        continue;

                    var index = line.IndexOf("//");

                    if(index > -1)
                        line = (index == 0 ? "" : line.Substring(0, index));

                    if(line.Length == 0)
                        continue;

                    args = line.Split(CHARS, 2);

                    if(args.Length != 2)
                    {
                        Log.Error($"Unknown {FILE} line: {line}\nMaybe is missing the '=' ?");
                        continue;
                    }

                    var key = args[0].Trim();
                    var val = args[1].Trim();

                    if(key.Equals("MenuBind", COMPARE_TYPE))
                    {
                        string error;
                        var input = InputLib.Combination.Create(val, out error, menuBindInvalidInputs);

                        if(input != null)
                            MenuBind = input;
                        else
                            Log.Error(error);

                        continue;
                    }

                    if(key.Equals("CycleOverlaysBind", COMPARE_TYPE))
                    {
                        string error;
                        var input = InputLib.Combination.Create(val, out error);

                        if(input != null)
                            CycleOverlaysBind = input;
                        else
                            Log.Error(error);

                        continue;
                    }

                    if(key.Equals("ToggleTransparencyBind", COMPARE_TYPE))
                    {
                        string error;
                        var input = InputLib.Combination.Create(val, out error);

                        if(input != null)
                            ToggleTransparencyBind = input;
                        else
                            Log.Error(error);

                        continue;
                    }

                    if(key.Equals("FreezePlacementBind", COMPARE_TYPE))
                    {
                        string error;
                        var input = InputLib.Combination.Create(val, out error);

                        if(input != null)
                            FreezePlacementBind = input;
                        else
                            Log.Error(error);

                        continue;
                    }

                    if(key.Equals("BlockPickerBind", COMPARE_TYPE))
                    {
                        string error;
                        var input = InputLib.Combination.Create(val, out error);

                        if(input != null)
                            BlockPickerBind = input;
                        else
                            Log.Error(error);

                        continue;
                    }

                    if(key.Equals("ShowTextInfo", COMPARE_TYPE))
                    {
                        if(bool.TryParse(val, out b))
                            showTextInfo = b;
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("AlwaysVisible", COMPARE_TYPE))
                    {
                        if(bool.TryParse(val, out b))
                            alwaysVisible = b;
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("UseCustomStyling", COMPARE_TYPE)
                    || key.Equals("UseScreenPos", COMPARE_TYPE)) // backwards compatibility
                    {
                        if(bool.TryParse(val, out b))
                            textAPIUseCustomStyling = b;
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("ScreenPos", COMPARE_TYPE))
                    {
                        var vars = val.Split(',');
                        double x, y;

                        if(vars.Length == 2 && double.TryParse(vars[0], out x) && double.TryParse(vars[1], out y))
                            textAPIScreenPos = new Vector2D(x, y);
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("Alignment", COMPARE_TYPE))
                    {
                        var vars = args[1].Split(',');

                        if(vars.Length == 2)
                        {
                            var x = vars[0].Trim();
                            var y = vars[1].Trim();

                            var isLeft = (x == "left");
                            var isTop = (y == "top");

                            if((isLeft || x == "right") && (isTop || y == "bottom"))
                            {
                                textAPIAlignRight = !isLeft;
                                textAPIAlignBottom = !isTop;
                                continue;
                            }
                        }

                        Log.Error("Invalid " + args[0] + " value: " + args[1] + ". Expected left or right, a comma, then top or bottom (e.g.: right, top)");

                        continue;
                    }

                    if(key.Equals("Scale", COMPARE_TYPE))
                    {
                        if(float.TryParse(val, out f))
                            textAPIScale = MathHelper.Clamp(f, 0f, 10f);
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("BackgroundOpacity", COMPARE_TYPE))
                    {
                        if(val.Trim().Equals("HUD", COMPARE_TYPE))
                            textAPIBackgroundOpacity = -1;
                        else if(float.TryParse(val, out f))
                            textAPIBackgroundOpacity = MathHelper.Clamp(f, 0, 1);
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("AllLabels", COMPARE_TYPE))
                    {
                        if(bool.TryParse(val, out b))
                            allLabels = b;
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("AxisLabels", COMPARE_TYPE))
                    {
                        if(bool.TryParse(val, out b))
                            axisLabels = b;
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("AdjustBuildDistance", COMPARE_TYPE))
                    {
                        if(bool.TryParse(val, out b))
                            adjustBuildDistance = b;
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("Debug", COMPARE_TYPE))
                    {
                        if(bool.TryParse(val, out b))
                            debug = b;
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("ConfigVersion", COMPARE_TYPE))
                    {
                        if(int.TryParse(val, out i))
                            configVersion = i;
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }
                }

                Log.Info("Loaded settings:\n" + GetSettingsString(false));
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public void Save()
        {
            try
            {
                var text = GetSettingsString(true);

                using(var file = MyAPIGateway.Utilities.WriteFileInLocalStorage(FILE, typeof(Settings)))
                {
                    file.Write(text);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public string GetSettingsString(bool comments)
        {
            var str = new StringBuilder();

            if(comments)
            {
                str.Append("// ").Append(Log.ModName).Append(" mod config.").AppendLine();
                str.Append("// Lines starting with // are comments. All values are case and space insensitive unless otherwise specified.").AppendLine();
                str.Append("// You can reload this while the game is running, by typing in chat: ").Append(COMMAND).AppendLine();
                str.Append("// NOTE: This file gets automatically overwritten after being loaded, only edit the values.").AppendLine();
                str.AppendLine();
                str.AppendLine();
            }

            if(comments)
            {
                str.AppendLine();
                str.Append("// Whether to show the build info when having a block equipped.").AppendLine();
                str.Append("// This can be chaned in-game in the menu as well.").AppendLine();
                str.Append("// Default: ").Append(default_showTextInfo ? "true" : "false").AppendLine();
            }
            str.Append("ShowTextInfo = ").Append(showTextInfo ? "true" : "false").AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// Determines if game HUD hidden state is used for hiding this mod's elements.").AppendLine();
                str.Append("// Set to true to draw info regardless of HUD visible state.").AppendLine();
                str.Append("// Default: ").Append(default_alwaysVisible ? "true" : "false").AppendLine();
            }
            str.Append("AlwaysVisible = ").Append(alwaysVisible ? "true" : "false").AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// The key/button to use for accessing the buildinfo menu.").AppendLine();
                str.Append("// (see inputs and instructions at the bottom of this file)").AppendLine();
                str.Append("// Default: ").Append(default_menuBind.CombinationString).AppendLine();
            }
            str.Append("MenuBind = ").Append(MenuBind.CombinationString).AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// The bind for cycling through block info overlays (").Append(string.Join(", ", BuildInfo.Instance.DRAW_OVERLAY_NAME)).Append(").").AppendLine();
                str.Append("// (see inputs and instructions at the bottom of this file)").AppendLine();
                str.Append("// Default: ").Append(default_cycleOverlaysBind.CombinationString).AppendLine();
            }
            str.Append("CycleOverlaysBind = ").Append(CycleOverlaysBind.CombinationString).AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// The bind for toggling block transparency when equipped.").AppendLine();
                str.Append("// (see inputs and instructions at the bottom of this file)").AppendLine();
                str.Append("// Default: ").Append(default_toggleTransparencyBind.CombinationString).AppendLine();
            }
            str.Append("ToggleTransparencyBind = ").Append(ToggleTransparencyBind.CombinationString).AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// The key/button to use for accessing the menu and key combinations.").AppendLine();
                str.Append("// (see inputs and instructions at the bottom of this file)").AppendLine();
                str.Append("// Default: ").Append(default_freezePlacementBind.CombinationString).AppendLine();
            }
            str.Append("FreezePlacementBind = ").Append(FreezePlacementBind.CombinationString).AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// The bind for adding the aimed block to the toolbar. Note: it does request a number press afterwards.").AppendLine();
                str.Append("// (see inputs and instructions at the bottom of this file)").AppendLine();
                str.Append("// Default: ").Append(default_blockPickerBind.CombinationString).AppendLine();
            }
            str.Append("BlockPickerBind = ").Append(BlockPickerBind.CombinationString).AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// All these settings control the visuals of the info box that gets created with TextAPI.").AppendLine();
                str.AppendLine();
                str.Append("// Enables the use of ScrenPos and Alignment settings which allows you to place the text info box anywhere you want.").AppendLine();
                str.Append("// If false, the text info box will be placed according to rotation hints (the cube and key hints top right).").AppendLine();
                str.Append("// (If false) With rotation hints off, text info will be set top-left, otherwise top-right.").AppendLine();
                str.Append("// Default: ").Append(default_textAPIUseCustomStyling ? "true" : "false").AppendLine();
            }
            str.Append("UseCustomStyling = ").Append(textAPIUseCustomStyling ? "true" : "false").AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// Screen position in X and Y coordinates where 0,0 is the screen center.").AppendLine();
                str.Append("// Positive values are right and up, while negative ones are opposite of that.").AppendLine();
                str.Append("// NOTE: UseCustomStyling needs to be true for this to be used!").AppendLine();
                str.Append("// Default: ").Append(default_textAPIScreenPos.X).Append(", ").Append(default_textAPIScreenPos.Y).AppendLine();
            }
            str.Append("ScreenPos = ").Append(Math.Round(textAPIScreenPos.X, 5)).Append(", ").Append(Math.Round(textAPIScreenPos.Y, 5)).AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// Determines which corner is the screen position going to use and which way the text box gets resized towards.").AppendLine();
                str.Append("// For example, using \"right, bottom\" will make the bottom-right corner be placed at the specified position and").AppendLine();
                str.Append("//   any resizing required will be done on the opposide sides (left and top).").AppendLine();
                str.Append("// NOTE: UseCustomStyling needs to be true for this to be used!").AppendLine();
                str.Append("// Default: ").Append(default_textAPIAlignRight ? "right" : "left").Append(", ").Append(default_textAPIAlignBottom ? "bottom" : "top").AppendLine();
            }
            str.Append("Alignment = ").Append(textAPIAlignRight ? "right" : "left").Append(", ").Append(textAPIAlignBottom ? "bottom" : "top").AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// The overall text info scale. Works regardless of UseCustomStyling's value.").AppendLine();
                str.Append("// Minimum value is 0.0001, max 10. Default: ").AppendFormat("{0:0.0#####}", default_textAPIScale).AppendLine();
            }
            str.Append("Scale = ").AppendFormat("{0:0.0#####}", textAPIScale).AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// Text info's background opacity percent scale (0.0 to 1.0 value) or the word HUD.").AppendLine();
                str.Append("// The HUD value will use the game's UI background opacity.").AppendLine();
                str.Append("// Default: ").Append(default_textAPIBackgroundOpacity < 0 ? "HUD" : default_textAPIBackgroundOpacity.ToString()).AppendLine();
            }
            str.Append("BackgroundOpacity = ").Append(textAPIBackgroundOpacity < 0 ? "HUD" : Math.Round(textAPIBackgroundOpacity, 5).ToString()).AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// Using textAPI and block volumes view mode shows labels on some things.").AppendLine();
                str.Append("// You can toggle those labels here.").AppendLine();
                str.Append("// Everything is default true").AppendLine();
            }
            str.Append("AllLabels = ").Append(allLabels ? "true" : "false").Append(comments ? "  // a single toggle for all of them, if this is false then the values below are ignored" : "").AppendLine();
            str.Append("AxisLabels = ").Append(axisLabels ? "true" : "false").Append(comments ? "  // axes are colored in X/Y/Z = R/G/B, labels aren't really needed" : "").AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// Survival build ghost can be adjusted with Ctrl+Scroll, you can turn it off here if you don't want it (or it has issues).").AppendLine();
                str.Append("// Default: ").Append(default_adjustBuildDistance ? "true" : "false").AppendLine();
            }
            str.Append("AdjustBuildDistance = ").Append(adjustBuildDistance ? "true" : "false").AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.Append("// Enable/disable debug logging/printing, various stuff written to log/screen for debugging.").AppendLine();
                str.Append("// Currently it only logs values of build distance.").AppendLine();
                str.Append("// Not for regular use.").AppendLine();
                str.Append("// Default: ").Append(default_debug ? "true" : "false").AppendLine();
            }
            str.Append("Debug = ").Append(debug ? "true" : "false").AppendLine();

            if(comments)
            {
                str.AppendLine();
                str.AppendLine();
                str.AppendLine();
                InputLib.AppendInputBindingInstructions(str);
                str.AppendLine();
            }

            if(comments)
            {
                str.AppendLine();
                str.AppendLine();
                str.AppendLine();
                str.Append("// Config version; should not be edited.").AppendLine();
            }
            str.Append("ConfigVersion = ").Append(configVersion).AppendLine();

            return str.ToString();
        }
    }
}