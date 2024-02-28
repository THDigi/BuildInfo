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

        private readonly Arguments args = new Arguments();

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
            new CommandProfile();
            new CommandClearCache();

            MyAPIGateway.Utilities.MessageEntered += MessageEntered;
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
        }

        void MessageEntered(string text, ref bool send)
        {
            try
            {
                if(text.StartsWith(HelpAlternative, StringCompare))
                {
                    CommandHelp.ExecuteNoArgs();
                    return;
                }

                if(!text.StartsWith(ModCommandPrefix, StringCompare))
                    return;

                if(!args.TryParse(text))
                    return;

                send = false;

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
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
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
    }
}