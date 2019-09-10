using System.Text;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public abstract class Command
    {
        public readonly string[] Aliases;
        public readonly string MainAlias;

        protected BuildInfoMod Main => BuildInfoMod.Instance;

        public Command(params string[] commands)
        {
            Aliases = commands;
            MainAlias = ChatCommandHandler.MAIN_COMMAND + " " + Aliases[0];
            Main.ChatCommandHandler.AddCommand(this);
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
    }
}