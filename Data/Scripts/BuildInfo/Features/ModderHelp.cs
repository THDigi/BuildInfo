using System.Collections.Generic;
using System.Linq;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class ModderHelp : ModComponent
    {
        int ModProblems = 0;
        int ModConcerns = 0;

        public ModderHelp(BuildInfoMod main) : base(main)
        {
            // only in offline mode with at least one local mod
            if(MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE || !MyAPIGateway.Session.Mods.Any(m => m.PublishedFileId == 0))
                return;

            foreach(MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                if(!BuildInfoMod.IsDevMod)
                {
                    // ignore untouched definitions
                    if(def.Context == null || def.Context.IsBaseGame)
                        continue;

                    // ignore workshop mods
                    if(def.Context.ModItem.PublishedFileId != 0)
                        continue;
                }

                MyCubeBlockDefinition blockDef = def as MyCubeBlockDefinition;
                if(blockDef != null)
                {
                    // TODO: check for airtightness optimization?
                    //if(!blockDef.IsAirTight.HasValue)
                    //{
                    //    int airTightFaces, totalFaces;
                    //    AirTightMode airTight = Utils.GetAirTightFaces(blockDef, out airTightFaces, out totalFaces);
                    //    if(airTightFaces == 0)
                    //    {
                    //        ModHint(def, $"has no sides airtight but no IsAirTight declared, could set it to false to save some computation.");
                    //    }
                    //    else if(airTightFaces == totalFaces)
                    //    {
                    //        ModHint(def, $"has all sides airtight but no IsAirTight declared, could set it to true to save some computation.");
                    //    }
                    //}

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
                        ModProblem(def, "exploitable! Block gets placed in survival with more than one first componment, allowing players to grind it to gain those extra components." +
                                        "\nFix by reordering components or changing amounts of other components or making a single component as first stack.");
                    }
                }
            }

            if(ModProblems > 0 || ModConcerns > 0)
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        void ModProblem(MyDefinitionBase def, string text)
        {
            Log.Info($"Mod problem: {def.Id.ToString().Replace("MyObjectBuilder_", "")} from '{def.Context?.ModName ?? "(base game)"}' {text}");
            ModProblems++;
        }

        void ModConcern(MyDefinitionBase def, string text)
        {
            Log.Info($"Mod concern: {def.Id.ToString().Replace("MyObjectBuilder_", "")} from '{def.Context?.ModName ?? "(base game)"}' {text}");
            ModConcerns++;
        }

        public override void UpdateAfterSim(int tick)
        {
            if(tick % Constants.TicksPerSecond != 0)
                return;

            IMyCharacter character = MyAPIGateway.Session?.Player?.Character;
            if(character != null) // wait until first spawn
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

                //StringBuilder sb = new StringBuilder(512);
                //
                //sb.Append("... :");
                //
                //if(ModProblems > 0)
                //    sb.Append(ModProblems).Append(" problems, ");
                //
                //if(ModConcerns > 0)
                //    sb.Append(ModConcerns).Append(" concerns, ");
                //
                //sb.Length -= 2; // remove last ", "
                //
                //Utils.ShowColoredChatMessage(BuildInfoMod.ModName, sb.ToString(), FontsHandler.RedSh);

                Utils.ShowColoredChatMessage(BuildInfoMod.ModName, "ModderHelp: There's problems with local mod(s), see SE log or BuildInfo's log.", FontsHandler.RedSh);
            }
        }
    }
}