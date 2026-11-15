namespace Mu3Library.Game.WatermelonGame.Board.Command
{
    /// <summary>
    /// A command that can be stopped before it reached its end, so the board can drop it when
    /// <br/> the game ends, when it is prepared again, or when the project simply asks it to.
    /// <br/> A command without this interface is only ever dropped through
    /// <see cref="System.IDisposable.Dispose"/>.
    /// </summary>
    public interface ICancelableBoardCommand : IBoardCommand
    {
        /// <summary>
        /// True when the command finished without reaching its end.
        /// <see cref="IBoardCommand.IsCompleted"/> is set as well, a canceled command is finished.
        /// </summary>
        public bool IsCanceled { get; }



        /// <summary>
        /// Stops the command. It does nothing once the command is finished.
        /// </summary>
        public void Cancel();
    }
}
