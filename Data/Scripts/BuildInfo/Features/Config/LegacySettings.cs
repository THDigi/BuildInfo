using System;
using System.Collections.Generic;
using System.IO;
using Digi.BuildInfo.Utilities;
using Digi.ConfigLib;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.Config
{
    /// <summary>
    /// Loads the settings from the old settings.cfg
    /// </summary>
    public class LegacyConfig : ModComponent
    {
        private bool newConfigExists = false;
        private const string FILE = "settings.cfg";
        private readonly char[] CHARS = new char[] { '=' };

        public LegacyConfig(BuildInfoMod main) : base(main)
        {
            newConfigExists = MyAPIGateway.Utilities.FileExistsInLocalStorage(Config.FileName, typeof(Config));
        }

        public override void RegisterComponent()
        {
            LoadAndDelete();
        }

        public override void UnregisterComponent()
        {
        }

        private void LoadAndDelete()
        {
            try
            {
                bool oldConfigExists = MyAPIGateway.Utilities.FileExistsInLocalStorage(FILE, typeof(LegacyConfig));

                if(newConfigExists && oldConfigExists)
                {
                    Log.Info($"Found {FILE} while the new config also already existed, deleting {FILE}.");

                    MyAPIGateway.Utilities.DeleteFileInLocalStorage(FILE, typeof(LegacyConfig));
                    return;
                }

                if(oldConfigExists)
                {
                    Log.Info($"Found legacy {FILE} loading and deleting...");

                    using(TextReader file = MyAPIGateway.Utilities.ReadFileInLocalStorage(FILE, typeof(LegacyConfig)))
                    {
                        ReadSettings(file);
                    }

                    Log.Info($"All loaded, deleting {FILE}.");

                    MyAPIGateway.Utilities.DeleteFileInLocalStorage(FILE, typeof(LegacyConfig));
                    return;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void LoadSetting(ISetting setting, string value, string line)
        {
            string error;
            setting.ReadValue(value, out error);
            if(error != null)
                Log.Error($"Legacy {FILE} got an error on load: {error}\nOriginal line: {line}\nIf this was a custom setting please transfer the value manually to the new config: {Config.FileName}");
        }

        private void ReadSettings(TextReader file)
        {
            try
            {
                const StringComparison COMPARE_TYPE = StringComparison.InvariantCultureIgnoreCase;
                bool allLabels = true;
                bool axisLabels = true;
                int configVersion = 0;
                string l;
                List<string> lines = new List<string>();

                while((l = file.ReadLine()) != null)
                {
                    if(l.Length == 0)
                        continue;

                    int index = l.IndexOf("//");

                    if(index > -1)
                        l = (index == 0 ? "" : l.Substring(0, index));

                    if(l.Length == 0)
                        continue;

                    lines.Add(l);

                    string[] args = l.Split(CHARS, 2);

                    if(args.Length != 2)
                    {
                        Log.Error($"Unknown {FILE} line: {l}\nMaybe is missing the '=' ?");
                        continue;
                    }

                    string key = args[0].Trim();

                    if(configVersion == 0 && key.Equals("ConfigVersion", COMPARE_TYPE))
                    {
                        int tmp;
                        if(int.TryParse(args[1], out tmp))
                            configVersion = tmp;
                        continue;
                    }
                }

                if(configVersion < 1) // only read cfg version 1 or higher
                    return;

                Main.Config.Handler.ConfigVersion.Value = Math.Min(configVersion, 2); // can't be higher than 2

                foreach(string line in lines)
                {
                    string[] args = line.Split(CHARS, 2);

                    if(args.Length != 2)
                    {
                        Log.Error($"Unknown {FILE} line: {line}\nMaybe is missing the '=' ?");
                        continue;
                    }

                    string key = args[0].Trim();
                    string val = args[1].Trim();

                    if(key.Equals("MenuBind", COMPARE_TYPE))
                    {
                        LoadSetting(Main.Config.MenuBind, val, line);
                        continue;
                    }

                    if(key.Equals("CycleOverlaysBind", COMPARE_TYPE))
                    {
                        LoadSetting(Main.Config.CycleOverlaysBind, val, line);
                        continue;
                    }

                    if(key.Equals("ToggleTransparencyBind", COMPARE_TYPE))
                    {
                        LoadSetting(Main.Config.ToggleTransparencyBind, val, line);
                        continue;
                    }

                    if(key.Equals("FreezePlacementBind", COMPARE_TYPE))
                    {
                        LoadSetting(Main.Config.FreezePlacementBind, val, line);
                        continue;
                    }

                    if(key.Equals("BlockPickerBind", COMPARE_TYPE))
                    {
                        LoadSetting(Main.Config.BlockPickerBind, val, line);
                        continue;
                    }

                    if(key.Equals("ShowTextInfo", COMPARE_TYPE))
                    {
                        LoadSetting(Main.Config.TextShow, val, line);
                        continue;
                    }

                    if(key.Equals("AlwaysVisible", COMPARE_TYPE))
                    {
                        LoadSetting(Main.Config.TextAlwaysVisible, val, line);
                        continue;
                    }

                    if(key.Equals("UseCustomStyling", COMPARE_TYPE)
                    || key.Equals("UseScreenPos", COMPARE_TYPE)) // backwards compatibility
                    {
                        LoadSetting(Main.Config.TextAPICustomStyling, val, line);
                        continue;
                    }

                    if(key.Equals("ScreenPos", COMPARE_TYPE))
                    {
                        if(val.Equals("-0.9825, 0.8"))
                            Log.Info($"{key} is default, ignoring because new default is different.");
                        else
                            LoadSetting(Main.Config.TextAPIScreenPosition, val, line);
                        continue;
                    }

                    if(key.Equals("Alignment", COMPARE_TYPE))
                    {
                        if(val.Equals("left, top", COMPARE_TYPE))
                            Log.Info($"{key} is default, ignoring because new default is different.");
                        else
                            LoadSetting(Main.Config.TextAPIAlign, val, line);
                        continue;
                    }

                    if(key.Equals("Scale", COMPARE_TYPE))
                    {
                        LoadSetting(Main.Config.TextAPIScale, val, line);
                        continue;
                    }

                    if(key.Equals("BackgroundOpacity", COMPARE_TYPE))
                    {
                        LoadSetting(Main.Config.TextAPIBackgroundOpacity, val, line);
                        continue;
                    }

                    if(key.Equals("AllLabels", COMPARE_TYPE))
                    {
                        allLabels = val.ContainsIgnoreCase("true");
                        continue;
                    }

                    if(key.Equals("AxisLabels", COMPARE_TYPE))
                    {
                        axisLabels = val.ContainsIgnoreCase("true");
                        continue;
                    }

                    bool colorWorld = key.Equals("LeakParticleColorWorld", COMPARE_TYPE);
                    if(colorWorld || key.Equals("LeakParticleColorOverlay", COMPARE_TYPE))
                    {
                        if(colorWorld)
                            LoadSetting(Main.Config.LeakParticleColorWorld, val, line);
                        else
                            LoadSetting(Main.Config.LeakParticleColorOverlay, val, line);
                        continue;
                    }

                    if(key.Equals("AdjustBuildDistance", COMPARE_TYPE))
                    {
                        LoadSetting(Main.Config.AdjustBuildDistanceSurvival, val, line);
                        continue;
                    }

                    if(key.Equals("Debug", COMPARE_TYPE))
                    {
                        LoadSetting(Main.Config.Debug, val, line);
                        continue;
                    }
                }

                Main.Config.OverlayLabels.Value = (int)OverlayLabelsFlags.None;

                if(allLabels && axisLabels)
                    Main.Config.OverlayLabels.Value = (int)OverlayLabelsFlags.All;
                else if(allLabels && !axisLabels)
                    Main.Config.OverlayLabels.Value |= (int)OverlayLabelsFlags.Other;

                Main.Config.Save();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}