using System;
using System.IO;
using System.Text;
using Sandbox.ModAPI;
using VRageMath;

namespace Digi.BuildInfo
{
    public class Settings
    {
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
        public int configVersion;

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

        public const int LATEST_CONFIG_VERSION = 1; // controls reset/ignore of settings

        private void SetDefaults()
        {
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
            // don't reset configVersion, only read
        }

        public bool firstLoad = false;

        private const string COMMAND = "/buildinfo reload";
        private const string FILE = "settings.cfg";
        private readonly char[] CHARS = new char[] { '=' };

        public Settings()
        {
            // load the settings if they exist
            if(!Load())
            {
                firstLoad = true; // config didn't exist, assume it's the first time the mod is loaded
            }

            // HACK because of an issue with new configs being created with wrong default values, they need to be wiped :(
            if(Math.Abs(textAPIScale) < 0.0001f)
            {
                if(configVersion == 0)
                {
                    Log.Info("NOTE: Config version 0 and scale 0 -> the mod will reset all settings due to a default config generation issue.");
                    SetDefaults();
                }
                else
                {
                    textAPIScale = MathHelper.Clamp(textAPIScale, 0.0001f, 10f);
                }
            }

            configVersion = LATEST_CONFIG_VERSION;

            Save(); // refresh config in case of any missing or extra settings
        }

        public void Close()
        {
        }

        public bool Load()
        {
            try
            {
                SetDefaults();

                if(MyAPIGateway.Utilities.FileExistsInLocalStorage(FILE, typeof(Settings)))
                {
                    var file = MyAPIGateway.Utilities.ReadFileInLocalStorage(FILE, typeof(Settings));
                    ReadSettings(file);
                    file.Close();
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
                string line;
                string[] args;
                int i;
                bool b;
                float f;

                while((line = file.ReadLine()) != null)
                {
                    if(line.Length == 0)
                        continue;

                    var index = line.IndexOf("//", StringComparison.Ordinal);

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

                    if(key.Equals("ShowTextInfo", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if(bool.TryParse(val, out b))
                            showTextInfo = b;
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("AlwaysVisible", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if(bool.TryParse(val, out b))
                            alwaysVisible = b;
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("UseCustomStyling", StringComparison.CurrentCultureIgnoreCase)
                    || key.Equals("UseScreenPos", StringComparison.CurrentCultureIgnoreCase)) // backwards compatibility
                    {
                        if(bool.TryParse(val, out b))
                            textAPIUseCustomStyling = b;
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("ScreenPos", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var vars = val.Split(',');
                        double x, y;

                        if(vars.Length == 2 && double.TryParse(vars[0], out x) && double.TryParse(vars[1], out y))
                            textAPIScreenPos = new Vector2D(x, y);
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("Alignment", StringComparison.CurrentCultureIgnoreCase))
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

                    if(key.Equals("Scale", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if(float.TryParse(val, out f))
                            textAPIScale = MathHelper.Clamp(f, 0f, 10f);
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("BackgroundOpacity", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if(val.Trim().Equals("HUD", StringComparison.CurrentCultureIgnoreCase))
                            textAPIBackgroundOpacity = -1;
                        else if(float.TryParse(val, out f))
                            textAPIBackgroundOpacity = MathHelper.Clamp(f, 0, 1);
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("AllLabels", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if(bool.TryParse(val, out b))
                            allLabels = b;
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("AxisLabels", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if(bool.TryParse(val, out b))
                            axisLabels = b;
                        else
                            Log.Error($"Invalid {key} value: {val}");

                        continue;
                    }

                    if(key.Equals("ConfigVersion", StringComparison.CurrentCultureIgnoreCase))
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
                var file = MyAPIGateway.Utilities.WriteFileInLocalStorage(FILE, typeof(Settings));
                file.Write(GetSettingsString(true));
                file.Flush();
                file.Close();
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
                str.Append("// ").Append(Log.modName).Append(" mod config; this file gets automatically overwritten after being loaded so don't leave custom comments.").AppendLine();
                str.Append("// You can reload this while the game is running by typing in chat: ").Append(COMMAND).AppendLine();
                str.Append("// If this file doesn't exist, or one or all settings are missing, they will be reset to default when loaded or reloaded with the command.").AppendLine();
                str.Append("// Lines starting with // are comments. All values are case and space insensitive unless otherwise specified.").AppendLine();
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
                str.AppendLine();
                str.AppendLine();
                str.Append("// Config version; should not be edited.").AppendLine();
            }

            str.Append("ConfigVersion = ").Append(configVersion).AppendLine();

            return str.ToString();
        }
    }
}