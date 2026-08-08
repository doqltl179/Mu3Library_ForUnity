using System;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Flow
{
    /// <summary>
    /// Runs its commands side by side and finishes once the last one is done.
    /// <br/> Use it to hold together the pieces of one board moment, the shake and the item that
    /// <br/> falls into it, so the board keeps them as a single thing it can also cancel as one.
    /// <br/> A command that gives up does not stop the others, they were started to run together;
    /// <br/> put the ones that depend on each other in a <see cref="SequenceCommand"/> instead.
    /// </summary>
    public class ParallelCommand : CompositeBoardCommand
    {
        private readonly Action _onComplete;



        public ParallelCommand(params IBoardCommand[] commands)
            : this(null, commands)
        {
        }

        /// <param name="onComplete">Called once every command is done, never on a canceled group.</param>
        /// <param name="commands">The commands, all started on the same frame.</param>
        public ParallelCommand(Action onComplete, params IBoardCommand[] commands)
            : base(commands)
        {
            _onComplete = onComplete;
        }

        protected override void OnRun()
        {
            Step(0.0f);
        }

        protected override void OnUpdate(float deltaTime)
        {
            Step(deltaTime);
        }

        protected override void OnComplete()
        {
            _onComplete?.Invoke();
        }

        private void Step(float deltaTime)
        {
            bool hasWorkLeft = false;

            for (int index = 0; index < _commands.Length; index++)
            {
                if (BoardCommandRunner.Advance(_commands[index], deltaTime))
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
