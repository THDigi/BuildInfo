using System;
using System.Text;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utils;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;

namespace Digi.BuildInfo.Features
{
    public class ChatCommands : ClientComponent
    {
        #region Constants
        public const string CMD_BUILDINFO = "/bi";
        public const string CMD_BUILDINFO_OLD = "/buildinfo";
        public const string CMD_HELP = "/bi help";
        public const string CMD_RELOAD = "/bi reload";
        public const string CMD_CLEARCACHE = "/bi clearcache";
        public const string CMD_MODLINK = "/bi modlink";
        public const string CMD_WORKSHOP = "/bi workshop";
        public const string CMD_LASERPOWER = "/bi laserpower";
        public const string CMD_GETBLOCK = "/bi getblock";
        public const StringComparison CMD_COMPARE_TYPE = StringComparison.InvariantCultureIgnoreCase;

        private const string HELP_FORMAT =
            "Chat commands:\n" +
            "  /bi or /buildinfo\n" +
            "    shows this window or menu if you're holding a block.\n" +
            "  /bi help\n" +
            "    shows this window.\n" +
            "  /bi reload\n" +
            "    reloads the config.\n" +
            "  /bi getblock [1~9]\n" +
            "    Picks the aimed block to be placed in toolbar.\n" +
            "  /bi modlink\n" +
            "    Opens steam overlay with workshop on the selected block's mod.\n" +
            "  /bi workshop\n" +
            "    Opens steam overlay with workshop of this mod.\n" +
            "  /bi laserpower <km>\n" +
            "    Calculates power needed for equipped/aimed laser antenna\n" +
            "    at the specified range.\n" +
            "  /bi clearcache\n" +
            "    clears the block info cache, not for normal use.\n" +
            "\n" +
            "\n" +
            "Hotkeys:\n" +
            "  {0} show/hide menu\n" +
            "    Can be changed in config.\n" +
            "  {1} with block equipped/selected\n" +
            "    Cycles overlay draw.\n" +
            "  {2} with block equipped/selected\n" +
            "    Toggle transparent model.\n" +
            "  {3} with block equipped\n" +
            "    Toggle freeze position.\n" +
            "\n" +
            "\n" +
            "The config is located in:\n" +
            "%appdata%\\SpaceEngineers\\Storage\\514062285.sbm_BuildInfo\\settings.cfg\n" +
            "\n" +
            "\n" +
            "The asterisks on the labels (e.g. Power usage*: 10 W) means\n" +
            "  that the value is calculated from hardcoded values taken\n" +
            "  from the game source, they might become inaccurate with updates.\n" +
            "\n" +
            "\n" +
            "Mount points & airtightness explained:\n" +
            "\n" +
            "  Mount points define areas that can be attached to other\n" +
            "    block's mount points.\n" +
            "  Orange mount point is the one used for auto-rotation.\n" +
            "\n" +
            "  Airtightness also uses the mount points system, if a\n" +
            "    mount point spans accross an entire grid cell face\n" +
            "    then that face is airtight.\n" +
            "\n" +
            "\n" +
            "\nNumbered markings in text explained:" +
            "\n" +
            "[1] Laser antenna power usage is linear up to 200km, after\n" +
            "   that it's a quadratic ecuation.\n" +
            "   To calculate it at your needed distance, hold a laser antenna\n" +
            "   and type in chat: /bi laserpower <km>" +
            "\n";
        #endregion Constants

        public ChatCommands(Client mod) : base(mod)
        {
        }

        public override void RegisterComponent()
        {
            MyAPIGateway.Utilities.MessageEntered += MessageEntered;
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
        }

        private void MessageEntered(string msg, ref bool send)
        {
            try
            {
                if(msg.StartsWith(CMD_MODLINK, CMD_COMPARE_TYPE))
                {
                    send = false;
                    ShowSelectedBlocksModWorkshop();
                    return;
                }

                if(msg.StartsWith(CMD_WORKSHOP, CMD_COMPARE_TYPE))
                {
                    send = false;
                    ShowBuildInfoWorkshop();
                    return;
                }

                if(msg.StartsWith(CMD_RELOAD, CMD_COMPARE_TYPE))
                {
                    send = false;
                    QuickMenu.ReloadConfig(CMD_RELOAD);
                    return;
                }

                if(msg.StartsWith(CMD_CLEARCACHE, CMD_COMPARE_TYPE))
                {
                    send = false;
                    TextGeneration.CachedBuildInfoNotification.Clear();
                    TextGeneration.CachedBuildInfoTextAPI.Clear();
                    Utilities.ShowColoredChatMessage(CMD_CLEARCACHE, "Emptied block info cache.", MyFontEnum.Green);
                    return;
                }

                if(msg.StartsWith(CMD_LASERPOWER, CMD_COMPARE_TYPE))
                {
                    send = false;

                    if(EquipmentMonitor.BlockDef is MyLaserAntennaDefinition)
                    {
                        var arg = msg.Substring(CMD_LASERPOWER.Length);
                        float km;

                        if(float.TryParse(arg, out km))
                        {
                            var meters = (km * 1000);
                            var megaWatts = VanillaData.Hardcoded.LaserAntenna_PowerUsage((MyLaserAntennaDefinition)EquipmentMonitor.BlockDef, meters);
                            var s = new StringBuilder().Append(EquipmentMonitor.BlockDef.DisplayNameText).Append(" will use ").PowerFormat(megaWatts).Append(" at ").DistanceFormat(meters).Append(".");
                            Utilities.ShowColoredChatMessage(CMD_LASERPOWER, s.ToString(), MyFontEnum.Green);
                        }
                        else
                        {
                            Utilities.ShowColoredChatMessage(CMD_LASERPOWER, $"Need a distance in kilometers, e.g. {CMD_LASERPOWER} 500", MyFontEnum.Red);
                        }
                    }
                    else
                    {
                        Utilities.ShowColoredChatMessage(CMD_LASERPOWER, "Need a reference Laser Antenna, equip one first.", MyFontEnum.Red);
                    }

                    return;
                }

                if(msg.StartsWith(CMD_GETBLOCK, CMD_COMPARE_TYPE))
                {
                    send = false;
                    Mod.PickBlock.ParseCommand(msg);
                    return;
                }

                if(msg.StartsWith(CMD_BUILDINFO, CMD_COMPARE_TYPE) || msg.StartsWith(CMD_BUILDINFO_OLD, CMD_COMPARE_TYPE))
                {
                    send = false;

                    if(EquipmentMonitor.BlockDef == null || msg.StartsWith(CMD_HELP, CMD_COMPARE_TYPE))
                    {
                        ShowHelp();
                    }
                    else // no arg and block equipped/selected
                    {
                        Mod.QuickMenu.Shown = true;
                    }

                    return;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public void ShowBuildInfoWorkshop()
        {
            var id = Log.WorkshopId;

            if(id > 0)
            {
                var link = $"https://steamcommunity.com/sharedfiles/filedetails/?id={id}";

                // 0 in this method opens for the local client, hopefully they don't change that to "ALL" like they did on the chat message...
                MyVisualScriptLogicProvider.OpenSteamOverlay(link, 0);

                Utilities.ShowColoredChatMessage(CMD_WORKSHOP, $"Opened steam overlay with {link}", MyFontEnum.Green);
            }
            else
                Utilities.ShowColoredChatMessage(CMD_WORKSHOP, "Can't find mod workshop ID, probably it's a local mod?", MyFontEnum.Red);
        }

        public void ShowSelectedBlocksModWorkshop()
        {
            if(EquipmentMonitor.BlockDef != null)
            {
                if(!EquipmentMonitor.BlockDef.Context.IsBaseGame)
                {
                    var id = EquipmentMonitor.BlockDef.Context.GetWorkshopID();

                    if(id > 0)
                    {
                        var link = $"https://steamcommunity.com/sharedfiles/filedetails/?id={id}";

                        // 0 in this method opens for the local client, hopefully they don't change that to "ALL" like they did on the chat message...
                        MyVisualScriptLogicProvider.OpenSteamOverlay(link, 0);

                        Utilities.ShowColoredChatMessage(CMD_MODLINK, $"Opened steam overlay with {link}", MyFontEnum.Green);
                    }
                    else
                        Utilities.ShowColoredChatMessage(CMD_MODLINK, "Can't find mod workshop ID, probably it's a local mod?", MyFontEnum.Red);
                }
                else
                    Utilities.ShowColoredChatMessage(CMD_MODLINK, $"{EquipmentMonitor.BlockDef.DisplayNameText} is not added by a mod.", MyFontEnum.Red);
            }
            else
                Utilities.ShowColoredChatMessage(CMD_MODLINK, "No block selected/equipped.", MyFontEnum.Red);
        }

        public void ShowHelp()
        {
            var help = string.Format(HELP_FORMAT,
                Config.MenuBind.Value.GetBinds(),
                Config.CycleOverlaysBind.Value.GetBinds(),
                Config.ToggleTransparencyBind.Value.GetBinds(),
                Config.FreezePlacementBind.Value.GetBinds());

            MyAPIGateway.Utilities.ShowMissionScreen("BuildInfo Mod", "", "Various help topics", help, null, "Close");
        }
    }
}
