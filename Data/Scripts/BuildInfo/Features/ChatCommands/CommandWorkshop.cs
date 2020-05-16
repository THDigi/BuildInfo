using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Game;
using VRage.Game;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandWorkshop : Command
    {
        public CommandWorkshop() : base("workshop")
        {
        }

        public override void Execute(Arguments args)
        {
            var id = Log.WorkshopId;

            if(id > 0)
            {
                var link = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + id.ToString();

                MyVisualScriptLogicProvider.OpenSteamOverlayLocal(link);

                Utils.ShowColoredChatMessage(MainAlias, $"Opened steam overlay with {link}", MyFontEnum.Green);
            }
            else
            {
                Utils.ShowColoredChatMessage(MainAlias, "Can't find mod workshop ID, probably it's a local mod?", MyFontEnum.Red);
            }
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(MainAlias).NewLine();
            sb.Append("  Opens steam overlay with workshop of this mod.").NewLine();
        }
    }
}