using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class ChatCommandHandler : ModComponent
    {
        public const string MAIN_COMMAND = "/bi";
        public const string HELP_ALT = "/buildinfo";

        public readonly List<Command> Commands = new List<Command>();
        public readonly Dictionary<string, Command> AliasToCommand = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

        public CommandHelp CommandHelp;
        public CommandModLink CommandModLink;
        public CommandWorkshop CommandWorkshop;
        public CommandChangelog CommandChangelog;
        public CommandGetBlock CommandGetBlock;
        public CommandShipMods CommandShipMods;
        public CommandQuickMenu CommandQuickMenu;
        public CommandLaserPower CommandLaserPower;
        public CommandReloadConfig CommandReloadConfig;
        public CommandClearCache CommandClearCache;
        public CommandMeasureText CommandMeasureText;

        private readonly Arguments args = new Arguments();

        public ChatCommandHandler(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            CommandHelp = new CommandHelp();
            CommandModLink = new CommandModLink();
            CommandWorkshop = new CommandWorkshop();
            CommandChangelog = new CommandChangelog();
            CommandGetBlock = new CommandGetBlock();
            CommandShipMods = new CommandShipMods();
            CommandQuickMenu = new CommandQuickMenu();
            CommandLaserPower = new CommandLaserPower();
            CommandReloadConfig = new CommandReloadConfig();
            CommandClearCache = new CommandClearCache();
            CommandMeasureText = new CommandMeasureText();

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
                if(text.StartsWith(HELP_ALT))
                {
                    CommandHelp.ExecuteNoArgs();
                    return;
                }

                if(!text.StartsWith(MAIN_COMMAND))
                    return;

                if(!args.TryParse(text))
                    return;

                send = false;

                var alias = (args.Count > 1 ? args.Get(1) : "");
                Command cmd;

                if(AliasToCommand.TryGetValue(alias, out cmd))
                {
                    args.IndexOffset = 2; // skip past main and sub-command so that parameters start from index 0
                    cmd.Execute(args);
                }
                else
                {
                    Utils.ShowColoredChatMessage(BuildInfoMod.MOD_NAME, $"Unknown command: {MAIN_COMMAND} {alias}", FontsHandler.RedSh);
                    Utils.ShowColoredChatMessage(BuildInfoMod.MOD_NAME, $"For commands list, type: {MAIN_COMMAND}", FontsHandler.RedSh);
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
            Commands.Add(cmd);

            foreach(var alias in cmd.Aliases)
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