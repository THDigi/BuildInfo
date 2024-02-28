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

        public override void PrintHelp(StringBuilder sb)
        {
            AppendCommands(sb, "<km>");
            sb.Append("  Calculates power needed for equipped/aimed laser antenna").NewLine();
            sb.Append("  at the specified range.").NewLine();
        }

        public override void Execute(Arguments args)
        {
            MyLaserAntennaDefinition antennaDef = Main.EquipmentMonitor.BlockDef as MyLaserAntennaDefinition;
            if(antennaDef != null)
            {
                if(args != null && args.Count > 0)
                {
                    string kmStr = args.Get(0);
                    float km;

                    if(float.TryParse(kmStr, out km) && km > 0)
                    {
                        float meters = (km * 1000);
                        float megaWatts = Hardcoded.LaserAntenna_PowerUsage(antennaDef, meters);

                        sb.Clear();
                        sb.PowerFormat(megaWatts).Append(" at ").DistanceFormat(meters).Append(" for ").Append(antennaDef.DisplayNameText).Append(antennaDef.CubeSize == MyCubeSize.Large ? " (large grid)" : " (small grid)");

                        PrintChat(sb.ToString(), FontsHandler.GreenSh);
                    }
                    else
                    {
                        PrintChat($"'{kmStr}' is not a number larger than 0.", FontsHandler.RedSh);
                    }
                }
                else
                {
                    PrintChat($"Need a distance in kilometers, e.g. {PrimaryCommand} 500", FontsHandler.RedSh);
                }
            }
            else
            {
                PrintChat("Need a reference Laser Antenna type block, equip or aim at one first then the command.", FontsHandler.RedSh);
            }
        }
    }
}