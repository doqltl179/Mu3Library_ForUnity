using System;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Flow
{
    /// <summary>
    /// Runs its commands side by side and finishes once the last one is done.
    /// <br/> Use it to hold together the pieces of one board moment, the shake and the item that
    /// <br/> falls into it, so the board keeps them as a single thing it can also cancel as one.
    /// <br/> A command that gives up does not stop the others, they were started to run together;
    /// <br/> put the ones that depend on each other in a <see cref="SequenceCommand"/> instead.
    /// <br/> On a board, a handler of what one command raised that ends the game or prepares the
    /// <br/> board again stops the group there: that command finishes its step, and the ones after
    /// <br/> it are not advanced in that frame.
    /// </summary>
    public class ParallelCommand : CompositeBoardCommand
    {
        public ParallelCommand(params IBoardCommand[] commands)
            : this(null, commands)
        {
        }

        /// <param name="onComplete">Called once every command is done, never on a canceled group.</param>
        /// <param name="commands">The commands, all started on the same frame.</param>
        public ParallelCommand(Action onComplete, params IBoardCommand[] commands)
            : base(onComplete, commands)
        {
        }

        protected override void Step(float deltaTime)
        {
            bool hasWorkLeft = false;

            for (int index = 0; index < _commands.Length; index++)
            {
                IBoardCommand command = _commands[index];
                if (BoardCommandRunner.IsFinished(command))
                {
                    continue;
                }

                // A handler of what an earlier command raised can stop the board, and the commands
                // left then wait, as the commands after the group on the board do.
                if (!CanContinueStep())
                {
                    return;
                }

                if (BoardCommandRunner.Advance(command, deltaTime))
                {
                    hasWorkLeft = true;
                }
            }

            if (hasWorkLeft)
            {
                return;
            }

            Complete();
        }
    }
}
