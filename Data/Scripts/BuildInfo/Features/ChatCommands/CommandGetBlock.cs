using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Game;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandGetBlock : Command
    {
        public CommandGetBlock() : base("getblock")
        {
        }

        public override void Execute(Arguments args)
        {
            if(MyAPIGateway.Session?.Player == null)
            {
                PrintChat(Constants.WarnPlayerIsNull, FontsHandler.RedSh);
                return;
            }

            if(Main.EquipmentMonitor.AimedBlock != null)
            {
                if(args != null && args.Count > 0)
                {
                    string slotStr = args.Get(0);
                    int slot;

                    if(int.TryParse(slotStr, out slot) && slot >= 1 && slot <= 9)
                    {
                        MyVisualScriptLogicProvider.SetToolbarSlotToItemLocal(slot - 1, Main.EquipmentMonitor.BlockDef.Id, MyAPIGateway.Session.Player.IdentityId);

                        PrintChat($"{Main.EquipmentMonitor.BlockDef.DisplayNameText} placed in slot {slot.ToString()}.", FontsHandler.GreenSh);
                    }
                    else
                    {
                        PrintChat($"'{slotStr}' is not a number from 1 to 9.", FontsHandler.RedSh);
                    }

                    return;
                }

                // if no argument is defined, ask for a number
                Main.PickBlock.AskToPick(Main.EquipmentMonitor.BlockDef);
            }
            else
            {
                Utils.ShowColoredChatMessage(MainAlias, "Aim at a block with a welder or grinder first.", FontsHandler.RedSh);
            }
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(MainAlias).Append(" [1~9]").NewLine();
            sb.Append("  Picks the aimed block to be placed in toolbar.").NewLine();
        }
    }
}