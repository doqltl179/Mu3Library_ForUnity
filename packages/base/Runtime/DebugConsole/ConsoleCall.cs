using System.Collections.Generic;

namespace Mu3Library.DebugConsole
{
    /// <summary>
    /// One run of a command that takes its words as they are: the words after the command's name,
    /// and a way to write to the console.
    /// </summary>
    public sealed class ConsoleCall
    {
        private readonly IDebugConsoleManager _console;

        public string CommandName { get; }
        public IReadOnlyList<string> Arguments { get; }



        internal ConsoleCall(IDebugConsoleManager console, string commandName, IReadOnlyList<string> arguments)
        {
            _console = console;
            CommandName = commandName;
            Arguments = arguments;
        }

        public void Print(string message) => _console.Print(message);
        public void PrintError(string message) => _console.PrintError(message);
    }
}
