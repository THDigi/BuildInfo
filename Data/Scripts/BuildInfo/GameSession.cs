using System;
using System.Collections.Generic;
using System.IO;
using Digi.BuildInfo;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Utilities;
using Digi.ConfigLib;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Utils;

namespace Digi.ComponentLib
{
    // HACK class rename required because otherwise it won't work properly with other mods that use this component system.
    // https://support.keenswh.com/spaceengineers/pc/topic/1-192-022modapi-session-update-broken-by-namespaceclass-sharing-in-multiple-mods
    public partial class BuildInfo_GameSession
    {
        public static bool IsKilled = false;

        static bool IsKilledComputed;
        static string CustomReason;
        static BuildInfo_GameSession Instance;

        public BuildInfo_GameSession()
        {
            Instance = this;
        }

        void LoadMod()
        {
            // HACK: game bug check, DS side
            if(MyAPIGateway.Session.IsServer && !ModContext.ModPath.StartsWith(Path.GetFullPath(ModContext.ModPath)))
            {
                const string Error = "ERROR: Mod scripts cannot read from mod folders!\n" +
                                     "This is a game bug with not cleaning paths properly.\n" +
                                     "You work around it by removing trailing slashes from '-path' launch command.\n" +
                                     "(message given by BuildInfo mod)";

                MyLog.Default.WriteLineAndConsole(Error);
            }

            GetOrComputeIsKilled();

            if(MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
                return; // this mod does nothing server side (with no render), no reason to allocate any more memory.

            // just making sure it's not too far
            MyCubeBuilder.IntersectionDistance = 12f;

            if(IsKilled)
            {
                string text = $"No script components loaded. Mod has been killed, reason: {CustomReason ?? "Unknown"}";
                Log.Info(text);
                MyLog.Default.WriteLine($"### {ModContext.ModName} mod NOTICE: {text}");
                return;
            }

            ModBase = new BuildInfoMod(this);
        }

        public static bool GetOrComputeIsKilled()
        {
            if(!IsKilledComputed)
            {
                IsKilledComputed = true;

                try
                {
                    IsKilled = CheckKilled();
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }

                Instance = null; // no longer needed
            }

            return IsKilled;
        }

        static bool CheckKilled()
        {
            if(Instance == null)
            {
                CustomReason = "Instance is null xD";
                return true;
            }

            if(MyAPIGateway.Utilities == null)
                MyAPIGateway.Utilities = MyAPIUtilities.Static; // HACK: avoid MyAPIGateway.Utilities being null

            if(MyAPIGateway.Utilities == null)
            {
                CustomReason = "MyAPIGateway.Utilities is still null after manually assigning it...";
                return true;
            }

            // leverage this feature to turn off things DS-side
            if(MyAPIGateway.Utilities.IsDedicated)
                return true;

            if(CheckKilledByConfig())
                return true;

            if(CheckKilledByPriority())
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

        static bool CheckKilledByPriority()
        {
            const string FileName = "buildinfo-priority.txt";

            if(Instance?.Session?.Mods == null)
            {
                CustomReason = "Instance?.Session?.Mods is null :(";
                return true;
            }

            // HACK: fallback way of finding current mod because ModContext might be null at this time
            MyObjectBuilder_Checkpoint.ModItem? thisMod = null;
            string scopeName = MyAPIGateway.Utilities.GamePaths.ModScopeName;

            if(Instance?.ModContext != null)
                thisMod = Instance.ModContext.ModItem;

            List<MyTuple<int, MyObjectBuilder_Checkpoint.ModItem>> biMods = new List<MyTuple<int, MyObjectBuilder_Checkpoint.ModItem>>();

            foreach(MyObjectBuilder_Checkpoint.ModItem mod in Instance.Session.Mods)
            {
                if(!MyAPIGateway.Utilities.FileExistsInModLocation(FileName, mod))
                    continue; // not a relevant mod

                if(thisMod == null)
                {
                    // HACK: adding the _ separator so that it cannot be mistaken for another mod; e.g. BuildInfo_Unimportant vs BuildInfo.dev_Unimportant;
                    string modNameTest = mod.Name + "_";

                    if(scopeName.StartsWith(modNameTest))
                        thisMod = mod;
                }

                string firstLine;

                using(TextReader reader = MyAPIGateway.Utilities.ReadFileInModLocation(FileName, mod))
                {
                    firstLine = reader.ReadLine();
                }

                int priority;
                if(!int.TryParse(firstLine, out priority))
                {
                    priority = int.MinValue;
                    //Log.Error($"Couldn't parse {FileName} first line, not an integer. Mod: {mod.FriendlyName} ({mod.PublishedServiceName}:{mod.PublishedFileId})");
                    //continue;
                }

                biMods.Add(MyTuple.Create(priority, mod));
            }

            if(thisMod == null)
            {
                CustomReason = $"thisMod == null";
                return true;
            }

            //MyLog.Default.WriteLine($"### {thisMod.Value.GetNameAndId()}: found other buildinfo mods:");
            //foreach(var tuple in biMods)
            //{
            //    MyLog.Default.WriteLine($" - {tuple.Item2.GetNameAndId()}; priority={tuple.Item1}");
            //}

            if(biMods.Count > 1)
            {
                // sort descending
                biMods.Sort((a, b) => b.Item1.CompareTo(a.Item1));

                MyObjectBuilder_Checkpoint.ModItem priorityMod = biMods[0].Item2;

                if(thisMod.Value.Name != priorityMod.Name)
                {
                    CustomReason = $"a higher priority mod is present: {priorityMod.GetNameAndId()}";
                    return true;
                }
            }

            return false;
        }
    }
}