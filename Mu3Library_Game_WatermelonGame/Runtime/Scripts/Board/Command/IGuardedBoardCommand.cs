using System;

namespace Mu3Library.Game.WatermelonGame.Board.Command
{
    /// <summary>
    /// A command that carries several pieces of work within one step, the way a command group
    /// <br/> advances the commands it holds, and stops between them when its host asks it to.
    /// <br/> The board hands it a guard when it accepts the command. The guard turns false once the
    /// <br/> board stops running, so a handler that ends the game or prepares the board again stops
    /// <br/> the group the way it stops the board's own list: the piece that raised the event
    /// <br/> finishes its step, and the pieces after it are not advanced.
    /// <br/> A command that never gets a guard, one run through <see cref="BoardCommandRunner"/>
    /// <br/> outside a board, carries every piece of its step.
    /// </summary>
    public interface IGuardedBoardCommand : IBoardCommand
    {
        /// <summary>
        /// Sets what the command asks before each further piece of work within a step.
        /// </summary>
        /// <param name="canContinueStep">Returns false when the step has to stop there; null lets every step run to its end.</param>
        public void SetStepGuard(Func<bool> canContinueStep);
    }
}
