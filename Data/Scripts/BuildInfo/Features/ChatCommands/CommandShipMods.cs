using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;
using VRage.Game;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandShipMods : Command
    {
        public CommandShipMods() : base("shipmods")
        {
        }

        public override void Execute(Arguments args)
        {
            if(Main.EquipmentMonitor.AimedBlock != null)
            {
                var grid = Main.EquipmentMonitor.AimedBlock.CubeGrid;

                bool friendly = true;

                // TODO: improve checking to avoid people placing subgrids and aiming at subgrid to fool the check
                if(grid.BigOwners != null)
                {
                    friendly = false;

                    for(int i = 0; i < grid.BigOwners.Count; ++i)
                    {
                        if(MyAPIGateway.Session.Player.GetRelationTo(grid.BigOwners[i]) != MyRelationsBetweenPlayerAndBlock.Enemies)
                        {
                            friendly = true;
                            break;
                        }
                    }
                }

                if(friendly)
                {
                    Main.AnalyseShip.Analyse(grid);
                }
                else
                {
                    Utils.ShowColoredChatMessage(MainAlias, "Can't be used on enemy ships.", MyFontEnum.Red);
                }
            }
            else
            {
                Utils.ShowColoredChatMessage(MainAlias, "Aim at a ship with a welder/grinder first.", MyFontEnum.Red);
                Utils.ShowColoredChatMessage(MainAlias, "This feature is also in projectors' terminal.", MyFontEnum.White);
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