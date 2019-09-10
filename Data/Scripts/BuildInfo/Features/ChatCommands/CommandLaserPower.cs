using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using VRage.Game;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandLaserPower : Command
    {
        private StringBuilder sb = new StringBuilder(96);

        public CommandLaserPower() : base("laserpower")
        {
        }

        public override void Execute(Arguments args)
        {
            var antennaDef = Main.EquipmentMonitor.BlockDef as MyLaserAntennaDefinition;

            if(antennaDef != null)
            {
                if(args != null && args.Count > 0)
                {
                    var kmStr = args.Get(0);
                    float km;

                    if(float.TryParse(kmStr, out km) && km > 0)
                    {
                        var meters = (km * 1000);
                        var megaWatts = Hardcoded.LaserAntenna_PowerUsage(antennaDef, meters);

                        sb.Clear();
                        sb.PowerFormat(megaWatts).Append(" at ").DistanceFormat(meters).Append(" for ").Append(antennaDef.DisplayNameText).Append(antennaDef.CubeSize == MyCubeSize.Large ? " (large grid)" : " (small grid)");

                        Utils.ShowColoredChatMessage(MainAlias, sb.ToString(), MyFontEnum.Green);
                    }
                    else
                    {
                        Utils.ShowColoredChatMessage(MainAlias, $"'{kmStr}' is not a number larger than 0.", MyFontEnum.Red);
                    }
                }
                else
                {
                    Utils.ShowColoredChatMessage(MainAlias, $"Need a distance in kilometers, e.g. {MainAlias} 500", MyFontEnum.Red);
                }
            }
            else
            {
                Utils.ShowColoredChatMessage(MainAlias, "Need a reference Laser Antenna type block, equip or aim at one first then the command.", MyFontEnum.Red);
            }
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(MainAlias).Append(" <km>").NewLine();
            sb.Append("  Calculates power needed for equipped/aimed laser antenna").NewLine();
            sb.Append("  at the specified range.").NewLine();
        }
    }
}