using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandToolbarErasePrefix : Command
    {
        public CommandToolbarErasePrefix() : base("toolbarErasePrefix", "tep")
        {
        }

        public override void Execute(Arguments args)
        {
            IMyShipController shipCtrl = MyAPIGateway.Session.ControlledObject as IMyShipController;
            if(shipCtrl == null)
            {
                PrintChat("Must control a cockpit or RC first.", FontsHandler.RedSh);
                return;
            }

            string text = args.GetRestAsText(0);

            string failReason = Main.ToolbarCustomLabels.SetErasePrefix(text);
            if(failReason != null)
            {
                PrintChat($"Couldn't set erase prefix, reason: {failReason}", FontsHandler.RedSh);
                return;
            }

            if(text == null)
                PrintChat($"Cleared toolbar erase prefix.", FontsHandler.GreenSh);
            else
                PrintChat($"Set toolbar erase prefix to '{text}'", FontsHandler.GreenSh);
        }

        public override void PrintHelp(StringBuilder sb)
        {
            foreach(string alias in Aliases)
            {
                sb.Append(ChatCommandHandler.MainCommandPrefix).Append(' ').Append(alias).Append(" [prefix]").NewLine();
            }

            sb.Append("  Set the prefix to be erased for your controlled cockpit/RC's toolbar.").NewLine();
            sb.Append("  Text is optional, omitting it would clear the prefix.").NewLine();
        }
    }
}