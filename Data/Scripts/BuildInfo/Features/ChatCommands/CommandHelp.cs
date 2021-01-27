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
            "\n  Blue wireframe over mountpoint is the side and point used for auto-rotation." +
            "\n  Red mountpoint has special rules attached to it and might not attach to other red mounts." +
            "\n" +
            "\n  Airtightness also uses the mount points system, if a mount point spans accross an entire cell face then that face is airtight." +
            "\n  Blocks in construction stages are never airtight." +
            "\n" +
            "\n" +
            "\n##### Numbered markings in text explained #####" +
            "\n" +
            "\n[1] Laser antenna power usage is linear up to 200km, after that it's a quadratic ecuation." +
            "\n   To calculate it at your needed distance, hold a laser antenna and type in chat: /bi laserpower <km>" +
            "\n" +
            "\n[2] No standalone means the block can't exist as the only block in the grid." +
            "\n   Blocks with no collisions also have this limitation." +
            "\n" +
            "\n[3] Explaining conveyor power usage:" +
            "\n   A hub is a conveyor hub or a block with ports (assembler, reactor, etc), but not conveyor tubes, those are one kind of conveyor lines." +
            "\n   A conveyor line is a connection between 2 hubs with or without conveyor tubes." +
            "\n   If a hub is on the line then it splits the line in 2 lines." +
            "\n   For example, 2 refineries that are placed to be connected with one port will form a conveyor line there." +
            "\n   Knowning all that, each conveyor line requires 0.1 W." + // HACK hardcoded per-line power from MyGridConveyorSystem.CalculateConsumption()
            "\n" +
            "\n" +
            "\n##### Inventory bar in ships #####" +
            "\nThe vanilla backpack icon+bar is altered to show the current ship's combined Cargo Containers fill." +
            "\nOptionally you can declare a group 'Cargo' to use for the bar (no matter the type)." +
            "\n" +
            "\n" +
            "\n##### Custom labels for toolbar slots per cockpit #####" +
            "\n  Using a cockpit's CustomData you can set custom label per slot using ini format." +
            "\n  This can be added anywhere in CustomData, but it must be in this order:" +
            "\n [Toolbar]" +
            "\n page-slot = label" +
            "\n (and more like above)" +
            "\n" +
            "\n Functional example:" +
            "\n [Toolbar]" +
            "\n 1-1 = Named the first slot!" +
            "\n 2-5 = <color=lime>Do stuff" +
            "\n" +
            "\n  You can also use textAPI's formatting:" +
            "\n    <color=red> or <color=255,0,0> (no end tag)." +
            "\n    <i> and </i> for italicized text." +
            "\n  Max line 128 chars including formatting, and multilines are removed.";

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
            sb.Append("  ");
            Main.Config.LockOverlayBind.Value.GetBinds(sb, specialChars: false);
            sb.Append("   (with block aimed at)").NewLine();
            sb.Append("     Keep overlay active so you can move around.").NewLine();

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