using System;
using System.Collections.Generic;

namespace Mu3Library.DebugConsole
{
    /// <summary>
    /// Holds the console's commands, runs typed lines, and keeps what the console shows: the lines
    /// it ran, what commands printed, and, while it captures them, Unity's log messages.
    /// <see cref="DebugConsoleView"/> draws it.
    /// </summary>
    public interface IDebugConsoleManager
    {
        /// <summary>
        /// Whether this is the editor or a Development Build, where every command runs.
        /// </summary>
        public bool IsDevelopmentBuild { get; }

        /// <summary>
        /// Whether a release build opens the console and runs its <see cref="ConsoleAccess.Player"/>
        /// commands. Off by default, so a release build keeps the console closed until the game turns
        /// this on. A Development Build and the editor ignore it.
        /// </summary>
        public bool PlayerConsoleEnabled { get; set; }

        /// <summary>
        /// Whether the console can open: in the editor and a Development Build, or with
        /// <see cref="PlayerConsoleEnabled"/> on.
        /// </summary>
        public bool CanOpen { get; }

        public bool IsOpen { get; }

        /// <summary>
        /// Whether a command marked <see cref="ConsoleCommandOptions.AffectsProgress"/> ran since the
        /// console was created or <see cref="ResetProgressModified"/> was called.
        /// </summary>
        public bool HasModifiedProgress { get; }

        /// <summary>
        /// Most lines the console keeps; the oldest go first. 1000 by default.
        /// </summary>
        public int MaxLines { get; set; }

        public IReadOnlyList<ConsoleLine> Lines { get; }

        /// <summary>
        /// The lines run so far, oldest first, up to 50, for the view's up and down keys.
        /// </summary>
        public IReadOnlyList<string> History { get; }



        public bool Open();
        public void Close();
        public void Toggle();

        public void RegisterCommand(string name, Action handler, ConsoleCommandOptions options = null);
        public void RegisterCommand<T1>(string name, Action<T1> handler, ConsoleCommandOptions options = null);
        public void RegisterCommand<T1, T2>(string name, Action<T1, T2> handler, ConsoleCommandOptions options = null);
        public void RegisterCommand<T1, T2, T3>(string name, Action<T1, T2, T3> handler, ConsoleCommandOptions options = null);
        public void RegisterCommand<T1, T2, T3, T4>(string name, Action<T1, T2, T3, T4> handler, ConsoleCommandOptions options = null);

        /// <summary>
        /// Registers a command that takes the words of its line as they are, through
        /// <see cref="ConsoleCall.Arguments"/>.
        /// </summary>
        public void RegisterCommand(string name, Action<ConsoleCall> handler, ConsoleCommandOptions options = null);

        public bool UnregisterCommand(string name);
        public bool TryGetCommand(string name, out ConsoleCommand command);

        /// <summary>
        /// Adds every registered command to <paramref name="results"/>, with
        /// <paramref name="runnableOnly"/> only those this build and setting let run.
        /// </summary>
        public void GetCommands(List<ConsoleCommand> results, bool runnableOnly = true);

        /// <summary>
        /// Adds the names of the runnable commands that start with <paramref name="prefix"/> to
        /// <paramref name="results"/>, in order.
        /// </summary>
        public void GetCompletions(string prefix, List<string> results);

        /// <summary>
        /// Runs one line: a command name and its arguments. Adds the line, and its error when it
        /// fails, to <see cref="Lines"/>, and the line to <see cref="History"/>.
        /// </summary>
        public ConsoleResult Execute(string line);

        public void Print(string message);
        public void PrintError(string message);
        public void Clear();
        public void ResetProgressModified();
    }
}
