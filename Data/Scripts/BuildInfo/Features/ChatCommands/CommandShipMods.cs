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

            var grids = MyAPIGateway.GridGroups.GetGroup(aimedGrid, GridLinkTypeEnum.Physical);

            foreach(var grid in grids)
            {
                if(grid.BigOwners != null && grid.BigOwners.Count > 0)
                {
                    foreach(var owner in grid.BigOwners)
                    {
                        if(MyAPIGateway.Session.Player.GetRelationTo(owner) == MyRelationsBetweenPlayerAndBlock.Enemies)
                        {
                            Utils.ShowColoredChatMessage(MainAlias, "Can't be used on enemy ships.", MyFontEnum.Red);
                            return;
                        }
                    }
                }
            }

            Main.AnalyseShip.Analyse(aimedGrid);
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(MainAlias).NewLine();
            sb.Append("  Shows what mods and DLCs are used on the aimed block's ship.").NewLine();
            sb.Append("  Also available for blueprints in projectors' terminal.").NewLine();
        }
    }
}