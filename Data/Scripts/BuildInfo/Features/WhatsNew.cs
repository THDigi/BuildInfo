using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features
{
    public class WhatsNew : ModComponent
    {
        public WhatsNew(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            int modVersion = Main.Config.ModVersion.Value;
            if(modVersion > 0 && modVersion < Constants.MOD_VERSION)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
            }
        }

        public override void UnregisterComponent()
        {
        }

        public override void UpdateAfterSim(int tick)
        {
            if(tick % Constants.TICKS_PER_SECOND != 0)
                return;

            IMyCharacter character = MyAPIGateway.Session?.Player?.Character;
            if(character != null) // wait until first spawn
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

                Main.Config.ModVersion.Value = Constants.MOD_VERSION;
                Main.Config.Save();
                Utils.ShowColoredChatMessage(Log.ModName, "New notable changes! For changelog type in chat: /bi changelog", FontsHandler.GreenSh);
            }
        }
    }
}