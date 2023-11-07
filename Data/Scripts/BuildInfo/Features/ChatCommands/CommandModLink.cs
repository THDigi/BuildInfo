using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Game;
using VRage.Game;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandModLink : Command
    {
        public CommandModLink() : base("modlink")
        {
        }

        public override void Execute(Arguments args)
        {
            if(Main.EquipmentMonitor.BlockDef != null)
            {
                if(!Main.EquipmentMonitor.BlockDef.Context.IsBaseGame)
                {
                    MyObjectBuilder_Checkpoint.ModItem modItem = Main.EquipmentMonitor.BlockDef.Context.ModItem;
                    if(modItem.PublishedFileId > 0)
                    {
                        Utils.OpenModPage(modItem.PublishedServiceName, modItem.PublishedFileId);
                    }
                    else
                    {
                        PrintChat("Can't find mod workshop ID, probably it's a local mod?", FontsHandler.RedSh);
                    }
                }
                else
                    PrintChat($"{Main.EquipmentMonitor.BlockDef.DisplayNameText} is not added by a mod.", FontsHandler.RedSh);
            }
            else
                PrintChat("No block selected/equipped.", FontsHandler.RedSh);
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(MainAlias).NewLine();
            sb.Append("  Opens steam overlay with workshop on the selected block's mod").NewLine();
        }
    }
}