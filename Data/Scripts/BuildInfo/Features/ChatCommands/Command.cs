using System.Text;
using Digi.BuildInfo.Utilities;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public abstract class Command
    {
        /// <summary>
        /// The full command including <see cref="ChatCommandHandler.ModCommandPrefix"/>.
        /// </summary>
        public readonly string PrimaryCommand;

        /// <summary>
        /// All aliases (without mod command prefix) and includes the main one as the first, therefore never empty.
        /// Can be "" if it only expects the mod prefix command to work.
        /// </summary>
        public readonly string[] Aliases;

        protected BuildInfoMod Main => BuildInfoMod.Instance;

        public Command(params string[] aliases)
        {
            if(aliases == null || aliases.Length <= 0)
            {
                Log.Error($"{GetType().Name} No aliases given!");
                return;
            }

            foreach(string alias in aliases)
            {
                if(alias.IndexOf(' ') != -1)
                {
                    Log.Error($"{GetType().Name}: Alias '{alias}' has spaces! Use arguments instead.");
                    return;
                }
            }

            Aliases = aliases;
            PrimaryCommand = ChatCommandHandler.ModCommandPrefix + " " + Aliases[0];
            Main.ChatCommandHandler.AddCommand(this);
        }

        /// <summary>
        /// Shows a chat message with the current command as sender.
        /// <para><paramref name="commandFont"/> will color only the sender (in this case the command text)</para>
        /// </summary>
        protected void PrintChat(string message, string commandFont = FontsHandler.WhiteSh)
        {
            Utils.ShowColoredChatMessage(PrimaryCommand, message, senderFont: commandFont);
        }

        public void PrintHelpToChat()
        {
            StringBuilder sb = new StringBuilder(256);
            PrintHelp(sb);
            PrintChat(sb.ToString());
        }

        /// <summary>
        /// Execute the command with given args.
        /// NOTE: <paramref name="parser"/> can be null.
        /// </summary>
        public abstract void Execute(Arguments args);

        /// <summary>
        /// Calls <see cref="Execute(Arguments)"/> with null argument.
        /// </summary>
        public void ExecuteNoArgs() => Execute(null);

        /// <summary>
        /// Called by the help command for info about this command.
        /// Do not clear the <paramref name="sb"/>.
        /// </summary>
        public abstract void PrintHelp(StringBuilder sb);

        protected void AppendCommands(StringBuilder sb, string args = null, bool all = true)
        {
            if(all)
            {
                foreach(string alias in Aliases)
                {
                    sb.Append(ChatCommandHandler.ModCommandPrefix).Append(' ').Append(alias);
                    if(args != null)
                        sb.Append(' ').Append(args);
                    sb.NewLine();
                }
            }
            else
            {
                sb.Append(PrimaryCommand);
                if(args != null)
                    sb.Append(' ').Append(args);
                sb.NewLine();
            }
        }
    }
}