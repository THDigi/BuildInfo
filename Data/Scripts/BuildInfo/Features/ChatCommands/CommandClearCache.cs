using System.Text;
using Digi.BuildInfo.Utilities;
using VRage.Game;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandClearCache : Command
    {
        public CommandClearCache() : base("clearcache")
        {
        }

        public override void Execute(Arguments args)
        {
            Main.TextGeneration.CachedBuildInfoNotification.Clear();
            Main.TextGeneration.CachedBuildInfoTextAPI.Clear();
            Utils.ShowColoredChatMessage(MainAlias, "Emptied block info cache.", MyFontEnum.Green);
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(MainAlias).NewLine();
            sb.Append("  Clears the block info cache, not for normal use.").NewLine();
        }
    }
}