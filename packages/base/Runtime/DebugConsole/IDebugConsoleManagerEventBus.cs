using System;

namespace Mu3Library.DebugConsole
{
    public interface IDebugConsoleManagerEventBus
    {
        public event Action<ConsoleLine> OnLineAdded;
        public event Action OnCleared;

        /// <summary>
        /// The console opened (true) or closed (false).
        /// </summary>
        public event Action<bool> OnOpenChanged;

        /// <summary>
        /// A command marked <see cref="ConsoleCommandOptions.AffectsProgress"/> ran: its name.
        /// </summary>
        public event Action<string> OnProgressModified;

        /// <summary>
        /// A line ran: the command's name, or null when the line named none, and the result.
        /// </summary>
        public event Action<string, ConsoleResult> OnExecuted;
    }
}
