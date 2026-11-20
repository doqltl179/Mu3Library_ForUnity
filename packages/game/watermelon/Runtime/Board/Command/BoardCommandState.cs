namespace Mu3Library.Game.WatermelonGame.Board.Command
{
    /// <summary>
    /// Where a <see cref="BoardCommand"/> stands, meant for logging and for a UI that follows
    /// <br/> the board work instead of reading the single flags.
    /// </summary>
    public enum BoardCommandState
    {
        /// <summary>
        /// Enqueued, waiting for the board to start it.
        /// </summary>
        Pending,

        /// <summary>
        /// Started and not finished yet.
        /// </summary>
        Running,

        /// <summary>
        /// Finished after reaching its end.
        /// </summary>
        Completed,

        /// <summary>
        /// Finished without reaching its end.
        /// </summary>
        Canceled,
    }
}
