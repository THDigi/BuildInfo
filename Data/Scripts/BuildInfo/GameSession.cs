using System;
using System.Collections.Generic;
using System.IO;
using BuildInfo.Utilities;
using Digi.BuildInfo;
using Digi.BuildInfo.Features;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Utilities;
using Digi.ConfigLib;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace Digi.ComponentLib
{
    // HACK class rename required because otherwise it won't work properly with other mods that use this component system.
    // https://support.keenswh.com/spaceengineers/pc/topic/1-192-022modapi-session-update-broken-by-namespaceclass-sharing-in-multiple-mods
    public partial class BuildInfo_GameSession
    {
        public static bool IsKilled = false;
        static string CustomReason;
        static BuildInfo_GameSession Instance;

        string KilledBySignal = null;

        const long InterModId = InterModAPI.MOD_API_ID - 1;

        public BuildInfo_GameSession()
        {
            Instance = this;

            // for kill signal from other versions
            MyAPIGateway.Utilities = MyAPIUtilities.Static;
            MyAPIGateway.Utilities.RegisterMessageHandler(InterModId, ModMessageReceived);
        }

        void LoadMod()
        {
            // time's up xD
            MyAPIGateway.Utilities.UnregisterMessageHandler(InterModId, ModMessageReceived);

            CheckPathBugs(ModContext);

            if(MyAPIGateway.Utilities.IsDedicated)
            {
                IsKilled = true;
                return; // this mod does nothing else server side (with no render), no reason to allocate any more memory.
            }

            IsKilled = CheckKilled();

            // just making sure it's not too far
            MyCubeBuilder.IntersectionDistance = 12f;

            if(IsKilled)
            {
                string text = $"No script components loaded, mod intentionally killed. Reason:\n{CustomReason ?? "Unknown"}";
                Log.Info(text);
                MyLog.Default.WriteLine($"### Mod {ModContext.ModItem.GetNameAndId()} NOTICE: {text}");
                MyDefinitionErrors.Add((MyModContext)ModContext, $"Mod intentionally killed. Reason:\n{CustomReason ?? "Unknown"}", TErrorSeverity.Notice, false);
                return;
            }

            try
            {
                ModBase = new BuildInfoMod(this);
            }
            catch(Exception e)
            {
                Log.Error(e);
                IsKilled = true;
                MyLog.Default.WriteLine($"### Mod {ModContext.ModItem.GetNameAndId()} NOTICE: Error during initialization, killed the rest of the mod to prevent issues. Report to author!");

                // this shows F11 in offline or a prompt that a mod failed to load in online, pointing people to the log.
                MyDefinitionErrors.Add((MyModContext)ModContext, $"Error during initialization, killed the rest of the mod to prevent issues. Report to author! Stacktrace: {e.ToString()}", TErrorSeverity.Critical, false);

                try
                {
                    ModBase?.WorldExit();
                    ModBase = null;
                }
                catch(Exception)
                {
                    // HACK: any errors here are not important as they're side effects
                }
            }
        }

        void UnloadMod()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(InterModId, ModMessageReceived);
        }

        // This called by IMyHudStat classes would trigger between this constructor and LoadData().
        public static bool GetOrComputeIsKilled(string source)
        {
            try
            {
                if(Instance.KilledBySignal == null)
                {
#if PLUGIN_LOADER
                    MyAPIGateway.Utilities.SendModMessage(InterModId, Instance.ModContext.ModId);
#else
                    // HACK: mods brought by plugin loader have empty name
                    if(string.IsNullOrEmpty(Instance.ModContext.ModItem.FriendlyName))
                    {
                        MyAPIGateway.Utilities.SendModMessage(InterModId, Instance.ModContext.ModId);
                    }
#endif
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            return IsKilled;
        }

        void ModMessageReceived(object obj)
        {
            try
            {
                string modId = obj as string;
                if(modId != null && modId != ModContext.ModId)
                {
                    KilledBySignal = modId;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        static bool CheckKilled()
        {
            if(Instance == null)
            {
                CustomReason = "Instance is null xD";
                return true;
            }

            if(MyAPIGateway.Utilities == null)
            {
                CustomReason = "MyAPIGateway.Utilities is still null after manually assigning it...";
                return true;
            }

            if(Instance.KilledBySignal != null)
            {
                CustomReason = $"Another BuildInfo version ({Instance.KilledBySignal}) sent a kill signal (brought in by plugin loader for example).";
                return true;
            }

            if(CheckKilledByConfig())
                return true;

            return false;
        }

        static bool CheckKilledByConfig()
        {
            // manually check config for killswitch to avoid loading any components if it's true
            // REMINDER: don't use ANY instanced fields here
            if(MyAPIGateway.Utilities.FileExistsInLocalStorage(Config.FileName, typeof(Config)))
            {
                using(TextReader file = MyAPIGateway.Utilities.ReadFileInLocalStorage(Config.FileName, typeof(Config)))
                {
                    string line;
                    while((line = file.ReadLine()) != null)
                    {
                        if(!line.StartsWith(Config.KillswitchName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        string[] args = line.Split(new char[] { ConfigHandler.VALUE_SEPARATOR }, 2);
                        if(args.Length != 2)
                        {
                            Log.Error($"Config '{Config.KillswitchName}' has too many or no separators ({ConfigHandler.VALUE_SEPARATOR.ToString()}), line: '{line}'");
                            break;
                        }

                        string value = args[1];
                        bool on;
                        if(!bool.TryParse(value, out on))
                        {
                            Log.Error($"Config '{Config.KillswitchName}' has invalid value: '{value}'");
                            break;
                        }

                        if(on)
                            CustomReason = $"'{Config.KillswitchName}' setting in '{Config.FileName}'";
                        return on;
                    }
                }
            }

            return false;
        }

        static void CheckPathBugs(IMyModContext modContext)
        {
            // HACK: game bug check, usually on DS side
            if(MyAPIGateway.Session.IsServer && !modContext.ModPath.StartsWith(Path.GetFullPath(modContext.ModPath)))
            {
                const string Error = "ERROR: Mod scripts cannot read from mod folders!\n" +
                                     "This is a game bug with not cleaning paths properly.\n" +
                                     "You can work around it by removing trailing slashes from '-path' launch command.\n" +
                                     "(message given by BuildInfo mod)";

                MyLog.Default.WriteLineAndConsole(Error);
            }
        }

        // triggers on everyone, DS too!
        void WorldLoaded()
        {
            CheckModsForDuplicates();
        }

        void CheckModsForDuplicates()
        {
            List<string> dupeMods = null;

            try
            {
                if(MyAPIGateway.Session == null) throw new Exception("Session  is null");
                if(MyAPIGateway.Session.Mods == null) throw new Exception("Session.Mods is null");

                HashSet<string> uniqueMods = new HashSet<string>(MyAPIGateway.Session.Mods.Count);

                foreach(MyObjectBuilder_Checkpoint.ModItem mod in MyAPIGateway.Session.Mods)
                {
                    string id = mod.PublishedFileId == 0 ? mod.Name : $"{mod.PublishedServiceName}:{mod.PublishedFileId}";

                    if(!uniqueMods.Add(id))
                    {
                        var modContext = (MyModContext)mod.GetModContext();
                        modContext.CurrentFile = "(all of it)";

                        string name = mod.GetNameAndId();
                        MyLog.Default.WriteLineAndConsole($"  ERROR: Mod {name} is added more than once in the mods list!");

                        if(dupeMods == null)
                            dupeMods = new List<string>();

                        dupeMods.Add(name);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            if(dupeMods != null && dupeMods.Count > 0)
            {
                ModCrash.Throw(new Exception($"{dupeMods.Count} mods added multiple times:\n    " + string.Join("\n    ", dupeMods)),
                    schedule: true);
            }
        }
    }
}