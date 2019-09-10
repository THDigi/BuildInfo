using System.Text;
using Digi.BuildInfo.Utilities;
using VRage.Game;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandReloadConfig : Command
    {
        public CommandReloadConfig() : base("reload")
        {
        }

        public override void Execute(Arguments args)
        {
            if(Main.Config.Load())
                Utils.ShowColoredChatMessage(MainAlias, "Config loaded.", MyFontEnum.Green);
            else
                Utils.ShowColoredChatMessage(MainAlias, "Config created and loaded default settings.", MyFontEnum.Green);

            Main.Config.Save();
            Main.TextGeneration.OnConfigReloaded();
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(MainAlias).NewLine();
            sb.Append("  Reloads the config file.").NewLine();
        }
    }
}