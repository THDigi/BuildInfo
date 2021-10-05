using System.Text;
using Digi.BuildInfo.Utilities;
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
            IMyCubeGrid aimedGrid = Main.EquipmentMonitor?.AimedBlock?.CubeGrid;
            if(aimedGrid == null)
            {
                PrintChat("Aim at a ship with a welder/grinder first.", FontsHandler.RedSh);
                PrintChat("This feature is also in projectors' terminal.", FontsHandler.RedSh);
                return;
            }

            bool allowed = Main.AnalyseShip.Analyse(aimedGrid);
            if(!allowed)
            {
                PrintChat("Can't be used on enemy ships.", FontsHandler.RedSh);
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