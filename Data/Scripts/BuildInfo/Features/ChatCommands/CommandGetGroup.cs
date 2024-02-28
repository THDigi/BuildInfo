using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandGetGroup : Command
    {
        public CommandGetGroup() : base("getgroup")
        {
        }

        public override void PrintHelp(StringBuilder sb)
        {
            AppendCommands(sb, "<1~9>");
            sb.Append("  Adds aimed/held block's variant group to the toolbar.").NewLine();
        }

        public override void Execute(Arguments args)
        {
            if(MyAPIGateway.Session?.Player == null)
            {
                PrintChat(Constants.WarnPlayerIsNull, FontsHandler.RedSh);
                return;
            }

            MyCubeBlockDefinition blockDef = Main.EquipmentMonitor?.BlockDef;
            if(blockDef == null)
            {
                Utils.ShowColoredChatMessage(PrimaryCommand, "First equip a block or aim at one using welder or grinder.", FontsHandler.RedSh);
                return;
            }

            MyCubeBlockDefinition primaryDef = blockDef.BlockVariantsGroup?.PrimaryGUIBlock;
            if(primaryDef == null)
            {
                Utils.ShowColoredChatMessage(PrimaryCommand, "This block does not belong to any variants group.", FontsHandler.YellowSh);
                return;
            }

            if(args == null || args.Count <= 0)
            {
                Utils.ShowColoredChatMessage(PrimaryCommand, "Input a slot number to place this into.", FontsHandler.RedSh);
                return;
            }

            string slotStr = args.Get(0);
            int slot;

            if(int.TryParse(slotStr, out slot) && slot >= 1 && slot <= 9)
            {
                MyVisualScriptLogicProvider.SetToolbarSlotToItemLocal(slot - 1, primaryDef.Id, MyAPIGateway.Session.Player.IdentityId);

                PrintChat($"{primaryDef.DisplayNameText} placed in slot {slot.ToString()}.", FontsHandler.GreenSh);
            }
            else
            {
                PrintChat($"'{slotStr}' is not a number from 1 to 9.", FontsHandler.RedSh);
            }
        }
    }
}