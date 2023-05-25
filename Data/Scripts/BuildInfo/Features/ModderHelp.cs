using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Draygo.API;
using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class ModderHelp : ModComponent
    {
        int ModProblems = 0;
        int ModHints = 0;
        bool DefinitionErrors = false;
        bool CompileErrors = false;
        bool FirstSpawnChecked = false;

        HudState? RevertHud;
        HudAPIv2.BillBoardHUDMessage ErrorsMenuBackdrop;

        const string ErrorsGUITypeName = "MyGuiScreenDebugErrors";
        const string Signature = "[BuildInfo] ";

        public ModderHelp(BuildInfoMod main) : base(main)
        {
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

                CheckErrors(localMods, MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE);

                if(MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE && localMods.Count > 0)
                {
                    CheckMods();
                }

                // show chat alerts on first spawn, if applicable
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
            }
        }

        public override void UnregisterComponent()
        {
            try
            {
                RevertHUDBack();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            Main.GUIMonitor.ScreenAdded -= GUIScreenAdded;
            Main.GUIMonitor.ScreenRemoved -= GUIScreenRemoved;
        }

        [ProtoContract]
        struct HackCloudLayerSettings  // HACK: MyCloudLayerSettings is not whitelisted
        {
            //[ProtoMember(1)] public string Model;
            [ProtoMember(4)] [XmlArrayItem("Texture")] public List<string> Textures;
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
                MyDefinitionErrors.Add(def.Context, $"{Signature}Planet '{def.Id.SubtypeName}' at CloudLayer #{layerNum}: "
                    + "More than 1 texture, game only uses the first Texture tag! (and gives it _cm and _alphamask suffix to read 2 textures)", TErrorSeverity.Warning);
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
            catch(Exception e)
            {
                // game is not gonna add an error for this file path so I gotta add one myself
                MyDefinitionErrors.Add(def.Context, $"{Signature}Planet '{def.Id.SubtypeName}' at CloudLayer #{layerNum}: Invalid path: '{filePath}'"
                    + "\nIf the mod path is added twice then the problem is that the texture exists AND using a new planet generator definition style (compare EarthLike and Pertram start/end tags)."
                    + "\nEither modify definition to use the old style (recommended) or make it so the <Texture> tag doesn't point to an existing file while also having _cm and _alphamask prefixed ones with same name nearby.",
                    TErrorSeverity.Error);
                return;
            }

            bool startsWithModPath = filePath.StartsWith(def.Context.ModPath);

            // HACK: path not always has mod folder in it here, but in errors list it does.
            string pathKey = filePath;
            if(!startsWithModPath)
                pathKey = Path.Combine(def.Context.ModPath, pathKey);

            cloudLayerInfo[pathKey] = new CloudLayerInfo()
            {
                AppendMessage = "Game looks for this texture with _cm and _alphamask suffix, if those exist and they show up in game then this error can be ignored.",
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

            ListReader<MyDefinitionErrors.Error> errors = MyDefinitionErrors.GetErrors();

            for(int i = 0; i < errors.Count; i++)
            {
                MyDefinitionErrors.Error error = errors[i];

                bool isLocal = localMods.Contains(error.ModName);

                if(isLocal)
                {
                    // chat message when LOCAL mods have definition errors
                    DefinitionErrors = true;

                    // HACK: MyCubeBlockDefinition.InitMountPoints() has double standards on undefined mountpoints
                    if(error.Message.StartsWith("Obsolete default definition of mount points in"))
                    {
                        // does not modify it in the log file, because it writes it to log immediately as it's added
                        error.Severity = TErrorSeverity.Error; // escalate it because it's pretty bad
                        error.Message += $"\n{Signature}This means game will generate mountpoints for this block! Add a disabled mountpoint to have no mountpoints on this block.";
                    }
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

                if(f11MenuAccessible)
                {
                    // HACK: hardcoded various MyDefinitionErrors

                    // MyDefinitionManager.ProcessContentFilePath() + cloudlayer stuff from above
                    string resourceNotFoundPrefix = "Resource not found, setting to null. Resource path: ";
                    if(error.Message.StartsWith(resourceNotFoundPrefix))
                    {
                        string filePath = error.Message.Substring(resourceNotFoundPrefix.Length, error.Message.Length - resourceNotFoundPrefix.Length);

                        CloudLayerInfo info;
                        if(cloudLayerInfo.TryGetValue(filePath, out info))
                        {
                            if(!string.IsNullOrEmpty(info.AppendMessage))
                                error.Message += $"\n{Signature}{info.AppendMessage}";

                            if(info.SetSeverity.HasValue)
                                error.Severity = info.SetSeverity.Value;
                        }
                    }

                    // MyDefinitionManager.PostprocessBlueprints()
                    if(error.Message.StartsWith("Following blueprints could not be post-processed"))
                    {
                        error.Message += $"\n{Signature}Usually means a result item was not found.";
                    }

                    // MyDefinitionManager.InitSpawnGroups()
                    if(error.Message.StartsWith("Error loading spawn group"))
                    {
                        error.Message += $"\n{Signature}Means the spawn group has no prefabs or 0 spawn radius or 0 frequency.";
                    }

                    // MyDefinitionManager.MakeBlueprintFromComponentStack()
                    if(error.Message.StartsWith("Could not find component blueprint for"))
                    {
                        error.Message += $"\n{Signature}All components must have a blueprint otherwise game crashes when using tools on the block using the component."
                                       + "\nYou can have a blueprint without it being used though, just don't add it to a BlueprintClass."
                                       + "\nSee ZoneChip's blueprint as an example.";
                    }

                    // MyDefinitionManager.DefinitionDictionary<V>
                    if(error.Message == "Invalid definition id")
                    {
                        error.Message += $"\n{Signature}Very vague one indeed, it means you have a <TypeId> that is made up, you have to use an existing one.";
                    }
                }
            }
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
                    error.Message += $"\n{Signature}This is a meaningless error that always happens in a certain context, ignore it.";
                }

                // from MyScriptManager.TryAddEntityScripts()
                if(error.Message == "Possible entity type script logic collision")
                {
                    error.Message += $"\n{Signature}This just means multiple mods have GameLogic components on the same blocks, it's not very useful nor does it mean they collide."
                                    + "\nIgnore this and test the mod functions themselves if you are worried they're colliding.";
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

        void CheckMods()
        {
            Dictionary<string, ModHintData> modHints = new Dictionary<string, ModHintData>();

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

                if(!BuildInfoMod.IsDevMod)
                {
                    // ignore untouched definitions
                    if(def.Context.IsBaseGame)
                        continue;

                    // ignore workshop mods
                    if(def.Context.ModItem.PublishedFileId != 0)
                        continue;
                }

                MyCubeBlockDefinition blockDef = def as MyCubeBlockDefinition;
                if(blockDef != null)
                {
                    Vector3I maxCenter = blockDef.Size - 1;

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

                    MyComponentStack comps = new MyComponentStack(blockDef, MyComponentStack.MOUNT_THRESHOLD, MyComponentStack.MOUNT_THRESHOLD);
                    MyComponentStack.GroupInfo firstComp = comps.GetGroupInfo(0);
                    if(firstComp.TotalCount > 1 && firstComp.MountedCount > 1)
                    {
                        ModProblem(def, $"exploitable! Block gets placed in survival with {firstComp.MountedCount}x {firstComp.Component?.DisplayNameText} (first componment), allowing players to grind it to gain those extra components." +
                                        "\nFix by reordering components or changing amounts of other components or making a single component as first stack.");
                    }

                    // the basegame condition is for IsDevMod=true case to not spam myself
                    if(!blockDef.IsAirTight.HasValue && !def.Context.IsBaseGame)
                    {
                        int airTightFaces, totalFaces;
                        AirTightMode airTight = Utils.GetAirTightFaces(blockDef, out airTightFaces, out totalFaces);

                        if(airTightFaces == 0 || airTightFaces == totalFaces)
                        {
                            ModHintData hintData;
                            if(!modHints.TryGetValue(def.Context.ModId, out hintData))
                            {
                                hintData = new ModHintData(blockDef.Context); // clones modcontext without file data
                                modHints[def.Context.ModId] = hintData;
                            }

                            if(airTightFaces == 0)
                            {
                                hintData.RecommendIsAirTightFalse.GetOrAdd(blockDef.Context.CurrentFile).Add(blockDef);
                            }
                            else if(airTightFaces == totalFaces)
                            {
                                hintData.RecommendIsAirTightTrue.GetOrAdd(blockDef.Context.CurrentFile).Add(blockDef);
                            }
                        }
                    }

                    continue;
                }

                MyRespawnShipDefinition respawnShipDef = def as MyRespawnShipDefinition;
                if(respawnShipDef != null)
                {
                    // game does not alert if this is the case, you only find out when you try to use it and it infinitely streams.
                    if(respawnShipDef.Prefab == null)
                    {
                        MyDefinitionErrors.Add(def.Context, $"{Signature}RespawnShip '{def.Id.SubtypeName}' does not point to a valid prefab!"
                                                           + "\nMake sure to input the SubtypeId from inside the prefab file. Like all SBCs, the file name does not matter!", TErrorSeverity.Error);
                    }

                    continue;
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

        void ModProblem(MyDefinitionBase def, string text)
        {
            string message = $"Problem with '{GetDefId(def)}': {text}";
            MyDefinitionErrors.Add(def.Context, $"{Signature}{message}", TErrorSeverity.Error);
            ModProblems++;
        }

        void ModHint(MyModContext context, string text)
        {
            string message = $"Hint: {text}";
            MyDefinitionErrors.Add(context, $"{Signature}{message}", TErrorSeverity.Notice);
            ModHints++;
        }

        static string GetDefId(MyDefinitionBase def)
        {
            return def.Id.ToString().Replace("MyObjectBuilder_", "");
        }

        static string GetModName(MyModContext modContext)
        {
            return (modContext != null ? (modContext.IsBaseGame ? "(base game)" : modContext.ModName) : "(unknown)");
        }

        public override void UpdateAfterSim(int tick)
        {
            if(MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE && Main.Config.ModderHelpAlerts.Value && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.F11))
            {
                CheckErrorsOnF11();
            }

            if(tick % Constants.TicksPerSecond != 0)
                return;

            if(!FirstSpawnChecked)
            {
                IMyCharacter character = MyAPIGateway.Session?.Player?.Character;
                if(character != null) // wait until first spawn
                {
                    FirstSpawnChecked = true;

                    // relevant to CheckErrorsOnF11() above
                    if(MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE)
                    {
                        SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                    }

                    if(CompileErrors)
                        Utils.ShowColoredChatMessage(BuildInfoMod.ModName, "ModderHelp: Other mod(s) have compile errors! See SE log for details.", FontsHandler.RedSh);

                    if(ModHints > 0 || ModProblems > 0)
                        Utils.ShowColoredChatMessage(BuildInfoMod.ModName, "ModderHelp: There's problems or hints with local mod(s), see F11 menu.", FontsHandler.YellowSh);
                    else if(DefinitionErrors)
                        Utils.ShowColoredChatMessage(BuildInfoMod.ModName, "ModderHelp: There are definition errors, see F11 menu.", FontsHandler.YellowSh);
                }
            }
        }

        #region F11 menu backdrop
        void GUIScreenAdded(string screenType)
        {
            if(Main.TextAPI.WasDetected && screenType.EndsWith(ErrorsGUITypeName))
            {
                if(ErrorsMenuBackdrop == null)
                {
                    MyStringId material = MyStringId.GetOrCompute("BuildInfo_UI_Square");
                    //Color color = new Color(41, 54, 62);
                    Color color = new Color(37, 46, 53);

                    ErrorsMenuBackdrop = new HudAPIv2.BillBoardHUDMessage(material, Vector2D.Zero, color, HideHud: false);
                    ErrorsMenuBackdrop.Width = 1000; // fullscreen-est fullscreen xD
                    ErrorsMenuBackdrop.Height = 1000;
                }

                ErrorsMenuBackdrop.Visible = true;

                if(RevertHud == null)
                {
                    RevertHud = Main.GameConfig.HudState;
                    MyVisualScriptLogicProvider.SetHudState((int)HudState.OFF, playerId: 0); // playerId=0 shorcircuits to calling it locally
                }
            }
        }

        void GUIScreenRemoved(string screenType)
        {
            if(ErrorsMenuBackdrop != null && screenType.EndsWith(ErrorsGUITypeName))
            {
                ErrorsMenuBackdrop.Visible = false;
                RevertHUDBack();
            }
        }

        void RevertHUDBack()
        {
            if(RevertHud != null)
            {
                MyVisualScriptLogicProvider.SetHudState((int)RevertHud, playerId: 0);
                RevertHud = null;
            }
        }
        #endregion
    }
}