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

        public override void PrintHelp(StringBuilder sb)
        {
            AppendCommands(sb);
            sb.Append("  Shows what mods and DLCs are used on the ship you're looking at (up close).").NewLine();
            sb.Append("  Also available for blueprints in projectors' terminal.").NewLine();
        }

        public override void Execute(Arguments args)
        {
            IMyCubeGrid aimedGrid = Main.EquipmentMonitor?.AimedBlock?.CubeGrid;

            if(aimedGrid == null)
                aimedGrid = Utils.GetAimedGrid();

            if(aimedGrid == null)
            {
                PrintChat("Look at a ship up close first.", FontsHandler.RedSh);
                PrintChat("This feature is also in projectors' terminal.", FontsHandler.RedSh);
                return;
            }

            bool allowed = Main.AnalyseShip.AnalyseRealGrid(aimedGrid);
            if(!allowed)
            {
                PrintChat("Can't be used on enemy ships.", FontsHandler.RedSh);
            }
        }
    }
}