using System.Text;
using Digi.BuildInfo.Utilities;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandReloadConfig : Command
    {
        public CommandReloadConfig() : base("reload")
        {
        }

        public override void PrintHelp(StringBuilder sb)
        {
            AppendCommands(sb);
            sb.Append("  Reloads the config file.").NewLine();
        }

        public override void Execute(Arguments args)
        {
            if(Main.Config.Load())
                PrintChat("Config loaded.", FontsHandler.GreenSh);
            else
                PrintChat("Config created and loaded default settings.", FontsHandler.GreenSh);

            Main.Config.Save();
            Main.TextGeneration.OnConfigReloaded();
        }
    }
}