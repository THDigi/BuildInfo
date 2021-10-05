using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Game;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandChangelog : Command
    {
        public CommandChangelog() : base("changelog")
        {
        }

        public override void Execute(Arguments args)
        {
            ulong id = Log.WorkshopId;

            if(id > 0)
            {
                string link = "https://steamcommunity.com/sharedfiles/filedetails/changelog/" + id.ToString();

                MyVisualScriptLogicProvider.OpenSteamOverlayLocal(link);

                PrintChat($"Opened steam overlay with {link}", FontsHandler.GreenSh);
            }
            else
            {
                PrintChat("Can't find mod workshop ID, probably it's a local mod?", FontsHandler.RedSh);
            }
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(MainAlias).NewLine();
            sb.Append("  Opens steam overlay with workshop page on the change notes tab.").NewLine();
        }
    }
}