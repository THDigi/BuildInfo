using System.Text;
using Digi.BuildInfo.Utilities;
using VRageMath;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandMeasureText : Command
    {
        public CommandMeasureText() : base("measure")
        {
        }

        public override void Execute(Arguments args)
        {
            string text = args.GetRestAsText(0);

            Vector2D size = Main.TextAPI.GetStringSize(text);
            Vector2D spaceSize = Main.TextAPI.GetStringSize(" ");
            double spacesWidth = size.X / spaceSize.X;

            PrintChat($"Text size X={size.X.ToString("N6")}, Y={size.Y.ToString("N6")}, spaces-width={spacesWidth.ToString("N3")} for: \"{text}\"", FontsHandler.GreenSh);
        }

        public override void PrintHelp(StringBuilder sb)
        {
            if(BuildInfoMod.IsDevMod)
            {
                sb.Append(MainAlias).Append(" <text>").NewLine();
                sb.Append("  Measures given text with TextAPI.").NewLine();
            }
        }
    }
}