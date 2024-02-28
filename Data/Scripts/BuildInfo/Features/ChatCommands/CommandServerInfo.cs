using System.Text;
using Digi.BuildInfo.Utilities;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandServerInfo : Command
    {
        public CommandServerInfo() : base("serverinfo", "worldinfo", "mods")
        {
        }

        public override void PrintHelp(StringBuilder sb)
        {
            AppendCommands(sb);
            sb.Append("  Shows world's settings and mods.").NewLine();
        }

        public override void Execute(Arguments args)
        {
            Main.MenuHandler.ServerInfo.ToggleMenu();
        }
    }
}