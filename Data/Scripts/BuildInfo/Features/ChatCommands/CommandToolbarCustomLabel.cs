using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandToolbarCustomLabel : Command
    {
        StringBuilder TempSB = new StringBuilder(128);

        public CommandToolbarCustomLabel() : base("toolbarlabel", "tl")
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

            string label = null;
            if(args.Count > 1)
            {
                TempSB.Clear();

                for(int i = 1; i < args.Count; i++)
                {
                    TempSB.Append(args.Get(i)).Append(' ');
                }

                if(TempSB.Length > 0)
                    TempSB.Length -= 1; // remove last space

                label = TempSB.ToString();
            }

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

        public override void PrintHelp(StringBuilder sb)
        {
            foreach(string alias in Aliases)
            {
                sb.Append(ChatCommandHandler.MainCommandPrefix).Append(' ').Append(alias).Append(" <1~9> [text]").NewLine();
            }

            sb.Append("  Set custom label for the specified slot on the current page of your controlled cockpit/RC's toolbar.").NewLine();
            sb.Append("  Text is optional, omitting it would clear the custom label.").NewLine();
        }
    }
}