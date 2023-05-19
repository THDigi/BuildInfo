using System.Linq;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
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
        HudState? RevertHud;
        HudAPIv2.BillBoardHUDMessage ErrorsMenuBackdrop;
        const string ErrorsGUITypeName = "MyGuiScreenDebugErrors";

        public ModderHelp(BuildInfoMod main) : base(main)
        {
            Main.GUIMonitor.ScreenAdded += GUIScreenAdded;
            Main.GUIMonitor.ScreenRemoved += GUIScreenRemoved;

            CheckErrors();
            CheckMods();
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

        void CheckErrors()
        {
            foreach(MyDefinitionErrors.Error error in MyDefinitionErrors.GetErrors())
            {
                bool isLocal = IsLocalMod(error.ModName);

                // chat message when LOCAL mods have definition errors
                if(isLocal)
                    DefinitionErrors = true;

                bool isCompileError = error.Message.StartsWith("Compilation of");
                if(isCompileError)
                {
                    // chat message when ANY mod has compile errors (published too)
                    CompileErrors = true;

                    // pop up F11 menu if there's compile errors with local mods
                    if(isLocal)
                        MyDefinitionErrors.ShouldShowModErrors = true;
                }
            }

            if(DefinitionErrors || CompileErrors)
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        void CheckMods()
        {
            // only in offline mode with at least one local mod
            if(MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE || !MyAPIGateway.Session.Mods.Any(m => m.PublishedFileId == 0))
                return;

            foreach(MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                if(def.Context == null)
                {
                    // HACK: the generated LCD texture defs by the game have null Context
                    if(!(def is MyLCDTextureDefinition))
                        ModHint(def, "has null Context, a script probably set it like this?");

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
                        if(airTightFaces == 0)
                        {
                            ModHint(def, $"has 0 airtight sides, should set IsAirTight to false to save some computation.");
                        }
                        else if(airTightFaces == totalFaces)
                        {
                            ModHint(def, $"has all sides airtight, should set IsAirTight to true to save some computation.");
                        }
                    }
                }
            }

            if(ModProblems > 0 || ModHints > 0)
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        void ModProblem(MyDefinitionBase def, string text)
        {
            string message = $"Local mod problem: {GetDefId(def)} from '{GetModName(def)}' {text}";

            Log.Info(message);
            MyDefinitionErrors.Add(def.Context, $"[BuildInfo] {message}", TErrorSeverity.Error);
            ModProblems++;
        }

        void ModHint(MyDefinitionBase def, string text)
        {
            string message = $"Local mod hint: {GetDefId(def)} from '{GetModName(def)}' {text}";

            Log.Info(message);
            MyDefinitionErrors.Add(def.Context, $"[BuildInfo] {message}", TErrorSeverity.Notice);
            ModHints++;
        }

        static string GetDefId(MyDefinitionBase def)
        {
            return def.Id.ToString().Replace("MyObjectBuilder_", "");
        }

        static string GetModName(MyDefinitionBase def)
        {
            if(def.Context != null)
                return def.Context.IsBaseGame ? "(base game)" : def.Context.ModName;
            return "(unknown)";
        }

        static bool IsLocalMod(string modName)
        {
            foreach(MyObjectBuilder_Checkpoint.ModItem modItem in MyAPIGateway.Session.Mods)
            {
                if(modItem.Name == modName)
                {
                    return modItem.PublishedFileId == 0;
                }
            }

            return false;
        }

        public override void UpdateAfterSim(int tick)
        {
            if(tick % Constants.TicksPerSecond != 0)
                return;

            IMyCharacter character = MyAPIGateway.Session?.Player?.Character;
            if(character == null) // wait until first spawn
                return;

            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

            if(CompileErrors)
                Utils.ShowColoredChatMessage(BuildInfoMod.ModName, "ModderHelp: Other mod(s) have compile errors! See SE log for details.", FontsHandler.RedSh);

            if(ModHints > 0 || ModProblems > 0)
                Utils.ShowColoredChatMessage(BuildInfoMod.ModName, "ModderHelp: There's problems or hints with local mod(s), see F11 menu.", FontsHandler.YellowSh);
            else if(DefinitionErrors)
                Utils.ShowColoredChatMessage(BuildInfoMod.ModName, "ModderHelp: There are definition errors, see F11 menu.", FontsHandler.YellowSh);
        }

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
                    ErrorsMenuBackdrop.Width = 100; // fullscreen-est fullscreen xD
                    ErrorsMenuBackdrop.Height = 100;
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
    }
}