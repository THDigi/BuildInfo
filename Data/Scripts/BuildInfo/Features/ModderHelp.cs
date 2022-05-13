using System.Linq;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class ModderHelp : ModComponent
    {
        bool ModErrors = false;

        public ModderHelp(BuildInfoMod main) : base(main)
        {
            // only in offline mode with at least one local mod
            if(MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE || !MyAPIGateway.Session.Mods.Any(m => m.PublishedFileId == 0))
                return;

            foreach(MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
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
                        ModError(def, $"has negative values for Center tag! This will break some mountpoints and various other weird issues.");
                    }
                    else if(blockDef.Center.X > maxCenter.X || blockDef.Center.Y > maxCenter.Y || blockDef.Center.Z > maxCenter.Z)
                    {
                        ModError(def, $"has too high values for Center tag! It should be at most Size - 1. This will break some mountpoints and various other weird issues.");
                    }

                    if(blockDef.MirroringCenter.X < 0 || blockDef.MirroringCenter.Y < 0 || blockDef.MirroringCenter.Z < 0)
                    {
                        ModError(def, $"has negative values for MirroringCenter tag!");
                    }
                    else if(blockDef.MirroringCenter.X > maxCenter.X || blockDef.MirroringCenter.Y > maxCenter.Y || blockDef.MirroringCenter.Z > maxCenter.Z)
                    {
                        ModError(def, $"has too high values for MirroringCenter tag! It should be at most Size - 1.");
                    }
                }
            }

            if(ModErrors)
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        void ModError(MyDefinitionBase def, string text)
        {
            Log.Error($"{def.Id.ToString().Replace("MyObjectBuilder_", "")} from '{def.Context?.ModName ?? "(base game)"}' {text}");
            ModErrors = true;
        }

        void ModHint(MyDefinitionBase def, string text)
        {
            Log.Info($"HINT: {def.Id.ToString().Replace("MyObjectBuilder_", "")} from '{def.Context?.ModName ?? "(base game)"}' {text}");
        }

        public override void UpdateAfterSim(int tick)
        {
            if(tick % Constants.TicksPerSecond != 0)
                return;

            IMyCharacter character = MyAPIGateway.Session?.Player?.Character;
            if(character != null) // wait until first spawn
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

                Utils.ShowColoredChatMessage(BuildInfoMod.ModName, "Some installed mod(s) have some issue(s), see game's log or buildinfo's log.", FontsHandler.RedSh);
            }
        }
    }
}