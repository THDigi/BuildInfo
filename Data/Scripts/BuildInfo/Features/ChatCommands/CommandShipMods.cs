using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandShipMods : Command
    {
        public CommandShipMods() : base("shipmods")
        {
        }

        public override void Execute(Arguments args)
        {
            var aimedGrid = Main.EquipmentMonitor?.AimedBlock?.CubeGrid;

            if(aimedGrid == null)
            {
                Utils.ShowColoredChatMessage(MainAlias, "Aim at a ship with a welder/grinder first.", MyFontEnum.Red);
                Utils.ShowColoredChatMessage(MainAlias, "This feature is also in projectors' terminal.", MyFontEnum.White);
                return;
            }

            bool allowed = Main.AnalyseShip.Analyse(aimedGrid);

            if(!allowed)
            {
                Utils.ShowColoredChatMessage(MainAlias, "Can't be used on enemy ships.", MyFontEnum.Red);
            }
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(MainAlias).NewLine();
            sb.Append("  Shows what mods and DLCs are used on the aimed block's ship.").NewLine();
            sb.Append("  Also available for blueprints in projectors' terminal.").NewLine();
        }
    }
}