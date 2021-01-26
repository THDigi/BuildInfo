using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using VRage.Game;

namespace Digi.BuildInfo.Features
{
    public class WhatsNew : ModComponent
    {
        public WhatsNew(BuildInfoMod main) : base(main)
        {
        }

        protected override void RegisterComponent()
        {
            if(Main.Config.ModVersion.Value < Constants.MOD_VERSION)
            {
                UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;
            }
        }

        protected override void UnregisterComponent()
        {
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(tick % Constants.TICKS_PER_SECOND != 0)
                return;

            var character = MyAPIGateway.Session?.Player?.Character;

            if(character != null) // wait until first spawn
            {
                Main.Config.ModVersion.Value = Constants.MOD_VERSION;
                Main.Config.Save();

                UpdateMethods = UpdateFlags.NONE;
                Utils.ShowColoredChatMessage(Log.ModName, "New notable changes! For changelog type in chat: /bi changelog", MyFontEnum.Green);
            }
        }
    }
}