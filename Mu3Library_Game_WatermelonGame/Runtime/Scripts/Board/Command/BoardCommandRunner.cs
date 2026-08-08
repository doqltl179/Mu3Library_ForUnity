namespace Mu3Library.Game.WatermelonGame.Board.Command
{
    /// <summary>
    /// Drives a single command through the optional parts of its contract.
    /// <br/> The board runs its own queue through this, and so do the commands that hold other
    /// <br/> commands, which is what keeps one command list and a command group behaving alike.
    /// <br/> Use it to host commands somewhere else as well, in a menu or a tutorial that has no
    /// <br/> board of its own.
    /// </summary>
    public static class BoardCommandRunner
    {
        /// <summary>
        /// Carries a command one step further: starts it when it has not started yet, and updates
        /// <br/> it when it is running and asks to be updated.
        /// </summary>
        /// <returns>True while the command still has work left, false once it is finished.</returns>
        public static bool Advance(IBoardCommand command, float deltaTime)
        {
            if (IsFinished(command))
            {
                return false;
            }

            if (!command.IsRunning)
            {
                command.Run();
            }
            else if (command is IUpdatableBoardCommand updatableCommand)
            {
                updatableCommand.Update(deltaTime);
            }

            return !command.IsCompleted;
        }

        /// <summary>
        /// Stops a command that can be stopped.
        /// </summary>
        /// <returns>False when the command does not support being canceled, so the caller knows
        /// <br/> that only <see cref="System.IDisposable.Dispose"/> can end it.</returns>
        public static bool Cancel(IBoardCommand command)
        {
            if (command is not ICancelableBoardCommand cancelableCommand)
            {
                return false;
            }

            cancelableCommand.Cancel();
            return true;
        }

        /// <summary>
        /// True when the command finished without reaching its end.
        /// <br/> A command that cannot be canceled never reports it.
        /// </summary>
        public static bool IsCanceled(IBoardCommand command)
            => command is ICancelableBoardCommand { IsCanceled: true };

        /// <summary>
        /// True when there is nothing left to run, which a missing command also counts as.
        /// </summary>
        public static bool IsFinished(IBoardCommand command)
            => command == null || command.IsCompleted;
    }
}
