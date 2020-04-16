using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandHelp : Command
    {
        private const string FOOTER =
            "\n##### Asterisk in labels explained #####" +
            "\n  The asterisks on the labels (e.g. Power usage*: 10 W) means that the value is calculated from hardcoded values taken from the game source, they might become inaccurate with updates." +
            "\n" +
            "\n" +
            "\n##### Mount points & airtightness explained #####" +
            "\n" +
            "\n  Mount points define areas that can be attached to other block's mount points." +
            "\n  Orange mount point is the one used for auto-rotation." +
            "\n" +
            "\n  Airtightness also uses the mount points system, if a mount point spans accross an entire grid cell face then that face is airtight." +
            "\n" +
            "\n" +
            "\n##### Numbered markings in text explained #####" +
            "\n" +
            "\n[1] Laser antenna power usage is linear up to 200km, after that it's a quadratic ecuation." +
            "\n   To calculate it at your needed distance, hold a laser antenna and type in chat: /bi laserpower <km>" +
            "\n" +
            "\n[2] No standalone means the block can't exist as the only block in the grid." +
            "\n   Blocks with no collisions also have this limitation." +
            "\n";

        private StringBuilder sb = new StringBuilder(1024);

        public CommandHelp() : base("", "help")
        {
        }

        public override void Execute(Arguments args)
        {
            sb.Clear();
            sb.Append("##### Chat Commands #####").NewLine();
            sb.NewLine();

            foreach(var handler in Main.ChatCommandHandler.Commands)
            {
                handler.PrintHelp(sb);
                sb.NewLine();
            }

            sb.NewLine();
            sb.NewLine();
            sb.Append("##### Config #####").NewLine();
            sb.Append("You can edit the config ingame via textAPI Mod menu").NewLine();
            sb.Append("Open chat and press F2.").NewLine();
            sb.NewLine();
            sb.Append("Or you can edit the config file at:").NewLine();
            sb.Append(@"  %appdata%\SpaceEngineers\Storage").NewLine();
            sb.Append(@"    \").Append(Log.WorkshopId).Append(@".sbm_BuildInfo\settings.cfg").NewLine();
            sb.NewLine();
            sb.Append("And can be reloaded with: ").Append(Main.ChatCommandHandler.CommandReloadConfig.MainAlias).NewLine();
            sb.NewLine();
            sb.NewLine();
            sb.NewLine();
            sb.Append("##### Hotkeys #####").NewLine();

            sb.NewLine();
            sb.Append("  ");
            Main.Config.MenuBind.Value.GetBinds(sb, specialChars: false);
            sb.NewLine();
            sb.Append("     Show/hide quick menu.").NewLine();

            sb.NewLine();
            sb.Append("  ");
            Main.Config.CycleOverlaysBind.Value.GetBinds(sb, specialChars: false);
            sb.Append("   (with block equipped or aimed)").NewLine();
            sb.Append("     Cycles overlay draw").NewLine();

            sb.NewLine();
            sb.Append("  ");
            Main.Config.ToggleTransparencyBind.Value.GetBinds(sb, specialChars: false);
            sb.Append("   (with block equipped or aimed)").NewLine();
            sb.Append("     Toggle transparent model").NewLine();

            sb.NewLine();
            sb.Append("  ");
            Main.Config.FreezePlacementBind.Value.GetBinds(sb, specialChars: false);
            sb.Append("   (with block equipped)").NewLine();
            sb.Append("     Toggle freeze position").NewLine();

            sb.NewLine();
            sb.Append("  ");
            Main.Config.BlockPickerBind.Value.GetBinds(sb, specialChars: false);
            sb.Append("   (with block aimed at)").NewLine();
            sb.Append("     Get the aimed block in your toolbar.").NewLine();

            sb.NewLine();
            sb.Append(FOOTER);

            MyAPIGateway.Utilities.ShowMissionScreen(BuildInfoMod.MOD_NAME + " help", null, null, sb.ToString(), null, "Close");
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(ChatCommandHandler.MAIN_COMMAND).NewLine();
            sb.Append(ChatCommandHandler.MAIN_COMMAND).Append(" help").NewLine();
            sb.Append("  Shows this window.").NewLine();
        }
    }
}