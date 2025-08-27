using System.Text;
using Digi.BuildInfo.Utilities;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandScreenCoords : Command
    {
        public CommandScreenCoords() : base("screencoords")
        {
        }

        public override void PrintHelp(StringBuilder sb)
        {
            AppendCommands(sb);
            sb.Append("Coordinates at mouse position for easier placement of things.").NewLine();
        }

        public override void Execute(Arguments args)
        {
            Main.ScreenCoords.Toggle();
        }
    }
}