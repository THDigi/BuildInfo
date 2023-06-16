using System.Text;
using Digi.BuildInfo.Features.ToolbarInfo;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandHelp : Command
    {
        private const string Footer =
            SegmentPrefix + "TextInfo: Asterisk in labels explained" + SegmentSuffix +
            "\n  The asterisks on the labels (e.g. Power usage*: 10 W) means that the value is calculated from hardcoded values taken from the game source, they might become inaccurate with updates." +
            "\n" +
            "\n" +
            "\n" +
            "\n" + SegmentPrefix + "TextInfo: [i] at end of lines" + SegmentSuffix +
            "\n  Indicates that the line has a tooltip with a more detailed explanation or even more stats." +
            "\n  Open chat and the mouse appears, now you can aim at the line and the tooltip appears!" +
            "\n" +
            "\n" +
            "\n" +
            "\n" + SegmentPrefix + "Mount points & airtightness" + SegmentSuffix +
            "\n  Mount points define areas that can be attached to other block's mount points." +
            "\n  Blue wireframe over mountpoint is the side and point used for auto-rotation." +
            "\n  Red mountpoint has special rules attached to it and might not attach to other red mounts." +
            "\n" +
            "\n  Airtightness also uses the mount points system, if a mount point spans accross an entire cell face then that face is airtight." +
            "\n  Blocks in construction stages are never airtight." +
            "\n" +
            "\n" +
            "\n" +
            "\n" + SegmentPrefix + "Blueprint's stock build time" + SegmentSuffix +
            "\n  First off, it is impossible to tell which production block you're looking at from modAPI." +
            "\n  Therefore this stock time is only affected by world multipliers, but not any assembler/refinery efficiency or modules on it." +
            "\n  If it has a time range (Xs ~ Ys) it means there's multiple production blocks that can build this item." +
            "\n" +
            "\n" +
            "\n" +
            "\n" + SegmentPrefix + "Inventory bar in ships" + SegmentSuffix +
            "\n  The vanilla backpack icon+bar is altered to show the current ship's combined Cargo Containers fill." +
            "\n  Optionally you can declare a group 'Cargo' to use for the bar (no matter the type)." +
            "\n" +
            "\n" +
            "\n" + SegmentPrefix + "Toolbar block status with " + ToolbarStatusProcessor.CustomStatusTag + SegmentSuffix +
            "\n  Block statuses that have " + ToolbarStatusProcessor.CustomStatusTag + " top-right means that their status is provided by this mod, which for groups also means that all blocks are computed towards the final status text." +
            "\n  Any status that doesn't have that tag means it's the original status." +
            "\n  Feel free to request block+action statuses in the mod's discussions page." +
            "\n" +
            "\n" +
            "\n" + SegmentPrefix + "Custom labels for toolbar slots per cockpit" + SegmentSuffix +
            "\n  Easiest to use '{0}' chat command which will set CustomData accordingly." +
            "\n" +
            "\n  Or you can do it manually:" +
            "\n  Cockpit's CustomData can be used to set a custom label per slot, using ini format." +
            "\n  This can be added anywhere in CustomData, but it must be in this order:" +
            "\n [" + ToolbarCustomLabels.IniSection + "]" +
            "\n page-slot = label" +
            "\n ...and more like the line above, don't duplicate [" + ToolbarCustomLabels.IniSection + "]" +
            "\n" +
            "\n Functional example:" +
            "\n [" + ToolbarCustomLabels.IniSection + "]" +
            "\n 1-1 = Named the first slot!" +
            "\n 2-5 = <color=lime>Do stuff" +
            "\n" +
            "\n  You can also use textAPI's formatting:" +
            "\n    <color=red> or <color=255,0,0> (no end tag)." +
            "\n    <i> and </i> for italicized text." +
            "\n  Max line 128 chars including formatting, and multilines are removed." +
            "\n  NOTE: if non-ini things need to also be in CustomData then add ini at the top and " + ToolbarCustomLabels.IniDivider + " as the divider, parser will ignore everything after those 3 dashes.";

        private const string SegmentPrefix = "  "; //  menu;  windows
        private const string SegmentSuffix = ""; // " —————————";

        private StringBuilder sb = new StringBuilder(1024);

        public CommandHelp() : base("", "help")
        {
        }

        public override void Execute(Arguments args)
        {
            sb.Clear();
            sb.Append(SegmentPrefix).Append("Config").Append(SegmentSuffix).NewLine();
            sb.Append("You can edit the config ingame with TextAPI Mod menu").NewLine();
            sb.Append("Open chat and press F2.").NewLine();
            sb.NewLine();
            sb.Append("Or you can edit the config file at:").NewLine();
            sb.Append(@"  %appdata%\SpaceEngineers\Storage").NewLine();
            sb.Append(@"    \").Append(MyAPIGateway.Utilities.GamePaths.ModScopeName).Append(@"\").Append(Main.Config.Handler.FileName).NewLine();
            sb.NewLine();
            sb.Append("And can be reloaded with: ").Append(Main.ChatCommandHandler.CommandReloadConfig.MainAlias).NewLine();

            sb.NewLine();
            sb.NewLine();

            sb.Append(SegmentPrefix).Append("Hotkeys").Append(SegmentSuffix).NewLine();

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
            sb.NewLine();

            sb.Append(SegmentPrefix).Append("Chat Commands").Append(SegmentSuffix).NewLine();
            sb.NewLine();

            foreach(Command handler in Main.ChatCommandHandler.Commands)
            {
                int preLen = sb.Length;
                handler.PrintHelp(sb);
                if(sb.Length > preLen)
                    sb.NewLine();
            }

            sb.NewLine();

            sb.AppendFormat(Footer, Main.ChatCommandHandler.CommandToolbarCustomLabel.MainAlias);

            MyAPIGateway.Utilities.ShowMissionScreen($"{BuildInfoMod.ModName} help", null, null, sb.ToString(), null, "Close");
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(ChatCommandHandler.HelpAlternative).NewLine();
            sb.Append(ChatCommandHandler.MainCommandPrefix).NewLine();
            sb.Append(ChatCommandHandler.MainCommandPrefix).Append(" help").NewLine();
            sb.Append("  Shows this window.").NewLine();
        }
    }
}