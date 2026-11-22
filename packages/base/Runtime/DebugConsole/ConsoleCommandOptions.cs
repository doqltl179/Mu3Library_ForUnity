namespace Mu3Library.DebugConsole
{
    /// <summary>
    /// How a command is listed and who may run it.
    /// </summary>
    public sealed class ConsoleCommandOptions
    {
        /// <summary>
        /// One line that <c>help</c> shows next to the command.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Who may run the command. <see cref="ConsoleAccess.Developer"/> by default.
        /// </summary>
        public ConsoleAccess Access { get; set; } = ConsoleAccess.Developer;

        /// <summary>
        /// Whether the command changes the game's progress, such as by handing out an item or
        /// skipping a stage. Running it sets <see cref="DebugConsoleManager.HasModifiedProgress"/> and raises
        /// <see cref="DebugConsoleManager.OnProgressModified"/>, so the game can mark the save or hold back
        /// achievements by its own rules.
        /// </summary>
        public bool AffectsProgress { get; set; }



        public ConsoleCommandOptions()
        {
        }

        public ConsoleCommandOptions(string description, ConsoleAccess access = ConsoleAccess.Developer, bool affectsProgress = false)
        {
            Description = description ?? string.Empty;
            Access = access;
            AffectsProgress = affectsProgress;
        }
    }
}
