using System.Text;
using Digi.BuildInfo.Utilities;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandProfile : Command
    {
        public CommandProfile() : base("profile")
        {
        }

        public override void PrintHelp(StringBuilder sb)
        {
            AppendCommands(sb, "[advanced]");
            sb.Append("  Shows/hides profiling info for this mod to help identify CPU impacts.").NewLine();
        }

        public override void Execute(Arguments args)
        {
            bool advancedArg = args.Get(0)?.Equals("advanced", System.StringComparison.OrdinalIgnoreCase) ?? false;

            // doing /bi profile then /bi profile advanced would maintain it visible
            // then doing /bi profile with or without advanced will turn it off

            if(advancedArg && !Main.ProfilerDisplay.Advanced)
            {
                Main.ProfilerDisplay.Advanced = true;
                Main.ProfilerDisplay.Show = true;
                PrintChat("Profiling shown (+advanced)", FontsHandler.WhiteSh);
            }
            else
            {
                Main.ProfilerDisplay.Advanced = false;
                Main.ProfilerDisplay.Show = !Main.ProfilerDisplay.Show;
                PrintChat($"Profiling {(Main.ProfilerDisplay.Show ? "shown" : "hidden")}", FontsHandler.WhiteSh);
            }
        }
    }
}