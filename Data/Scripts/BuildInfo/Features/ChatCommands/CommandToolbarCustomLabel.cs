using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandToolbarCustomLabel : Command
    {
        public CommandToolbarCustomLabel() : base("toolbarLabel", "tl")
        {
        }

        public override void PrintHelp(StringBuilder sb)
        {
            AppendCommands(sb, "<1~9> [text]");
            sb.Append("  Set custom label for the specified slot on the current page of your controlled cockpit/RC's toolbar.").NewLine();
            sb.Append("  Text is optional, omitting it would clear the custom label.").NewLine();
        }

        public override void Execute(Arguments args)
        {
            IMyShipController shipCtrl = MyAPIGateway.Session.ControlledObject as IMyShipController;
            if(shipCtrl == null)
            {
                PrintChat("Must control a cockpit or RC first.", FontsHandler.RedSh);
                return;
            }

            if(args == null || args.Count == 0)
            {
                PrintChat("You need to provide a slot number, from 1 to 9.", FontsHandler.RedSh);
                return;
            }

            string slotStr = args.Get(0);
            int slot;
            if(!int.TryParse(slotStr, out slot) && slot >= 1 && slot <= 9)
            {
                PrintChat($"'{slotStr}' is not a number from 1 to 9.", FontsHandler.RedSh);
                return;
            }

            string label = args.GetRestAsText(1);

            slotStr = slot.ToString();

            string failReason = Main.ToolbarCustomLabels.SetSlotLabel(slot, label);
            if(failReason != null)
            {
                PrintChat($"Couldn't set slot {slotStr}'s label, reason: {failReason}", FontsHandler.RedSh);
                return;
            }

            if(label == null)
                PrintChat($"Cleared custom label for slot {slotStr}.", FontsHandler.GreenSh);
            else
                PrintChat($"Set custom label for {slotStr} to '{label}'", FontsHandler.GreenSh);
        }
    }
}