using System.Text;
using Digi.BuildInfo.Utilities;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandQuickMenu : Command
    {
        public CommandQuickMenu() : base("menu")
        {
        }

        public override void Execute(Arguments args)
        {
            Main.QuickMenu.ShowMenu();
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(MainAlias).NewLine();
            sb.Append("  Shows the quick menu.").NewLine();
        }
    }
}