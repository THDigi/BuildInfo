using System;
using System.IO;
using System.Text;
using Sandbox.ModAPI;
using VRageMath;

namespace Digi.BuildInfo
{
    public class Settings
    {
        public bool textAPIUseScreenPos;
        public Vector2D textAPIScreenPos;
        public float textAPIScale;
        public float textAPIBackgroundOpacity;

        public const bool default_textAPIUseScreenPos = false;
        public readonly Vector2D default_textAPIScreenPos = new Vector2D(-0.9825, 0.8);
        public const float default_textAPIScale = 1f;
        public const float default_textAPIBackgroundOpacity = -1f;

        public bool firstLoad = false;

        private const string FILE = "settings.cfg";
        private readonly char[] CHARS = new char[] { '=' };

        public Settings()
        {
            textAPIUseScreenPos = default_textAPIUseScreenPos;
            textAPIScreenPos = default_textAPIScreenPos;
            textAPIScale = default_textAPIScale;
            textAPIBackgroundOpacity = default_textAPIBackgroundOpacity;

            // load the settings if they exist
            if(!Load())
                firstLoad = true; // config didn't exist, assume it's the first time the mod is loaded

            Save(); // refresh config in case of any missing or extra settings
        }

        public void Close()
        {
        }

        public bool Load()
        {
            try
            {
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

                    i = line.IndexOf("//", StringComparison.Ordinal);

                    if(i > -1)
                        line = (i == 0 ? "" : line.Substring(0, i));

                    if(line.Length == 0)
                        continue;

                    args = line.Split(CHARS, 2);

                    if(args.Length != 2)
                    {
                        Log.Error($"Unknown {FILE} line: {line}\nMaybe is missing the '=' ?");
                        continue;
                    }

                    var key = args[0];
                    var val = args[1];

                    if(key.Equals("UseScreenPos", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if(bool.TryParse(val, out b))
                            textAPIUseScreenPos = b;
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

                    if(key.Equals("Scale", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if(float.TryParse(val, out f))
                            textAPIScale = MathHelper.Clamp(f, -100, 100);
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
                str.AppendLine("// BuildInfo mod config; this file gets automatically overwritten after being loaded so don't leave custom comments.");
                str.AppendLine("// You can reload this while the game is running by typing in chat: /buildinfo reload");
                str.AppendLine("// Lines starting with // are comments. All values are case insensitive unless otherwise specified.");
                str.AppendLine();
            }

            if(comments)
            {
                str.AppendLine("// These control the visuals of the info box that gets created with textAPI.");
            }

            str.Append("UseScreenPos=").Append(textAPIUseScreenPos).AppendLine(comments ? " // override screen position set by the mod with your own? Default: " + default_textAPIUseScreenPos : "");
            str.Append("ScreenPos=").Append(Math.Round(textAPIScreenPos.X, 5)).Append(", ").Append(Math.Round(textAPIScreenPos.Y, 5)).AppendLine(comments ? " // only used if above setting is true; screen position in X and Y coordinates where 0,0 is the screen center. Positive values are right and up and negative ones are opposite of that. Default: " + default_textAPIScreenPos.X + ", " + default_textAPIScreenPos.Y : "");
            str.Append("Scale=").Append(Math.Round(textAPIScale, 5)).AppendLine(comments ? " // The overall scale. Default: " + default_textAPIScale : "");
            str.Append("BackgroundOpacity=").Append(textAPIBackgroundOpacity < 0 ? "HUD" : Math.Round(textAPIBackgroundOpacity, 5).ToString()).AppendLine(comments ? " // Background opacity percent scale (0 to 1 value) or can be set to the word HUD to use the game's background opacity. Default: " + (default_textAPIBackgroundOpacity < 0 ? "HUD" : default_textAPIBackgroundOpacity.ToString()) : "");

            return str.ToString();
        }
    }
}