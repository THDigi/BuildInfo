using System;
using System.Collections.Generic;
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

                CheckErrors(localMods);

                if(MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE && localMods.Count > 0)
                {
                    CheckMods();
                }

                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
            }
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            Main.GUIMonitor.ScreenAdded -= GUIScreenAdded;
            Main.GUIMonitor.ScreenRemoved -= GUIScreenRemoved;

            RevertHUDBack();
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
            public string Message;
            public TErrorSeverity? SetSeverity;
        }

        void CheckErrors(HashSet<string> localMods)
        {
            #region Check cloud layer textures
            Dictionary<string, CloudLayerInfo> cloudLayerTextureMessage = new Dictionary<string, CloudLayerInfo>();

            Dictionary<MyStringHash, MyDefinitionBase> planets;
            if(MyDefinitionManager.Static.Definitions.Definitions.TryGetValue(typeof(MyObjectBuilder_PlanetGeneratorDefinition), out planets))
            {
                foreach(MyPlanetGeneratorDefinition def in planets.Values)
                {
                    if(def?.Context == null || def.Context.IsBaseGame)
                        continue;

                    for(int i = 0; i < def.CloudLayers.Count; i++)
                    {
                        // HACK: MyCloudLayerSettings is not whitelisted
                        byte[] binary = MyAPIGateway.Utilities.SerializeToBinary(def.CloudLayers[i]);
                        HackCloudLayerSettings hackLayer = MyAPIGateway.Utilities.SerializeFromBinary<HackCloudLayerSettings>(binary);

                        if(hackLayer.Textures == null || hackLayer.Textures.Count <= 0)
                            continue;

                        // HACK: from MyCloudRenderer.CreateCloudLayer()

                        if(hackLayer.Textures.Count > 1)
                        {
                            MyDefinitionErrors.Add(def.Context, $"{Signature}Found more than 1 texture in CloudLayer #{(i + 1)}; Game only uses the first texture!", TErrorSeverity.Warning);
                        }

                        string filePath = hackLayer.Textures[0];
                        string pathAM;
                        string pathCM;
                        try
                        {
                            pathAM = filePath.Insert(filePath.LastIndexOf('.'), "_alphamask");
                            pathCM = filePath.Insert(filePath.LastIndexOf('.'), "_cm");
                        }
                        catch(Exception e)
                        {
                            Log.Error($"[ModderHelp] Error processing planet texture '{filePath}' from '{GetModName(def.Context)}' into _alphamask and _cm parts, render will likely crash too.\n{e.ToString()}");
                            cloudLayerTextureMessage[filePath] = new CloudLayerInfo()
                            {
                                Message = "Error processing texture name to add _alphamask and _cm in it, render will likely crash too.",
                                SetSeverity = TErrorSeverity.Error,
                            };
                            continue;
                        }

                        // HACK: game forcefully adds mod path to cloudlayer textures, requiring them to be in mod path; from: MyDefinitionManager.InitPlanetGeneratorDefinitions()
                        bool existsAM = MyAPIGateway.Utilities.FileExistsInModLocation(pathAM, def.Context.ModItem);
                        bool existsCM = MyAPIGateway.Utilities.FileExistsInModLocation(pathCM, def.Context.ModItem);

                        CloudLayerInfo info = new CloudLayerInfo()
                        {
                            Message = "Can be ignored as long as the textures with the '_cm' and '_alphamask' suffixes exist and they work in game."
                                    + $"\n - File {(existsCM ? "exists" : "does NOT exist")}: '{pathCM}'"
                                    + $"\n - File {(existsAM ? "exists" : "does NOT exist")}: '{pathAM}'"
                                    + "\nNote: textures must be in the mod folder, game forecefully appends mod's path to the texture!",
                        };

                        if(existsAM && existsCM)
                            info.SetSeverity = TErrorSeverity.Notice;
                        else if(!existsAM && !existsCM)
                            info.SetSeverity = TErrorSeverity.Error;

                        cloudLayerTextureMessage[filePath] = info;
                    }
                }
            }
            else
            {
                Log.Error("Couldn't get planets from DefinitionSet O.o");
            }
            #endregion

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

                bool isCompileError = error.Message.StartsWith("Compilation of");
                if(isCompileError)
                {
                    // chat message when ANY mod has compile errors (published too)
                    CompileErrors = true;

                    // pop up F11 menu if there's compile errors with local mods
                    if(isLocal)
                        MyDefinitionErrors.ShouldShowModErrors = true;
                }

                // HACK MyDefinitionManager.ProcessContentFilePath() + cloudlayer stuff from above
                string resourceNotFoundPrefix = "Resource not found, setting to null. Resource path: ";
                if(error.Message.StartsWith(resourceNotFoundPrefix))
                {
                    string filePath = error.Message.Substring(resourceNotFoundPrefix.Length, error.Message.Length - resourceNotFoundPrefix.Length);

                    CloudLayerInfo info;
                    if(cloudLayerTextureMessage.TryGetValue(filePath, out info))
                    {
                        if(info.SetSeverity.HasValue)
                            error.Severity = info.SetSeverity.Value;

                        error.Message += $"\n{Signature}{info.Message}";
                    }
                }
            }
        }

        void CheckErrorsContinuous() // called per 3 seconds by update method
        {
            ListReader<MyDefinitionErrors.Error> errors = MyDefinitionErrors.GetErrors();

            for(int i = 0; i < errors.Count; i++)
            {
                MyDefinitionErrors.Error error = errors[i];

                // HACK MyCubeGrid.TestBlockPlacementArea(), falsely triggered by MyModel.LoadData() via m_loadingErrorProcessed=false
                // reminder that this executes per 3 seconds so it has to not catch the same messages multiple times.
                if(error.Severity == TErrorSeverity.Error && error.Message == "There was error during loading of model, please check log file.")
                {
                    error.Severity = TErrorSeverity.Notice; // reduce its severity
                    error.Message += $"\n{Signature}This is a meaningless error that always happens in a certain context, ignore it.";
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
            if(tick % (Constants.TicksPerSecond * 3) != 0)
                return;

            CheckErrorsContinuous();

            if(!FirstSpawnChecked)
            {
                IMyCharacter character = MyAPIGateway.Session?.Player?.Character;
                if(character != null) // wait until first spawn
                {
                    FirstSpawnChecked = true;

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