using System;
using System.Text;
using System.IO;
using Sandbox.ModAPI;
using VRageMath;

namespace Digi.BuildInfo
{
    public class Settings
    {
        private const string FILE = "settings.cfg";

        public bool textAPIUseScreenPos = false;
        public Vector2D textAPIScreenPos = Default_textAPIScreenPos;
        public float textAPIScale = Default_textAPIScale;
        public float textAPIBackgroundOpacity = Default_textAPIBackgroundOpacity; // TODO add a special value that gets the game's HUD transparency when that is available

        public static readonly Vector2D Default_textAPIScreenPos = new Vector2D(-0.9825, 0.8);
        public static readonly float Default_textAPIScale = 1f;
        public static readonly float Default_textAPIBackgroundOpacity = 0.9f;

        public bool firstLoad = false;

        private static char[] CHARS = new char[] { '=' };

        public Settings()
        {
            // load the settings if they exist
            if(!Load())
                firstLoad = true; // config didn't exist, assume it's the first time the mod is loaded

            Save(); // refresh config in case of any missing or extra settings
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
                        Log.Error("Unknown " + FILE + " line: " + line + "\nMaybe is missing the '=' ?");
                        continue;
                    }

                    args[0] = args[0].Trim().ToLower();
                    args[1] = args[1].Trim().ToLower();

                    switch(args[0])
                    {
                        case "usescreenpos":
                            if(bool.TryParse(args[1], out b))
                                textAPIUseScreenPos = b;
                            else
                                Log.Error("Invalid " + args[0] + " value: " + args[1]);
                            continue;
                        case "screenpos":
                            var vars = args[1].Split(',');
                            double x, y;
                            if(vars.Length == 2 && double.TryParse(vars[0].Trim(), out x) && double.TryParse(vars[1].Trim(), out y))
                                textAPIScreenPos = new Vector2D(x, y);
                            else
                                Log.Error("Invalid " + args[0] + " value: " + args[1]);
                            continue;
                        case "scale":
                            if(float.TryParse(args[1], out f))
                            {
                                textAPIScale = MathHelper.Clamp(f, -100, 100);
                            }
                            else
                                Log.Error("Invalid " + args[0] + " value: " + args[1]);
                            continue;
                        case "backgroundopacity":
                            if(float.TryParse(args[1], out f))
                                textAPIBackgroundOpacity = MathHelper.Clamp(f, 0, 1);
                            else
                                Log.Error("Invalid " + args[0] + " value: " + args[1]);
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

            str.Append("UseScreenPos=").Append(textAPIUseScreenPos ? "true" : "false").AppendLine(comments ? " // override screen position set by the mod with your own? Default: false" : "");
            str.Append("ScreenPos=").Append(Math.Round(textAPIScreenPos.X, 5)).Append(", ").Append(Math.Round(textAPIScreenPos.Y, 5)).AppendLine(comments ? " // only used if above setting is true; screen position in X and Y coordinates where 0,0 is the screen center. Positive values are right and up and negative ones are opposite of that. Default: " + Default_textAPIScreenPos.X + ", " + Default_textAPIScreenPos.Y : "");
            str.Append("Scale=").Append(Math.Round(textAPIScale, 5)).AppendLine(comments ? " // The overall scale. Default: " + Default_textAPIScale : "");
            str.Append("BackgroundOpacity=").Append(Math.Round(textAPIBackgroundOpacity, 5)).AppendLine(comments ? " // Background opacity percent scale (0 to 1 value). Should be configured to match the game HUD opacity. Default: " + Default_textAPIBackgroundOpacity : "");

            return str.ToString();
        }

        public void Close()
        {
        }
    }
}