namespace Mu3Library.DebugConsole
{
    /// <summary>
    /// Who may run a command.
    /// </summary>
    public enum ConsoleAccess
    {
        /// <summary>
        /// Runs only in the editor and in a Development Build. A release build never runs it.
        /// </summary>
        Developer = 0,

        /// <summary>
        /// A command the game offers its players on purpose, such as a convenience or a cheat it
        /// allows. It runs in the editor and a Development Build, and in a release build once the game
        /// turns on <see cref="DebugConsoleManager.PlayerConsoleEnabled"/>.
        /// </summary>
        Player = 1,
    }
}
