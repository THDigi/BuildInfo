using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class ChatCommandHandler : ModComponent
    {
        public const string ModCommandPrefix = "/bi";
        public const string HelpAlternative = "/buildinfo";

        /// <summary>
        /// Alias to command object for fast lookup.
        /// Does not expect <see cref="ModCommandPrefix"/>
        /// </summary>
        public readonly Dictionary<string, Command> AliasToCommand = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

        public readonly List<Command> UniqueCommands = new List<Command>();

        public CommandHelp CommandHelp;
        public CommandServerInfo CommandServerInfo;
        public CommandModLink CommandModLink;
        public CommandWorkshop CommandWorkshop;
        public CommandGetBlock CommandGetBlock;
        public CommandQuickMenu CommandQuickMenu;
        public CommandToolbarCustomLabel CommandToolbarCustomLabel;
        public CommandLaserPower CommandLaserPower;
        public CommandReloadConfig CommandReloadConfig;

        public const StringComparison StringCompare = StringComparison.OrdinalIgnoreCase;

        readonly Arguments args = new Arguments();
        bool IgnoreChatEvent;

        public ChatCommandHandler(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            // affects order in help menu
            CommandHelp = new CommandHelp();
            CommandReloadConfig = new CommandReloadConfig();
            CommandServerInfo = new CommandServerInfo();
            new CommandConveyorNetwork();
            CommandModLink = new CommandModLink();
            CommandWorkshop = new CommandWorkshop();
            new CommandChangelog();
            CommandGetBlock = new CommandGetBlock();
            new CommandGetGroup();
            new CommandShipMods();
            new CommandSort();
            CommandQuickMenu = new CommandQuickMenu();
            CommandToolbarCustomLabel = new CommandToolbarCustomLabel();
            new CommandToolbarErasePrefix();
            CommandLaserPower = new CommandLaserPower();
            new CommandLCDResolution();
            new CommandMeasureText();
            new CommandScreenCoords();
            new CommandProfile();
            new CommandClearCache();

            MyAPIGateway.Utilities.MessageEntered += MessageEntered;
            Main.GameConfig.FirstSpawn += FirstSpawn;
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.Utilities.MessageEntered -= MessageEntered;

            if(!Main.ComponentsRegistered)
                return;

            Main.GameConfig.FirstSpawn -= FirstSpawn;
        }

        void MessageEntered(string text, ref bool send)
        {
            try
            {
                if(IgnoreChatEvent)
                    return;

                if(!EnteredMessage(text))
                {
                    send = false;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="message"></param>
        /// <returns>whether the entered message is to be sent/shown</returns>
        bool EnteredMessage(string text)
        {
            if(text.StartsWith(HelpAlternative, StringCompare))
            {
                CommandHelp.ExecuteNoArgs();
                return false;
            }

            if(!text.StartsWith(ModCommandPrefix, StringCompare))
                return true; // not this mod's commands, skip

            if(!args.TryParse(text))
                return true; // empty text or no args

            string alias = (args.Count > 1 ? args.Get(1) : "");
            Command cmd;

            if(AliasToCommand.TryGetValue(alias, out cmd))
            {
                args.IndexOffset = 2; // skip past main and sub-command so that parameters start from index 0
                cmd.Execute(args);
            }
            else
            {
                Utils.ShowColoredChatMessage(BuildInfoMod.ModName, $"Unknown command: {ModCommandPrefix} {alias}", FontsHandler.RedSh);
                Utils.ShowColoredChatMessage(BuildInfoMod.ModName, $"For commands list, type: {ModCommandPrefix}", FontsHandler.RedSh);
            }
            return false;
        }

        /// <summary>
        /// Adds a command.
        /// Not to be used manually, <see cref="Command"/> calls it automatically.
        /// </summary>
        public void AddCommand(Command cmd)
        {
            UniqueCommands.Add(cmd);

            foreach(string alias in cmd.Aliases)
            {
                if(AliasToCommand.ContainsKey(alias))
                {
                    Log.Error($"{cmd.GetType().Name} tried to register alias '{alias}' which is already registered by {AliasToCommand[alias].GetType().Name}");
                    continue;
                }

                AliasToCommand.Add(alias, cmd);
            }
        }

        void FirstSpawn()
        {
            try
            {
                IgnoreChatEvent = true;
                CheckChatMessageForcing();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                IgnoreChatEvent = false;
            }
        }

        void CheckChatMessageForcing()
        {
            // normally this starts true, we're just checking if a mod sets it true for any random message
            bool send = false;

            // this only calls the modAPI events, it does not send any actual message
            MyAPIUtilities.Static.EnterMessage(MyAPIGateway.Multiplayer.MyId, ModCommandPrefix, ref send);

            if(send) // a mod set this to true which is not ok, report...
            {
                const string Prefix = "A mod is forcing chat commands to be visible!";

                const string MessageChat = Prefix + " The SE log has further instructions.";

                const string MessageLog = Prefix + "\n" +
                                          "Cannot identify it programatically because of lack of access, however, you can probably do it manually:\n" +
                                          " 1. Have something that can find text in multiple files, Notepad++ can do this for example.\n" +
                                          " 2. Now find-in-files for:\n" +
                                          "   - Text: Utilities.MessageEntered\n" +
                                          "   - File filter: *.cs\n" +
                                          "   - Folder: nagivate to Steam folder then inside \\steamapps\\workshop\n" +
                                          " 3. Now you have a list of files that mess with the chat event, make a list of all the mod workshop IDs:" +
                                          "   Example path you might see: C:\\Steam\\steamapps\\workshop\\content\\244850\\514062285\\Data\\Scripts\\BuildInfo\\Features\\ChatCommands\\ChatCommandHandler.cs\n" +
                                          "   This is for this very mod, the mod's workshopId is right after 244850\\, make a list of all of the unique ones.\n" +
                                          "   If you can understand general programming terms, you could try to find the offending mod, the thing to look for is the 'ref bool' from the parameters if it's being set to true.\n" +
                                          "   Otherwise you can narrow it down by having BuildInfo + all of those mods, then binary search by removing half of the mods (except BuildInfo) and loading the world to see if that half has it, if yes then repeat, otherwise replace with the other half of mods and repeat." +
                                          "   You can also contact me (@m_digi on discord) and send me the above list of mods and I can check them for you.";

                Log.Error(MessageLog, null);
                Utils.ShowColoredChatMessage(BuildInfoMod.ModName, MessageChat, FontsHandler.RedSh);
            }
            else
            {
                Log.Info("Checked chat handlers that might force chat messages to be visible, no problems found.");
            }
        }
    }
}