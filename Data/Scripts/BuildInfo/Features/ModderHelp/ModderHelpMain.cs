using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Draygo.API;
using ProtoBuf;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Definitions.SafeZone;
using VRage.Collections;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions.SessionComponents;
using VRage.Input;
using VRage.Library.Utils;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.ModderHelp
{
    public class ModderHelpMain : ModComponent
    {
        public const bool CheckEverything = false;

        int ModProblems = 0;
        int ModHints = 0;
        bool DefinitionErrors = false;
        bool CompileErrors = false;
        bool F11MenuShownOnLoad = false;

        HudAPIv2.BillBoardHUDMessage ErrorsMenuBackdrop;

        const string ErrorsGUITypeName = "MyGuiScreenDebugErrors";
        const string AppendMsg = "> [BuildInfo extra explanation] "; // messages added to existing errors/warnings
        const string CustomMsg = "> [Added by BuildInfo] "; // errors/warnings added by this mod
        const string LineSignature = "\n> ";

        public bool IsF11MenuAccessible => MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE;

        public ModderHelpMain(BuildInfoMod main) : base(main)
        {
            //CheckEverything = BuildInfoMod.IsDevMod;

            if(MyAPIGateway.Session == null)
            {
                Log.Error("MyAPIGateway.Session is null in LoadData() O.o", Log.PRINT_MESSAGE);
                return;
            }

            if(MyAPIGateway.Session.Config == null)
            {
                Log.Info("WARNING: `MyAPIGateway.Session.Config` is null.");
                MyLog.Default.WriteLine($"{CustomMsg}WARNING: `MyAPIGateway.Session.Config` is null.");
            }
        }

        public override void RegisterComponent()
        {
            Main.GUIMonitor.ScreenAdded += GUIScreenAdded;
            Main.GUIMonitor.ScreenRemoved += GUIScreenRemoved;

            if(Main.Config.ModderHelpAlerts.Value)
            {
                HashSet<string> localMods = new HashSet<string>();

                foreach(MyObjectBuilder_Checkpoint.ModItem modItem in MyAPIGateway.Session.Mods)
                {
                    if(modItem.PublishedFileId == 0)
                        localMods.Add(modItem.Name);
                }

                CheckErrors(localMods, IsF11MenuAccessible);

                if(CheckEverything || (IsF11MenuAccessible && localMods.Count > 0))
                {
                    CheckModDefinitions();
                    CheckModFiles();

                    F11MenuShownOnLoad = MyDefinitionErrors.ShouldShowModErrors;
                }
            }

            // alert player in chat if applicable
            Main.GameConfig.FirstSpawn += FirstSpawn;

            // This game bug was fixed in SE v205
            //CheckVideos();
        }

        public override void UnregisterComponent()
        {
            Main.GUIMonitor.ScreenAdded -= GUIScreenAdded;
            Main.GUIMonitor.ScreenRemoved -= GUIScreenRemoved;
            Main.GameConfig.FirstSpawn -= FirstSpawn;
        }

        [ProtoContract]
        struct HackCloudLayerSettings  // HACK: MyCloudLayerSettings is not whitelisted
        {
            //[ProtoMember(1)] public string Model;
            [ProtoMember(4)][XmlArrayItem("Texture")] public List<string> Textures;
            //[ProtoMember(7)] public float RelativeAltitude;
            //[ProtoMember(10)] public Vector3D RotationAxis;
            //[ProtoMember(13)] public float AngularVelocity;
            //[ProtoMember(16)] public float InitialRotation;
            //[ProtoMember(19)] public bool ScalingEnabled;
            //[ProtoMember(22)] public float FadeOutRelativeAltitudeStart;
            //[ProtoMember(25)] public float FadeOutRelativeAltitudeEnd;
            //[ProtoMember(28)] public float ApplyFogRelativeDistance;
            //[ProtoMember(31)] public Vector4 Color = Vector4.One;
        }

        struct CloudLayerInfo
        {
            public string AppendMessage;
            public TErrorSeverity? SetSeverity;
        }

        enum FileExists { Missing, Mod, Game }
        static string ExistsString(FileExists exists)
        {
            switch(exists)
            {
                case FileExists.Mod: return "Exists in Mod";
                case FileExists.Game: return "Exists in Game";
                default: return "MISSING";
            }
        }

        void CheckCloudLayer(Dictionary<string, CloudLayerInfo> cloudLayerInfo, MyPlanetGeneratorDefinition def, int layerIndex)
        {
            // HACK: MyCloudLayerSettings is not whitelisted
            byte[] binary = MyAPIGateway.Utilities.SerializeToBinary(def.CloudLayers[layerIndex]);
            HackCloudLayerSettings hackLayer = MyAPIGateway.Utilities.SerializeFromBinary<HackCloudLayerSettings>(binary);

            if(hackLayer.Textures == null || hackLayer.Textures.Count <= 0)
                return;

            int layerNum = layerIndex + 1;

            // HACK: from MyCloudRenderer.CreateCloudLayer()

            if(hackLayer.Textures.Count > 1)
            {
                MyDefinitionErrors.Add(def.Context,
                    $"{CustomMsg}Planet '{def.Id.SubtypeName}' at CloudLayer #{layerNum}: "
                    + "More than 1 texture, game only uses the first Texture tag! (and gives it _cm and _alphamask suffix to read 2 textures)",
                    TErrorSeverity.Warning);
            }

            string filePath = hackLayer.Textures[0];

            // HACK: depending on definition style it can behave differently:
            //   <Definition xsi:type="PlanetGeneratorDefinition">:
            //     it doesn't force mod path to cloudlayer textures and textures can be in game folder too.
            //   <PlanetGeneratorDefinitions>:
            //     it adds mod path to cloudlayer textures, can't be referenced in game folder and can result in double-added mod paths if texture exists.
            // in more words: https://github.com/THDigi/SE-ModScript-Examples/wiki/Hidden-SBC-tags-features#planet-cloudlayers

            try
            {
                Path.GetFullPath(filePath);
                // if path is not valid, throws a System.NotSupportedException: The given path's format is not supported.
                // might have other exceptions, anything that makes this throw should be coverted by this custom error.
            }
            catch(Exception)
            {
                // game is not gonna add an error for this file path so I gotta add one myself
                MyDefinitionErrors.Add(def.Context,
                    $"{CustomMsg}Planet '{def.Id.SubtypeName}' at CloudLayer #{layerNum}: Invalid path: '{filePath}'"
                    + LineSignature + "If the mod path is added twice then the problem is that the texture exists AND using a new planet generator definition style (compare EarthLike and Pertram start/end tags)."
                    + LineSignature + "Either modify definition to use the old style (recommended) or make it so the <Texture> tag doesn't point to an existing file while also having _cm and _alphamask prefixed ones with same name nearby.",
                    TErrorSeverity.Error);

                return; // intentionally eating the error
            }

            bool startsWithModPath = filePath.StartsWith(def.Context.ModPath);

            // HACK: path not always has mod folder in it here, but in errors list it does.
            string pathKey = filePath;
            if(!startsWithModPath)
                pathKey = Path.Combine(def.Context.ModPath, pathKey);

            cloudLayerInfo[pathKey] = new CloudLayerInfo()
            {
                AppendMessage = $"\n{AppendMsg}You can ignore this game error if the planet looks fine." +
                                $"\n{LineSignature}If you're the planet's author you can ask on Keen discord how to get rid the fake cloudlayer error.",
                SetSeverity = TErrorSeverity.Warning,
            };

            // FIXME: same texture used in multiple planets on the same mod collide.
            // this is a problem if those different planets use different definition styles, changing the checks and errors significantly.

#if false
            string pathAlphaMask;
            string pathCM;
            try
            {
                pathAlphaMask = filePath.Insert(filePath.LastIndexOf('.'), "_alphamask");
                pathCM = filePath.Insert(filePath.LastIndexOf('.'), "_cm");
            }
            catch(Exception e)
            {
                cloudLayerInfo[filePath] = new CloudLayerInfo()
                {
                    AppendMessage = "Error processing texture name to add _alphamask and _cm in it, game will probably crash too.",
                    SetSeverity = TErrorSeverity.Error,
                };
                return;
            }

            FileExists existsCM = FileExists.Missing;
            FileExists existsAlphaMask = FileExists.Missing;

            if(startsWithModPath) // game added mod path, so either old definition + fake texture or new definition without fake texture (because we checked if path is valid above)
            {
                try
                {
                    if(MyAPIGateway.Utilities.FileExistsInModLocation(pathCM, def.Context.ModItem))
                        existsCM = FileExists.Mod;
                }
                catch(Exception e)
                {
                    Log.Error($"Error reading planet '{def.Id.SubtypeName}' cloudlayer #{layerNum}: '{pathCM}'\nOriginal path: '{filePath}'\n{e}");
                }

                try
                {
                    if(MyAPIGateway.Utilities.FileExistsInModLocation(pathAlphaMask, def.Context.ModItem))
                        existsAlphaMask = FileExists.Mod;
                }
                catch(Exception e)
                {
                    Log.Error($"Error reading planet '{def.Id.SubtypeName}' cloudlayer #{layerNum}: '{pathAlphaMask}'\nOriginal path: '{filePath}'\n{e}");
                }
            }

            if(!startsWithModPath) // AKA using old definition without fake texture, meaning it didn't append mod folder to path
            {
                try
                {
                    if(existsCM == FileExists.Missing && MyAPIGateway.Utilities.FileExistsInGameContent(pathCM))
                        existsCM = FileExists.Game;
                }
                catch(Exception e)
                {
                    Log.Error($"Error reading planet '{def.Id.SubtypeName}' cloudlayer #{layerNum}: '{pathCM}'\nOriginal path: '{filePath}'\n{e}");
                }

                try
                {
                    if(existsAlphaMask == FileExists.Missing && MyAPIGateway.Utilities.FileExistsInGameContent(pathAlphaMask))
                        existsAlphaMask = FileExists.Game;
                }
                catch(Exception e)
                {
                    Log.Error($"Error reading planet '{def.Id.SubtypeName}' cloudlayer #{layerNum}: '{pathAlphaMask}'\nOriginal path: '{filePath}'\n{e}");
                }
            }

            CloudLayerInfo info = new CloudLayerInfo();

            info.AppendMessage = $"From planet '{def.Id.SubtypeName}' cloudlayer #{layerNum}: Can be ignored as long as the textures with the '_cm' and '_alphamask' suffixes exist and they work in game."
                               + $"\n - ColorMetal {ExistsString(existsCM)}: '{(startsWithModPath ? "" : "<SE Content>")}{pathCM}'"
                               + $"\n - AlphaMask {ExistsString(existsAlphaMask)}: '{(startsWithModPath ? "" : "<SE Content>")}{pathAlphaMask}'";

            if(!startsWithModPath)
                info.AppendMessage += "\nNote: relative path detected, if you want the textures to be used from your mod, add a fake texture without the suffixes so the game finds it and uses your mod path.";

            if(existsCM != FileExists.Missing && existsAlphaMask != FileExists.Missing)
                info.SetSeverity = TErrorSeverity.Notice; // reduce severity if both textures are found
            else if(existsCM == FileExists.Missing && existsAlphaMask == FileExists.Missing)
                info.SetSeverity = TErrorSeverity.Error; // elevate severity if both are missing
            else
                info.SetSeverity = TErrorSeverity.Warning; // middleground if only one is missing

            // HACK: planets with <Definition xsi:type="PlanetGeneratorDefinition"> do not forcefully add mod path but then in errors it'll have the mod path, so gotta add it here to link it to the error properly
            string pathKey = filePath;
            if(!startsWithModPath)
                pathKey = Path.Combine(def.Context.ModPath, pathKey);

            cloudLayerInfo[pathKey] = info;
#endif
        }

        Dictionary<string, CloudLayerInfo> GrabCloudLayers()
        {
            Dictionary<string, CloudLayerInfo> cloudLayerInfo = new Dictionary<string, CloudLayerInfo>();

            Dictionary<MyStringHash, MyDefinitionBase> planets;
            if(MyDefinitionManager.Static.Definitions.Definitions.TryGetValue(typeof(MyObjectBuilder_PlanetGeneratorDefinition), out planets))
            {
                foreach(MyPlanetGeneratorDefinition def in planets.Values)
                {
                    if(def?.Context == null || def.Context.IsBaseGame)
                        continue;

                    for(int i = 0; i < def.CloudLayers.Count; i++)
                    {
                        try
                        {
                            CheckCloudLayer(cloudLayerInfo, def, i);
                        }
                        catch(Exception e)
                        {
                            Log.Error($"Error reading planet '{def.Id.SubtypeName}' cloudlayer #{(i + 1)}': {e}");
                        }
                    }
                }
            }
            else
            {
                Log.Error("Couldn't get planets from DefinitionSet O.o");
            }

            return cloudLayerInfo;
        }

        void CheckErrors(HashSet<string> localMods, bool f11MenuAccessible)
        {
            Dictionary<string, CloudLayerInfo> cloudLayerInfo = null;

            if(f11MenuAccessible)
                cloudLayerInfo = GrabCloudLayers();

            string phase6error = "MOD PARTIALLY SKIPPED, LOADED ONLY 6/6 PHASES, Following Error occured:" + Environment.NewLine + "Object reference not set to an instance of an object.";
            string blockInitError = $"at {typeof(MyCubeBlockDefinition).FullName}.Init";

            CheckErrorsOnF11(); // to reduce severity on errors before setting DefinitionErrors

            bool hasSignalDLC = MyAPIGateway.DLC.HasDLC("Signal", MyAPIGateway.Multiplayer.MyId);

            ListReader<MyDefinitionErrors.Error> errors = MyDefinitionErrors.GetErrors();

            for(int i = 0; i < errors.Count; i++)
            {
                MyDefinitionErrors.Error error = errors[i];

                bool isLocal = CheckEverything || localMods.Contains(error.ModName);

                // chat message when LOCAL mods have definition errors
                if(isLocal && error.Severity >= TErrorSeverity.Error)
                {
                    DefinitionErrors = true;
                }

                if(!CompileErrors)
                {
                    // from MyScriptManager.Compile()
                    if(error.Message.StartsWith("Compilation of")
                    || (error.Message.StartsWith("Cannot load ") && error.Message.EndsWith(".cs")))
                    {
                        // chat message when ANY mod has compile errors (published too)
                        CompileErrors = true;

                        // pop up F11 menu if there's compile errors with local mods
                        if(isLocal && f11MenuAccessible)
                            MyDefinitionErrors.ShouldShowModErrors = true;
                    }
                }

                // HACK: hardcoded various MyDefinitionErrors

                {
                    // MyWaveBank.FindAudioFile()
                    // "Unable to find audio file: '{cue.SubtypeId}', '{fileName}'"
                    const string unableToFindAudioFile = "Unable to find audio file: '";
                    if(error.Message.StartsWith(unableToFindAudioFile))
                    {
                        bool isMod = error.ModName != "Unknown";

                        if(!isMod)
                        {
                            if(!hasSignalDLC)
                            {
                                string strippedMessage = error.Message.Substring(unableToFindAudioFile.Length).TrimStart();

                                if(strippedMessage.StartsWith("MusConcert_"))
                                {
                                    error.Severity = TErrorSeverity.Notice;
                                    error.Message += $"\n{AppendMsg}This just a side effect of the Signal DLC not being installed.";
                                }
                            }

                            continue;
                        }

                        if(isMod && f11MenuAccessible)
                        {
                            error.Message += $"\n{AppendMsg}If you intend on referencing game sounds, you can only do with .wav files by using original relative path and removing the .wav extension.";
                            continue;
                        }
                    }
                }

                if(f11MenuAccessible)
                {
                    // MyCubeBlockDefinition.InitMountPoints() has double standards on undefined mountpoints
                    if(error.Message.StartsWith("Obsolete default definition of mount points in"))
                    {
                        error.Severity = TErrorSeverity.Error; // escalate it because it's pretty bad
                        error.Message += $"\n{AppendMsg}This means game will generate mountpoints for this block! Add a disabled mountpoint to have no mountpoints on this block.";
                    }

                    // MyDefinitionManager.ProcessContentFilePath() + cloudlayer stuff from above
                    const string resourceNotFoundPrefix = "Resource not found, setting to null. Resource path: ";
                    if(error.Message.StartsWith(resourceNotFoundPrefix))
                    {
                        string filePath = error.Message.Substring(resourceNotFoundPrefix.Length, error.Message.Length - resourceNotFoundPrefix.Length);

                        if(filePath.EndsWith(".xwm", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                        {
                            error.Message += $"\n{AppendMsg}Path to sounds in mods start from the mod root folder.";
                            continue;
                        }

                        CloudLayerInfo info;
                        if(cloudLayerInfo.TryGetValue(filePath, out info))
                        {
                            if(!string.IsNullOrEmpty(info.AppendMessage))
                                error.Message += info.AppendMessage;

                            if(info.SetSeverity.HasValue)
                                error.Severity = info.SetSeverity.Value;
                        }

                        continue;
                    }

                    // MyDefinitionManager.ProcessContentFilePath()
                    const string fileDoesNotHaveProperExtension = "File does not have a proper extension: ";
                    if(error.Message.StartsWith(resourceNotFoundPrefix))
                    {
                        string filePath = error.Message.Substring(fileDoesNotHaveProperExtension.Length, error.Message.Length - fileDoesNotHaveProperExtension.Length);

                        if(filePath.StartsWith("Audio", StringComparison.OrdinalIgnoreCase))
                        {
                            error.Severity = TErrorSeverity.Notice; // reduce severity
                            error.Message += $"\n{AppendMsg}Ignore this particular one, modder wants to use sound from the game folder and omitting extension is the only way to do that.";
                        }

                        continue;
                    }

                    // MyDefinitionManager.ProcessContentFilePath()
                    const string fileExtensionNotSupported = "File extension of: ";
                    if(error.Message.StartsWith(fileExtensionNotSupported))
                    {
                        const string tail = " is not supported.";
                        string filePath = error.Message.Substring(fileExtensionNotSupported.Length, error.Message.Length - fileExtensionNotSupported.Length - tail.Length);
                        string extension = Path.GetExtension(filePath);

                        if(!string.IsNullOrEmpty(extension))
                        {
                            bool anyUpperCase = false;
                            foreach(var c in extension)
                            {
                                if(char.IsUpper(c))
                                {
                                    anyUpperCase = true;
                                    break;
                                }
                            }

                            if(anyUpperCase)
                            {
                                error.Message += $"\n{AppendMsg}Game expects all lower-case extensions, try {extension.ToLower()}.";
                            }
                        }

                        continue;
                    }

                    // MyDefinitionManager.PostprocessBlueprints()
                    if(error.Message.StartsWith("Following blueprints could not be post-processed"))
                    {
                        error.ErrorFile = null; // file is misleading in this one
                        error.Message += $"\n{AppendMsg}Usually means a result item was not found.";
                        continue;
                    }

                    // MyDefinitionManager.InitSpawnGroups()
                    if(error.Message.StartsWith("Error loading spawn group"))
                    {
                        error.Message += $"\n{AppendMsg}Means the spawn group has no prefabs or 0 spawn radius or 0 frequency.";
                        continue;
                    }

                    // MyDefinitionManager.MakeBlueprintFromComponentStack()
                    if(error.Message.StartsWith("Could not find component blueprint for"))
                    {
                        error.ErrorFile = null; // file is misleading in this one
                        error.Message += $"\n{AppendMsg}All components must have a blueprint otherwise game crashes when using tools on the block using the component."
                                       + LineSignature + "You can have a blueprint without it being used though, just don't add it to a BlueprintClass."
                                       + LineSignature + "See ZoneChip's blueprint as an example.";
                        continue;
                    }

                    // MyDefinitionManager.DefinitionDictionary<V>
                    if(error.Message == "Invalid definition id")
                    {
                        error.Message += $"\n{AppendMsg}Very vague one indeed, it means you have a <TypeId> that is made up, you have to use an existing one.";
                        continue;
                    }

                    // MyDefinitionManager.FailModLoading() without any stacktrace most likely means an XML syntax error.
                    if(error.Message == "MOD SKIPPED, Cannot load definition file")
                    {
                        error.Message += $"\n{AppendMsg}Most likely an XML syntax error. Look in the SpaceEngineers.log file for 'Exception during objectbuilder'";
                        continue;
                    }

                    // MyDefinitionManager.FailModLoading() + a NRE stacktrace
                    if(error.Message.StartsWith(phase6error))
                    {
                        error.Message += $"\n{AppendMsg}This error is caused by various issues, attempting to identify the issue...";

                        MyObjectBuilder_Checkpoint.ModItem? mod = GetModItemFromName(error.ModName);

                        if(mod == null)
                        {
                            //Log.Error($"Couldn't find mod '{error.ModName}' in worlds' mods list - was needed for getting details on a definition error about it.");
                            error.Message += LineSignature + "Could not find the mod in the mods list.";
                            continue;
                        }

                        string identified = null;

                        if(mod.Value.PublishedFileId != 0)
                        {
                            error.Message += LineSignature + "Mod is published, no checks can be made. Possible issues are:"
                                           + LineSignature + " - No 'Data' folder directly in the mod (zipping the mod requires the Data folder to be there as soon as you open it)."
                                           + LineSignature + " - Download corruption, if the mod works fine for other people then re-download it by: exit game, unsub from mod, wait for steam to ''update SE'', re-sub and try again in game.";
                        }
                        else // local mod
                        {
                            string modPath = mod.Value.GetPath();

                            if(identified == null && !DirectoryExists(modPath))
                            {
                                identified = "Mod folder not found, it was removed or renamed";
                            }

                            if(identified == null && !DirectoryExists(Path.Combine(modPath, "Data")))
                            {
                                identified = "'Data' folder does not exist in mod root folder";
                            }

                            if(identified == null)
                            {
                                error.Message += LineSignature + "Mod folder and Data folder seem to exist, no other known issues to check for.";
                            }
                        }

                        if(identified != null)
                        {
                            error.Message += LineSignature + "The issue is: " + identified;
                        }

                        continue;
                    }

                    if(error.Message.Contains(blockInitError))
                    {
                        error.Message += $"\n{AppendMsg}Issue is in the common tags on block definition, possible causes:"
                                      + LineSignature + " - missing the <CriticalComponent tag"
                                      + LineSignature + " - one or more components don't exist (if component is in another mod, ensure that mod loads first, lower in the mods window)";
                        continue;
                    }

                    // MyDefinitionManager.InitCharacters()
                    if(error.Message.StartsWith("Invalid character Id found in mod"))
                    {
                        error.Message += $"\n{AppendMsg}Invalid TypeId in particular, it does not exist.";
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// Only works in local mods folder or <Game>/Content/Data folder.
        /// Relative paths will be assumed in local mods folder.
        /// </summary>
        static bool DirectoryExists(string path)
        {
            // HACK: no proper way to detect if a directory exists
            try
            {
                string[] files = PathUtils.GetFilesRecursively(path, "dontcareaboutanyfiles");
            }
            catch(Exception e)
            {
                if(e.GetType().Name == "DirectoryNotFoundException") // because the class is prohibited
                {
                    return false;
                }
                else
                {
                    Log.Error(e);
                }
            }

            return true;
        }

        static MyObjectBuilder_Checkpoint.ModItem? GetModItemFromName(string modName)
        {
            foreach(MyObjectBuilder_Checkpoint.ModItem modItem in MyAPIGateway.Session.Mods)
            {
                if(modItem.Name == modName)
                    return modItem;
            }

            return null;
        }

        void CheckErrorsOnF11() // called when pressing F11 in offline world
        {
            ListReader<MyDefinitionErrors.Error> errors = MyDefinitionErrors.GetErrors();

            for(int i = 0; i < errors.Count; i++)
            {
                MyDefinitionErrors.Error error = errors[i];

                // NOTE: use == or some other way to prevent detection after it was already appended-to.

                // HACK MyCubeGrid.TestBlockPlacementArea(), falsely triggered by MyModel.LoadData() via m_loadingErrorProcessed=false
                if(error.Severity == TErrorSeverity.Error && error.Message == "There was error during loading of model, please check log file.")
                {
                    error.Severity = TErrorSeverity.Notice; // reduce its severity
                    error.Message += $"\n{AppendMsg}This is a meaningless error that always happens in a certain context, ignore it.";
                }

                // from MyScriptManager.TryAddEntityScripts()
                if(error.Message == "Possible entity type script logic collision")
                {
                    error.Message += $"\n{AppendMsg}This just means multiple mods have GameLogic components on the same blocks, it's not very useful nor does it mean they collide."
                                    + LineSignature + "Ignore this and test the mod functions themselves if you are worried they're colliding.";
                }

                // from MyScriptManager.TryAddEntityScripts()
                //if(error.Message.Contains("is using the obsolete MyEntityComponentDescriptor overload!"))
                //{
                //}
            }
        }

        /// <summary>
        /// Find .sbc files that are not lower case ".sbc", making the game not load them.
        /// </summary>
        void CheckModFiles()
        {
            foreach(MyObjectBuilder_Checkpoint.ModItem modItem in MyAPIGateway.Session.Mods)
            {
                if(modItem.PublishedFileId != 0)
                    continue; // skip published mods because PathUtils.GetFilesRecursively doesn't work with those.

                if(modItem.Name == Main.Session.ModContext.ModName)
                    continue;

                MyModContext modContext = (MyModContext)modItem.GetModContext();
                string dataPath = modContext.ModPathData;

                // NOTE: this only allows to read in game folder and local mods folder, therefore it cannot work on published mods.
                string[] files = PathUtils.GetFilesRecursively(dataPath, "*");

                foreach(string filePath in files)
                {
                    string ext = Path.GetExtension(filePath);

                    if(ext != ".sbc" && ext.Equals(".sbc", StringComparison.OrdinalIgnoreCase))
                    {
                        modContext.CurrentFile = filePath;
                        ModProblem(modContext, $"This sbc file won't be loaded! Must be all lower case '.sbc' extension to get picked up.");
                    }
                }
            }
        }

        class ModHintData
        {
            public readonly MyModContext ModContext;

            public readonly Dictionary<string, List<MyCubeBlockDefinition>> RecommendIsAirTightTrue = new Dictionary<string, List<MyCubeBlockDefinition>>();
            public readonly Dictionary<string, List<MyCubeBlockDefinition>> RecommendIsAirTightFalse = new Dictionary<string, List<MyCubeBlockDefinition>>();

            /// <summary>
            /// Context gets copied because each definition has its own instance with the file
            /// </summary>
            public ModHintData(MyModContext context)
            {
                ModContext = new MyModContext();
                ModContext.Init(context);
                ModContext.CurrentFile = "(see below)";
            }
        }

        void CheckModDefinitions()
        {
            Dictionary<string, ModHintData> modHints = new Dictionary<string, ModHintData>();
            HashSet<string> voxelPlacementAlerted = new HashSet<string>();

            foreach(MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                if(def == null)
                    continue;

                if(def.Context == null)
                {
                    // HACK: the generated LCD texture defs by the game have null Context
                    if(!(def is MyLCDTextureDefinition))
                        ModHint(def.Context, $"'{GetDefId(def)}' has null Context, a script probably set it like this?");

                    continue;
                }

                if(!CheckEverything)
                {
                    // ignore untouched definitions
                    if(def.Context.IsBaseGame)
                        continue;

                    // ignore workshop mods
                    if(def.Context.ModItem.PublishedFileId != 0)
                        continue;
                }

                if(def.Id.SubtypeId == MyStringHash.NullOrEmpty)
                {
                    ModHint(def, "has empty subtype, is this intended?");
                }

                MyCubeBlockDefinition blockDef = def as MyCubeBlockDefinition;
                if(blockDef != null)
                {
                    if(blockDef.Size.X <= 0 || blockDef.Size.Y <= 0 || blockDef.Size.Z <= 0)
                    {
                        ModProblem(def, "has Size 0 or negative!");
                    }
                    else
                    {
                        Vector3I maxCenter = blockDef.Size - 1;

                        // TODO find a way to check if Center has changed in same game session and warn!
                        /* CONTEXT: I confirmed it too, it's because keen are processing conveyor stuff relative to <Center> and 
                         * storing them in a static dictionary (MyConveyorLine.m_blockLinePositions).
                         * reloading world won't clear them so they persist until you restart game.
                         * and if you change <Center> without restarting, it breaks conveyors 😆
                         */
                        // maybe also try to identify if a model that has empties was changed?

                        if(blockDef.Center.X < 0 || blockDef.Center.Y < 0 || blockDef.Center.Z < 0)
                        {
                            ModProblem(def, "has negative values for Center tag! This will break some mountpoints and various other weird issues.");
                        }
                        else if(blockDef.Center.X > maxCenter.X || blockDef.Center.Y > maxCenter.Y || blockDef.Center.Z > maxCenter.Z)
                        {
                            ModProblem(def, "has too high values for Center tag! It should be at most Size - 1. This will break some mountpoints and various other weird issues.");
                        }

                        if(blockDef.MirroringCenter.X < 0 || blockDef.MirroringCenter.Y < 0 || blockDef.MirroringCenter.Z < 0)
                        {
                            ModProblem(def, "has negative values for MirroringCenter tag!");
                        }
                        else if(blockDef.MirroringCenter.X > maxCenter.X || blockDef.MirroringCenter.Y > maxCenter.Y || blockDef.MirroringCenter.Z > maxCenter.Z)
                        {
                            ModProblem(def, "has too high values for MirroringCenter tag! It should be at most Size - 1.");
                        }

                        // check mountpoints on the block itself and on the build progress models
                        {
                            if(blockDef.BuildProgressModels != null && blockDef.BuildProgressModels.Length > 0)
                            {
                                for(int i = 0; i < blockDef.BuildProgressModels.Length; i++)
                                {
                                    MyCubeBlockDefinition.BuildProgressModel bpm = blockDef.BuildProgressModels[i];
                                    CheckMountpoints(blockDef, bpm.MountPoints, $"buildstage #{i + 1}, mountpoint");
                                }
                            }

                            if(blockDef.MountPoints != null && blockDef.MountPoints.Length > 0)
                            {
                                CheckMountpoints(blockDef, blockDef.MountPoints, "mountpoint");
                            }
                        }
                    }

                    if(blockDef.BlockTopology == MyBlockTopology.Cube)
                    {
                        if(blockDef.CubeDefinition == null)
                        {
                            if(string.IsNullOrEmpty(blockDef.Model))
                                ModProblem(def, "has BlockTopology set to Cube but no CubeDefinition!");
                            else
                                ModProblem(def, "has Model + BlockTopology=Cube + no CubeDefinition, are you sure you're trying to make a deformable armor? If not then use: <BlockTopology>TriangleMesh</BlockTopology> because it will cause issues, the block will go invisible once painted for example.");
                        }
                        else
                        {
                            if(blockDef.CubeDefinition.Model == null || blockDef.CubeDefinition.Model.Length == 0)
                            {
                                ModProblem(def, "has BlockTopology set to Cube but no models in CubeDefinition!");
                            }
                        }
                    }
                    else if(blockDef.BlockTopology == MyBlockTopology.TriangleMesh)
                    {
                        if(string.IsNullOrEmpty(blockDef.Model))
                        {
                            ModProblem(def, "has BlockTopology set to TriangleMesh but Model is not defined!");
                        }
                    }
                    else
                    {
                        ModHint(def, $"has BlockTopology set to unknown: {blockDef.BlockTopology}; inform BuildInfo author to update their stuff! :]");
                    }

                    MyComponentStack comps = new MyComponentStack(blockDef, MyComponentStack.MOUNT_THRESHOLD, MyComponentStack.MOUNT_THRESHOLD);
                    MyComponentStack.GroupInfo firstComp = comps.GetGroupInfo(0);
                    if(firstComp.TotalCount > 1 && firstComp.MountedCount > 1)
                    {
                        ModProblem(def, $"item duplicate exploitable! Block gets placed in survival with {firstComp.MountedCount} of the {firstComp.Component?.DisplayNameText} (first component)."
                                      + $"\nThis allows players to duplicate the item by placing the block (which consumes only one) and then grinding it to get back {firstComp.MountedCount} !"
                                      + "\nFix by reordering components or changing amounts of other components or making a single component as first stack.");
                    }

                    if(blockDef.DisplayNameText == null)
                    {
                        ModProblem(def, "does not have a DisplayName which can cause mod scripts to crash and players to be confused.");
                    }

                    //if((blockDef.DisplayNameEnum != null && blockDef.BlockPairName == blockDef.DisplayNameEnum.Value.String)
                    //|| (blockDef.BlockPairName == blockDef.DisplayNameString))
                    //{
                    //    ModHint(def, "does not have a BlockPairName defined therefore it defaults to the DisplayName."
                    //               + "\nRecommended to set it properly, similar to SubtypeId but without the grid size prefix/suffix if any.");
                    //}

                    // HACK: hardcoded definitions that have ResourceSinkGroup
                    // last generated from SE v1.205.024
                    if(def is MySafeZoneBlockDefinition) CheckResourceGroup(def, ((MySafeZoneBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyAdvancedDoorDefinition) CheckResourceGroup(def, ((MyAdvancedDoorDefinition)def).ResourceSinkGroup);
                    if(def is MyAirtightDoorGenericDefinition) CheckResourceGroup(def, ((MyAirtightDoorGenericDefinition)def).ResourceSinkGroup);
                    if(def is MyAirVentDefinition) CheckResourceGroup(def, ((MyAirVentDefinition)def).ResourceSinkGroup);
                    if(def is MyBatteryBlockDefinition) CheckResourceGroup(def, ((MyBatteryBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyBeaconDefinition) CheckResourceGroup(def, ((MyBeaconDefinition)def).ResourceSinkGroup);
                    if(def is MyBroadcastControllerDefinition) CheckResourceGroup(def, ((MyBroadcastControllerDefinition)def).ResourceSinkGroup);
                    if(def is MyButtonPanelDefinition) CheckResourceGroup(def, ((MyButtonPanelDefinition)def).ResourceSinkGroup);
                    if(def is MyCameraBlockDefinition) CheckResourceGroup(def, ((MyCameraBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyConveyorSorterDefinition) CheckResourceGroup(def, ((MyConveyorSorterDefinition)def).ResourceSinkGroup);
                    if(def is MyCryoChamberDefinition) CheckResourceGroup(def, ((MyCryoChamberDefinition)def).ResourceSinkGroup);
                    if(def is MyDoorDefinition) CheckResourceGroup(def, ((MyDoorDefinition)def).ResourceSinkGroup);
                    if(def is MyEmotionControllerBlockDefinition) CheckResourceGroup(def, ((MyEmotionControllerBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyEventControllerBlockDefinition) CheckResourceGroup(def, ((MyEventControllerBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyFlightMovementBlockDefinition) CheckResourceGroup(def, ((MyFlightMovementBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyGasFueledPowerProducerDefinition) CheckResourceGroup(def, ((MyGasFueledPowerProducerDefinition)def).ResourceSinkGroup);
                    if(def is MyGravityGeneratorBaseDefinition) CheckResourceGroup(def, ((MyGravityGeneratorBaseDefinition)def).ResourceSinkGroup);
                    if(def is MyGyroDefinition) CheckResourceGroup(def, ((MyGyroDefinition)def).ResourceSinkGroup);
                    if(def is MyJumpDriveDefinition) CheckResourceGroup(def, ((MyJumpDriveDefinition)def).ResourceSinkGroup);
                    if(def is MyLaserAntennaDefinition) CheckResourceGroup(def, ((MyLaserAntennaDefinition)def).ResourceSinkGroup);
                    if(def is MyLCDPanelsBlockDefinition) CheckResourceGroup(def, ((MyLCDPanelsBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyLightingBlockDefinition) CheckResourceGroup(def, ((MyLightingBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyMedicalRoomDefinition) CheckResourceGroup(def, ((MyMedicalRoomDefinition)def).ResourceSinkGroup);
                    if(def is MyMotorStatorDefinition) CheckResourceGroup(def, ((MyMotorStatorDefinition)def).ResourceSinkGroup);
                    if(def is MyOreDetectorDefinition) CheckResourceGroup(def, ((MyOreDetectorDefinition)def).ResourceSinkGroup);
                    if(def is MyOxygenFarmDefinition) CheckResourceGroup(def, ((MyOxygenFarmDefinition)def).ResourceSinkGroup);
                    if(def is MyParachuteDefinition) CheckResourceGroup(def, ((MyParachuteDefinition)def).ResourceSinkGroup);
                    if(def is MyPistonBaseDefinition) CheckResourceGroup(def, ((MyPistonBaseDefinition)def).ResourceSinkGroup);
                    if(def is MyPoweredCargoContainerDefinition) CheckResourceGroup(def, ((MyPoweredCargoContainerDefinition)def).ResourceSinkGroup);
                    if(def is MyProductionBlockDefinition) CheckResourceGroup(def, ((MyProductionBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyProgrammableBlockDefinition) CheckResourceGroup(def, ((MyProgrammableBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyProjectorDefinition) CheckResourceGroup(def, ((MyProjectorDefinition)def).ResourceSinkGroup);
                    if(def is MyRadioAntennaDefinition) CheckResourceGroup(def, ((MyRadioAntennaDefinition)def).ResourceSinkGroup);
                    if(def is MyRemoteControlDefinition) CheckResourceGroup(def, ((MyRemoteControlDefinition)def).ResourceSinkGroup);
                    if(def is MySearchlightDefinition) CheckResourceGroup(def, ((MySearchlightDefinition)def).ResourceSinkGroup);
                    if(def is MySensorBlockDefinition) CheckResourceGroup(def, ((MySensorBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyShipDrillDefinition) CheckResourceGroup(def, ((MyShipDrillDefinition)def).ResourceSinkGroup);
                    if(def is MySoundBlockDefinition) CheckResourceGroup(def, ((MySoundBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyTextPanelDefinition) CheckResourceGroup(def, ((MyTextPanelDefinition)def).ResourceSinkGroup);
                    if(def is MyThrustDefinition) CheckResourceGroup(def, ((MyThrustDefinition)def).ResourceSinkGroup);
                    if(def is MyTimerBlockDefinition) CheckResourceGroup(def, ((MyTimerBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyTransponderBlockDefinition) CheckResourceGroup(def, ((MyTransponderBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyTurretControlBlockDefinition) CheckResourceGroup(def, ((MyTurretControlBlockDefinition)def).ResourceSinkGroup);
                    if(def is MyVirtualMassDefinition) CheckResourceGroup(def, ((MyVirtualMassDefinition)def).ResourceSinkGroup);
                    if(def is MyWeaponBlockDefinition) CheckResourceGroup(def, ((MyWeaponBlockDefinition)def).ResourceSinkGroup);

                    if(blockDef.IsAirTight == null)
                    {
                        int airTightFaces, toggledAirTightFaces, totalFaces;
                        AirTightMode airTight = Pressurization.GetAirTightFaces(blockDef, out airTightFaces, out toggledAirTightFaces, out totalFaces);

                        if(airTightFaces == 0 || airTightFaces == totalFaces)
                        {
                            string modId = def.Context.IsBaseGame ? "<BaseGame>" : (def.Context.ModId ?? "Unknown");
                            ModHintData hintData;
                            if(!modHints.TryGetValue(modId, out hintData))
                            {
                                hintData = new ModHintData(blockDef.Context); // clones modcontext without file data
                                modHints[modId] = hintData;
                            }

                            string file = blockDef?.Context?.CurrentFile ?? "(UnknownFile)";
                            if(airTightFaces == 0)
                            {
                                hintData.RecommendIsAirTightFalse.GetValueOrNew(file).Add(blockDef);
                            }
                            else if(airTightFaces == totalFaces)
                            {
                                hintData.RecommendIsAirTightTrue.GetValueOrNew(file).Add(blockDef);
                            }
                        }
                    }

                    if(blockDef.IsStandAlone && !blockDef.HasPhysics)
                    {
                        ModProblem(def, "uses HasPhysics=false and IsStandAlone=true which allows player to place free-floating and then can't be grinded anymore!");
                    }

                    if(blockDef.IsStandAlone && blockDef.PhysicsOption == MyPhysicsOption.None)
                    {
                        ModProblem(def, "uses PhysicsOption=None and IsStandAlone=true which allows player to place free-floating and then can't be grinded anymore!");
                    }

                    if(blockDef.VoxelPlacement.HasValue)
                    {
                        var vp = blockDef.VoxelPlacement.Value;

                        bool firstTimeForThisMod = voxelPlacementAlerted.Add(blockDef.Context.ModId);
                        if(firstTimeForThisMod)
                        {
                            if((vp.StaticMode.PlacementMode == VoxelPlacementMode.Volumetric && vp.StaticMode.MinAllowed > vp.StaticMode.MaxAllowed)
                            || (vp.DynamicMode.PlacementMode == VoxelPlacementMode.Volumetric && vp.DynamicMode.MinAllowed > vp.DynamicMode.MaxAllowed))
                            {
                                ModHint(def, $"VoxelPlacement's MinAllowed is larger than MaxAllowed, is this really intended? If so please let me (Digi) know of the intended effect!");
                            }
                        }
                    }

                    MyThrustDefinition thrustDef = def as MyThrustDefinition;
                    if(thrustDef != null)
                    {
                        if(thrustDef.EffectivenessAtMinInfluence > 1f || thrustDef.EffectivenessAtMaxInfluence > 1f)
                        {
                            float maxEff = Math.Max(thrustDef.EffectivenessAtMinInfluence, thrustDef.EffectivenessAtMaxInfluence);

                            StringBuilder sb = new StringBuilder(600);
                            sb.Append("EffectivenessAtMinInfluence/EffectivenessAtMaxInfluence are above 1, while it will mostly work it's also unexpected even by the game (shows wrong power consumption) and will cause issues with other mods and PB scripts too.");
                            sb.Append("\nIf you wish to fix without affecting thrust behavior, here's the values normalized:");
                            sb.Append("\n<ForceMagnitude>").Append(thrustDef.ForceMagnitude * maxEff).Append("</ForceMagnitude>");
                            sb.Append("\n<MaxPowerConsumption>").Append(thrustDef.MaxPowerConsumption * maxEff).Append("</MaxPowerConsumption>");
                            sb.Append("\n<EffectivenessAtMinInfluence>").Append(thrustDef.EffectivenessAtMinInfluence / maxEff).Append("</EffectivenessAtMinInfluence>");
                            sb.Append("\n<EffectivenessAtMaxInfluence>").Append(thrustDef.EffectivenessAtMaxInfluence / maxEff).Append("</EffectivenessAtMaxInfluence>");
                            sb.Append("\n(You can find these written in the SE log too for easy copy)");

                            ModHint(def, sb.ToString());
                        }

                        if(Math.Abs(thrustDef.SlowdownFactor - 1f) > 0.0000001f)
                        {
                            ModHint(def, $"<SlowdownFactor> is {thrustDef.SlowdownFactor}!"
                                       + "\nThis forces all thrusters on the same grid to use the highest value SlowdownFactor."
                                       + "\nRecommended to set as 1.");
                        }
                    }

                    continue;
                }

                MyComponentDefinitionBase compDef = def as MyComponentDefinitionBase;
                if(compDef != null)
                {
                    // last generated from SE v1.205.024

                    //if(def is MyAiBlockPowerComponentDefinition) CheckResourceGroup(def, ((MyAiBlockPowerComponentDefinition)def).ResourceSinkGroup);

                    // HACK: MyAiBlockPowerComponentDefinition is not whitelisted
                    if(compDef.GetType().Name == "MyAiBlockPowerComponentDefinition")
                    {
                        var ob = (MyObjectBuilder_AiBlockPowerComponentDefinition)compDef.GetObjectBuilder();
                        CheckResourceGroup(compDef, ob.ResourceSinkGroup);
                    }

                    continue;
                }

                MyRespawnShipDefinition respawnShipDef = def as MyRespawnShipDefinition;
                if(respawnShipDef != null)
                {
                    // game does not alert if this is the case, you only find out when you try to use it and it infinitely streams.
                    if(respawnShipDef.Prefab == null)
                    {
                        MyDefinitionErrors.Add(def.Context, $"{CustomMsg}RespawnShip '{def.Id.SubtypeName}' does not point to a valid prefab!"
                                                           + "\nMake sure to input the SubtypeId from inside the prefab file. Like all SBCs, the file name does not matter!", TErrorSeverity.Error);
                    }

                    continue;
                }

                MySpawnGroupDefinition spawnGroup = def as MySpawnGroupDefinition;
                if(spawnGroup != null)
                {
                    // MyNeutralShipSpawner & IsCargoShip - cargo ships
                    // MyEncounterGenerator & IsEncounter - random encounters
                    // MyGlobalEncountersGenerator & IsGlobalEncounter - global encounters (factorum)
                    // MyPlanetaryEncountersGenerator & IsPlanetaryEncounter - planetary encounters
                    // MyStationCellGenerator - economy trade stations, which are not spawngroups but MyObjectBuilder_StationsListDefinition

                    long discard;

                    if(spawnGroup.IsCargoShip || spawnGroup.IsEncounter || spawnGroup.IsGlobalEncounter || spawnGroup.IsPlanetaryEncounter)
                    {
                        if(!spawnGroup.TryGetOwnerId(out discard))
                        {
                            ModProblem(spawnGroup, "Could not resolve owner, resulting in this not getting spawned by CargoShips, Encounters, GlobalEncounters or PlanetaryEncounters.");
                        }
                    }

                    var enemies = spawnGroup.HostileSubEncounters;
                    if(enemies != null && enemies.Count > 0)
                    {
                        foreach(var enemy in enemies)
                        {
                            MySpawnGroupDefinition sg;
                            if(MyDefinitionManager.Static.TryGetSpawnGroupDefinition(enemy.SubtypeId, out sg))
                            {
                                if(!sg.TryGetOwnerId(out discard, isGlobalSubEncounter: spawnGroup.IsGlobalEncounter))
                                {
                                    ModProblem(spawnGroup, $"HostileSubEncounters's '{enemy.SubtypeId}' cannot resolve owner, resulting in this sub-encounter not spawning with the primary encounter.");
                                }
                            }
                            else
                            {
                                ModHint(spawnGroup, $"HostileSubEncounters's '{enemy.SubtypeId}' does not exist as a SpawnGroup definition.");
                            }
                        }
                    }

                    // spawnGroup.FactionSubEncounters does not call TryGetOwnerId() as they're same owner as the primary encounter
                }
            }

            if(modHints.Count > 0)
            {
                StringBuilder sb = new StringBuilder(1024);

                foreach(ModHintData modData in modHints.Values)
                {
                    CombineIsAirTightHints(sb, modData, true);
                    CombineIsAirTightHints(sb, modData, false);
                }
            }
        }

        void CheckResourceGroup(MyDefinitionBase def, string group) => CheckResourceGroup(def, MyStringHash.GetOrCompute(group));

        void CheckResourceGroup(MyDefinitionBase def, MyStringHash group)
        {
            if(group == MyStringHash.NullOrEmpty)
            {
                ModHint(def, $"does not have ResourceSinkGroup defined which means it will be in the last power priority group. Refer to ResourceDistributionGroups.sbc for subtypes to use.");
            }
            else if(!Main.Constants.ResourceGroupPriority.ContainsKey(group))
            {
                ModProblem(def, $"the ResourceSinkGroup '{group.String}' does not exist which can cause issues with block working. Refer to ResourceDistributionGroups.sbc for subtypes to use.");
            }
        }

        void CheckMountpoints(MyCubeBlockDefinition blockDef, MyCubeBlockDefinition.MountPoint[] mountpoints, string identifier = "mountpoint")
        {
            if(mountpoints == null || mountpoints.Length == 0)
                return;

            for(int i = 0; i < mountpoints.Length; i++)
            {
                MyCubeBlockDefinition.MountPoint mp = mountpoints[i];
                if(!mp.Enabled)
                    continue;

                // TODO: also check these in overlays to color them different? maybe gray since it's not supposed to be used...

                BlockSideEnum side = MyCubeBlockDefinition.NormalToBlockSide(mp.Normal);
                Vector2I sizeRelative;
                switch(side)
                {
                    // only accurate for the purposes of seeing if mountpoint is out of bounds
                    case BlockSideEnum.Front:
                    case BlockSideEnum.Back: sizeRelative = new Vector2I(blockDef.Size.X, blockDef.Size.Y); break;
                    case BlockSideEnum.Left:
                    case BlockSideEnum.Right: sizeRelative = new Vector2I(blockDef.Size.Z, blockDef.Size.Y); break;
                    case BlockSideEnum.Top:
                    case BlockSideEnum.Bottom: sizeRelative = new Vector2I(blockDef.Size.X, blockDef.Size.Z); break;
                    default: ModProblem(blockDef, $"{identifier} #{i + 1} has unknown side: {side}"); continue;
                }

                Vector3 start, end; // ignore Z on these
                Hardcoded.UntransformMountPointPosition(ref mp.Start, (int)side, blockDef.Size, out start);
                Hardcoded.UntransformMountPointPosition(ref mp.End, (int)side, blockDef.Size, out end);

                // MyCubeBlockDefinition.SetMountPoints() does some offsets making mountpoints ever so slightly smaller, this is good here so we don't need to undo

                if(start.X < 0 || start.Y < 0 || end.X < 0 || end.Y < 0
                || start.X > sizeRelative.X || start.Y > sizeRelative.Y || end.X > sizeRelative.X || end.Y > sizeRelative.Y)
                {
                    ModProblem(blockDef, $"{identifier} #{i + 1} (1 is first) is outside of the block! Start or End are either below 0 or above Size." +
                                         "\nBlock can be placed but get detached when grid updates from any block being removed.");
                }
            }
        }

        void CombineIsAirTightHints(StringBuilder sb, ModHintData modData, bool recommendedValue)
        {
            sb.Clear();

            if(recommendedValue)
                sb.Append("Blocks have all faces airtight but IsAirTight is null, recommended to set it true.");
            else
                sb.Append("Blocks have 0 faces airtight but IsAirTight is null, recommended to set it false.");

            sb.Append("\nPressurization check for blocks exits early if IsAirTight is true or false, making the pressurization ever so slightly faster.");
            sb.Append("\nBlocks found:");

            int startIdx = sb.Length;

            Dictionary<string, List<MyCubeBlockDefinition>> dict = (recommendedValue ? modData.RecommendIsAirTightTrue : modData.RecommendIsAirTightFalse);

            foreach(KeyValuePair<string, List<MyCubeBlockDefinition>> kv in dict)
            {
                sb.Append("\n  - File '");

                string filePath = kv.Key;
                if(filePath != null)
                {
                    if(filePath.StartsWith(modData.ModContext.ModPath))
                    {
                        int prefixLen = modData.ModContext.ModPath.Length;
                        sb.Append(filePath, prefixLen, filePath.Length - prefixLen);
                    }
                    else
                        sb.Append(filePath);
                }
                else
                    sb.Append("(Unknown)");

                sb.Append("':");

                foreach(MyCubeBlockDefinition def in kv.Value)
                {
                    string defIdText = def.Id.ToString();

                    sb.Append("\n      - ");

                    const string MyOBPrefix = "MyObjectBuilder_";
                    if(defIdText.StartsWith(MyOBPrefix))
                        sb.Append(defIdText, MyOBPrefix.Length, defIdText.Length - MyOBPrefix.Length);
                    else
                        sb.Append(defIdText);
                }
            }

            if(startIdx < sb.Length) // any data was added
            {
                ModHint(modData.ModContext, sb.ToString());
            }
        }

        public void ModProblem(MyDefinitionBase def, string text)
        {
            string message = $"Problem with '{GetDefId(def)}': {text}";
            MyDefinitionErrors.Add(def.Context, $"{CustomMsg}{message}", TErrorSeverity.Error, writeToLog: false);
            MyLog.Default.WriteLine($"BuildInfo ModderHelp: {message}");
            Log.Info($"[ModderHelp] {message}");
            ModProblems++;
        }

        public void ModProblem(MyModContext context, string text)
        {
            string message = $"Problem: {text}";
            MyDefinitionErrors.Add(context, $"{CustomMsg}{message}", TErrorSeverity.Error, writeToLog: false);
            MyLog.Default.WriteLine($"BuildInfo ModderHelp: {message}");
            Log.Info($"[ModderHelp] {message}");
            ModProblems++;
        }

        public void ModHint(MyDefinitionBase def, string text)
        {
            string message = $"Hint for '{GetDefId(def)}': {text}";
            MyDefinitionErrors.Add(def.Context, $"{CustomMsg}{message}", TErrorSeverity.Notice, writeToLog: false);
            MyLog.Default.WriteLine($"BuildInfo ModderHelp: {message}");
            Log.Info($"[ModderHelp] {message}");
            ModHints++;
        }

        public void ModHint(MyModContext context, string text)
        {
            string message = $"Hint: {text}";
            MyDefinitionErrors.Add(context, $"{CustomMsg}{message}", TErrorSeverity.Notice, writeToLog: false);
            MyLog.Default.WriteLine($"BuildInfo ModderHelp: {message}");
            Log.Info($"[ModderHelp] {message}");
            ModHints++;
        }

        static string GetDefId(MyDefinitionBase def)
        {
            return def.Id.ToString().Replace("MyObjectBuilder_", "");
        }

        public override void UpdateAfterSim(int tick)
        {
            if(Main.Config.ModderHelpAlerts.Value && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.F11) && IsF11MenuAccessible)
            {
                CheckErrorsOnF11();
            }
        }

        void FirstSpawn()
        {
            if(Main.Config.ModderHelpAlerts.Value)
            {
                if(IsF11MenuAccessible)
                    SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true); // check errors added in realtime

                // F11 menu auto-popped up, don't bother writing to chat
                if(!(IsF11MenuAccessible && F11MenuShownOnLoad))
                {
                    if(CompileErrors) // online for published mods
                        Utils.ShowColoredChatMessage(BuildInfoMod.ModName + " ModderHelp", "Mods have compile errors! See game log for details.", FontsHandler.RedSh);
                    else if(ModProblems > 0) // offline+local mods
                        Utils.ShowColoredChatMessage(BuildInfoMod.ModName + " ModderHelp", "Problems with local mods! See F11 menu.", FontsHandler.YellowSh);
                    else if(DefinitionErrors) // offline+local mods
                        Utils.ShowColoredChatMessage(BuildInfoMod.ModName + " ModderHelp", "Definition errors with mods! See F11 menu.", FontsHandler.YellowSh);
                }
            }

            //if(AlertVideosInChat)
            //{
            //    Utils.ShowColoredChatMessage(BuildInfoMod.ModName, AlertVideosMessage);
            //}
        }

        #region F11 menu backdrop
        void GUIScreenAdded(string screenType)
        {
            if(Main.TextAPI.WasDetected && screenType.EndsWith(ErrorsGUITypeName))
            {
                if(ErrorsMenuBackdrop == null)
                {
                    MyStringId material = Constants.MatUI_Square;
                    //Color color = new Color(41, 54, 62);
                    Color color = new Color(37, 46, 53);

                    ErrorsMenuBackdrop = new HudAPIv2.BillBoardHUDMessage(material, Vector2D.Zero, color, HideHud: false);
                    ErrorsMenuBackdrop.Width = 1000; // fullscreen-est fullscreen xD
                    ErrorsMenuBackdrop.Height = 1000;
                }

                ErrorsMenuBackdrop.Visible = true;

                Main.GameConfig.TempHideHUD(nameof(ModderHelpMain), true);
            }
        }

        void GUIScreenRemoved(string screenType)
        {
            if(ErrorsMenuBackdrop != null && screenType.EndsWith(ErrorsGUITypeName))
            {
                ErrorsMenuBackdrop.Visible = false;
                Main.GameConfig.TempHideHUD(nameof(ModderHelpMain), false);
            }
        }
        #endregion

#if false
        #region Check menu videos existence
        bool AlertVideosInChat = false;
        const string AlertVideosMessage = "If you have black screen & no sound, restore the SE's Videos folder and restart game to fix.";

        /// <summary>
        /// This is done because the game in v204 (up to 204.018 at least) will have black screen and no sound in game worlds if the videos are missing, while main menu and HUD being fine.
        /// </summary>
        void CheckVideos()
        {
            const string VideosShutUpFile = "buildinfo shut up.txt";

            if(!HasVideos())
            {
                string extraMsg = $"If Keen fixed this issue please let me know to remove this! Meanwhile you can add a '{VideosShutUpFile}' in your videos folder to hide the chat message.";
                Log.Info($"WARNING: {AlertVideosMessage}\n{extraMsg}");
                MyLog.Default.WriteLine($"### {BuildInfoMod.ModName} WARNING: {AlertVideosMessage}\n{extraMsg}");

                if(MyAPIGateway.Utilities.FileExistsInGameContent(Path.Combine("Videos", "buildinfo shut up.txt")))
                {
                    Log.Info($"Found {VideosShutUpFile} in Videos folder, not nagging you about it in chat.");
                }
                else
                {
                    AlertVideosInChat = true;
                }
            }
            else
            {
                Log.Info("Checked main menu videos presence, all good! (early warning for black screen if any is missing)");
            }
        }

        static bool HasVideos()
        {
            try
            {
                // the exact files the game is looking for
                string[] bgVideos = MyPerGameSettings.GUI.MainMenuBackgroundVideos;

                if(bgVideos == null || bgVideos.Length == 0)
                {
                    Log.Info("WARNING: 'MyPerGameSettings.GUI.MainMenuBackgroundVideos' is null or has 0 videos. Unsure how this will behave regarding the black screen issue so I'll just mark it as 'no videos'.");
                    return false;
                }

                foreach(string relativePath in bgVideos)
                {
                    if(!MyAPIGateway.Utilities.FileExistsInGameContent(relativePath))
                    {
                        Log.Info($"{relativePath} not found in game folder");
                        return false;
                    }
                }

                return true;
            }
            catch(Exception e)
            {
                if(!e.Message.Contains("DirectoryNotFoundException")) // not whitelisted
                {
                    Log.Error(e);
                }

                return false;
            }
        }
        #endregion
#endif
    }
}