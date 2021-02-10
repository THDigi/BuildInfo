using System;
using Digi.BuildInfo.Features;
using Digi.BuildInfo.Features.Config;
using Digi.ConfigLib;
using Sandbox.ModAPI;
using VRage.Utils;

namespace Digi.ComponentLib
{
    // HACK class rename required because otherwise it won't work properly with other mods that use this component system.
    // https://support.keenswh.com/spaceengineers/general/topic/1-192-022modapi-session-update-broken-by-namespaceclass-sharing-in-multiple-mods
    public partial class BuildInfo_GameSession
    {
        public static bool IsKilled = false;

        public BuildInfo_GameSession()
        {
            try
            {
                IsKilled = CheckKilled();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void LoadMod()
        {
            if(IsKilled)
            {
                PlacementDistance.ResetDefaults();

                string text = $"REMINDER: No script components loaded! Mod has been killed with '{Config.KillswitchName}' in '{Config.FileName}'.";
                Log.Info(text);
                MyLog.Default.WriteLine("### " + Log.ModName + " mod " + text);

                return;
            }

            if(MyAPIGateway.Utilities.IsDedicated)
                return; // this mod does nothing server side (with no render), no reason to allocate any more memory.

            main = new BuildInfo.BuildInfoMod(this);
        }

        bool CheckKilled()
        {
            if(MyAPIGateway.Utilities == null)
                MyAPIGateway.Utilities = MyAPIUtilities.Static; // HACK: avoid this being null xD

            // manually check config for killswitch to avoid loading any components if it's true
            // REMINDER: don't use ANY instanced fields here

            if(!MyAPIGateway.Utilities.FileExistsInLocalStorage(Config.FileName, typeof(Config)))
                return false;

            using(var file = MyAPIGateway.Utilities.ReadFileInLocalStorage(Config.FileName, typeof(Config)))
            {
                string line;
                while((line = file.ReadLine()) != null)
                {
                    if(!line.StartsWith(Config.KillswitchName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var args = line.Split(new char[] { ConfigHandler.VALUE_SEPARATOR }, 2);
                    if(args.Length != 2)
                    {
                        Log.Error($"Config '{Config.KillswitchName}' has too many or no separators ({ConfigHandler.VALUE_SEPARATOR.ToString()}), line: '{line}'");
                        break;
                    }

                    var value = args[1];
                    bool on;
                    if(!bool.TryParse(value, out on))
                    {
                        Log.Error($"Config '{Config.KillswitchName}' has invalid value: '{value}'");
                        break;
                    }

                    return on;
                }
            }

            return false;
        }
    }
}