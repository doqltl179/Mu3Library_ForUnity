namespace Mu3Library.Game.WatermelonGame.Board.Command
{
    /// <summary>
    /// A command that needs more than the frame it started on, a timer or an effect that runs
    /// <br/> for a while.
    /// <br/> The board advances it every frame between <see cref="IBoardCommand.Run"/> and the
    /// <br/> moment it reports <see cref="IBoardCommand.IsCompleted"/>.
    /// <br/> A command without this interface is simply never updated, so a one-shot command
    /// <br/> does not have to carry an empty method.
    /// </summary>
    public interface IUpdatableBoardCommand : IBoardCommand
    {
        /// <summary>
        /// Carries the running command one frame further.
        /// </summary>
        /// <param name="deltaTime">The seconds that passed since the previous frame.</param>
        public void Update(float deltaTime);
    }
}
