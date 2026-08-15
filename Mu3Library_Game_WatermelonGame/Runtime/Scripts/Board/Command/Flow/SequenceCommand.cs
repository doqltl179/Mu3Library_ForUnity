using System;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Flow
{
    /// <summary>
    /// Runs its commands one after another and finishes with the last one.
    /// <br/> This is what turns single steps into a scripted moment on the board, a bonus item
    /// <br/> that appears, waits, and then pays out:
    /// <br/> <c>new SequenceCommand(new SpawnItemCommand(...), new DelayCommand(1.0f), new AddScoreCommand(...))</c>
    /// <br/> A step that gives up cancels the sequence, so the steps after it never run on a board
    /// <br/> that is no longer in the state they were written for.
    /// </summary>
    public class SequenceCommand : CompositeBoardCommand
    {
        protected int _currentIndex;
        /// <summary>
        /// The step the sequence stands on, which is <see cref="CompositeBoardCommand.Count"/>
        /// <br/> once every step is done.
        /// </summary>
        public int CurrentIndex => _currentIndex;

        /// <summary>
        /// The step that is running, null before the sequence started and after it finished.
        /// </summary>
        public IBoardCommand Current => GetCommand(_currentIndex);

        public SequenceCommand(params IBoardCommand[] commands)
            : this(null, commands)
        {
        }

        /// <param name="onComplete">Called once every step is done, never on a canceled sequence.</param>
        /// <param name="commands">The steps, run in the order they are given.</param>
        public SequenceCommand(Action onComplete, params IBoardCommand[] commands)
            : base(onComplete, commands)
        {
        }

        protected override void Step(float deltaTime)
        {
            while (_currentIndex < _commands.Length)
            {
                IBoardCommand command = _commands[_currentIndex];

                if (BoardCommandRunner.Advance(command, deltaTime))
                {
                    return;
                }

                bool isCanceled = BoardCommandRunner.IsCanceled(command);

                // The step is released as soon as it is done, the sequence can be a long one.
                command?.Dispose();
                _currentIndex++;

                if (isCanceled)
                {
                    Cancel();
                    return;
                }

                // The next step starts on this frame, but no time has passed for it yet.
                deltaTime = 0.0f;
            }

            Complete();
        }
    }
}
