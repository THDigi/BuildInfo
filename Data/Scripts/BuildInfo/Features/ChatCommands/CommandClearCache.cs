using System.Text;
using Digi.BuildInfo.Utilities;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandClearCache : Command
    {
        public CommandClearCache() : base("clearcache")
        {
        }

        public override void PrintHelp(StringBuilder sb)
        {
            AppendCommands(sb);
            sb.Append("  Clears the block info cache, not for normal use.").NewLine();
        }

        public override void Execute(Arguments args)
        {
            Main.TextGeneration.CachedBuildInfoNotification.Clear();
            Main.TextGeneration.CachedBuildInfoTextAPI.Clear();
            PrintChat("Emptied block info cache.", FontsHandler.GreenSh);
        }
    }
}