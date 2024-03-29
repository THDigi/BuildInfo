﻿using System.Text;
using Digi.BuildInfo.Utilities;
using VRage.Game;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandChangelog : Command
    {
        public CommandChangelog() : base("changelog")
        {
        }

        public override void PrintHelp(StringBuilder sb)
        {
            AppendCommands(sb);
            sb.Append("  Opens steam overlay with workshop page on the change notes tab.").NewLine();
        }

        public override void Execute(Arguments args)
        {
            MyObjectBuilder_Checkpoint.ModItem modItem = Main.Session.ModContext.ModItem;
            if(modItem.PublishedFileId > 0)
            {
                Utils.OpenModPage(modItem.PublishedServiceName, modItem.PublishedFileId, changelog: true);
            }
            else
            {
                PrintChat("Can't find mod workshop ID, probably it's a local mod?", FontsHandler.RedSh);
            }
        }
    }
}