using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using VRageMath;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandLCDResolution : Command
    {
        public CommandLCDResolution() : base("lcdres")
        {
        }

        public override void PrintHelp(StringBuilder sb)
        {
            AppendCommands(sb, "<width> <height> <resolution>");
            sb.Append("  (Modder tool) calculate screen area surface size.").NewLine();
        }

        public override void Execute(Arguments args)
        {
            int width, height, res;

            if(!int.TryParse(args.Get(0), out width) || !int.TryParse(args.Get(1), out height) || !int.TryParse(args.Get(2), out res))
            {
                PrintChat($"Invalid input, expected 3 integers for width/height/resolution, with space separator.", FontsHandler.RedSh);
                return;
            }

            Hardcoded.TextSurfaceInfo info = Hardcoded.TextSurface_GetInfo(width, height, res);

            PrintChat($"LCD surface size: {info.SurfaceSize.X.ToString("0.######")} x {info.SurfaceSize.Y.ToString("0.######")}", FontsHandler.GreenSh);

            if(width <= 0 || height <= 0 || res <= 0)
                PrintChat($"Using negative or 0 values can cause problems.", FontsHandler.YellowSh);

            if(!MathHelper.IsPowerOfTwo(res))
                PrintChat($"Resolution ({res.ToString()}) is not power of 2, it could cause problems. Next power of 2: {MathHelper.GetNearestBiggerPowerOfTwo(res).ToString()}", FontsHandler.YellowSh);
        }
    }
}