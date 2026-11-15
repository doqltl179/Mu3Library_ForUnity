using System;

namespace Mu3Library.Game.WatermelonGame.Board.Command
{
    /// <summary>
    /// A piece of board work the board runs for the player, a merge to begin with.
    /// <br/> This is the whole contract the board needs: it runs a command that has not started yet,
    /// <br/> and disposes it once it reports itself finished.
    /// <br/> Implement it directly to own every bit of the command state, derive from
    /// <see cref="BoardCommand"/> to be handed the state and the lifecycle hooks instead.
    /// <br/> Add <see cref="IUpdatableBoardCommand"/> to be advanced every frame, and
    /// <see cref="ICancelableBoardCommand"/> to be stopped before the end.
    /// </summary>
    public interface IBoardCommand : IDisposable
    {
        /// <summary>
        /// True from the moment the command started until it finished.
        /// <br/> The board calls <see cref="Run"/> while this is false, so a command that reports
        /// <br/> nothing here is run again on the next frame.
        /// </summary>
        public bool IsRunning { get; }

        /// <summary>
        /// True once the command finished and can be dropped, whether it ran to its end or
        /// <br/> was canceled. The board disposes the command as soon as it is set.
        /// </summary>
        public bool IsCompleted { get; }



        /// <summary>
        /// Starts the command. The board only calls it while <see cref="IsRunning"/> and
        /// <see cref="IsCompleted"/> are both false.
        /// </summary>
        public void Run();
    }
}
