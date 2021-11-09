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
            if(BuildInfoMod.IsDevMod)
            {
                StringBuilder sb = new StringBuilder(128);

                for(int i = 0; i < args.Count; i++)
                {
                    sb.Append(args.Get(i)).Append(" ");
                }

                if(sb.Length > 0)
                    sb.Length -= 1; // remove last space

                Vector2D size = Main.TextAPI.GetStringSize(sb);
                Vector2D spaceSize = Main.TextAPI.GetStringSize(new StringBuilder(" ")); // TODO: cache result?

                double spacesWidth = size.X / spaceSize.X;

                PrintChat($"Text size X={size.X.ToString("N6")}, Y={size.Y.ToString("N6")}, spaces-width={spacesWidth.ToString("N3")} for: \"{sb.ToString()}\"", FontsHandler.GreenSh);
            }
            else
            {
                PrintChat("How do you know about this huuuh? :P", FontsHandler.RedSh);
            }
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