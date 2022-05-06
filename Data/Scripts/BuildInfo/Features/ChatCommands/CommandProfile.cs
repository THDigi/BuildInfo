using System.Text;
using Digi.BuildInfo.Utilities;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandProfile : Command
    {
        public CommandProfile() : base("profile")
        {
        }

        public override void Execute(Arguments args)
        {
            Main.ProfilerDisplay.Show = !Main.ProfilerDisplay.Show;
            PrintChat($"Profiling {(Main.ProfilerDisplay.Show ? "started" : "stopped")}", FontsHandler.WhiteSh);
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(MainAlias).NewLine();
            sb.Append("  Starts/stops profiling this mod to identify high CPU usage areas.").NewLine();
        }
    }
}