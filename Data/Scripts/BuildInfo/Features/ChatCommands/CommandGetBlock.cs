using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandGetBlock : Command
    {
        public CommandGetBlock() : base("getblock")
        {
        }

        public override void Execute(Arguments args)
        {
            if(!Constants.BLOCKPICKER_IN_MP && MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                Utils.ShowColoredChatMessage(MainAlias, Constants.BLOCKPICKER_DISABLED_CHAT, MyFontEnum.Red);
                return;
            }

            if(Main.EquipmentMonitor.AimedBlock != null)
            {
                if(args != null && args.Count > 0)
                {
                    var slotStr = args.Get(0);
                    int slot;

                    if(int.TryParse(slotStr, out slot) && slot >= 1 && slot <= 9)
                    {
                        MyVisualScriptLogicProvider.SetToolbarSlotToItem(slot - 1, Main.EquipmentMonitor.BlockDef.Id, MyAPIGateway.Session.Player.IdentityId);

                        Utils.ShowColoredChatMessage(MainAlias, $"{Main.EquipmentMonitor.BlockDef.DisplayNameText} placed in slot {slot.ToString()}.", MyFontEnum.Green);
                    }
                    else
                    {
                        Utils.ShowColoredChatMessage(MainAlias, $"'{slotStr}' is not a number from 1 to 9.", MyFontEnum.Red);
                    }

                    return;
                }

                // if no argument is defined, ask for a number
                Main.PickBlock.PickedBlockDef = Main.EquipmentMonitor.BlockDef;
            }
            else
            {
                Utils.ShowColoredChatMessage(MainAlias, "Aim at a block with a welder or grinder first.", MyFontEnum.Red);
            }
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(MainAlias).Append(" [1~9]").NewLine();
            sb.Append("  Picks the aimed block to be placed in toolbar.").NewLine();
        }
    }
}