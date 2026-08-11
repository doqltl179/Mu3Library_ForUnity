using System;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Flow
{
    /// <summary>
    /// A command built out of other commands, which the board sees as one.
    /// <br/> It owns the commands it was given: it stops and disposes every one of them when it is
    /// <br/> stopped itself, so they must not be enqueued on the board on their own as well.
    /// <br/> Derive from it to arrange the children in an order this package does not carry:
    /// <br/> override <see cref="Step"/> to carry the group one step further through
    /// <see cref="BoardCommandRunner"/>, or override <see cref="BoardCommand.OnRun"/> and
    /// <see cref="BoardCommand.OnUpdate"/> to drive the children entirely on your own.
    /// </summary>
    public abstract class CompositeBoardCommand : BoardCommand
    {
        private static readonly IBoardCommand[] EmptyCommands = new IBoardCommand[0];

        protected readonly IBoardCommand[] _commands;

        /// <summary>
        /// How many commands the group was built with, the empty entries included.
        /// </summary>
        public int Count => _commands.Length;

        private readonly Action _onComplete;



        protected CompositeBoardCommand(IBoardCommand[] commands)
            : this(null, commands)
        {
        }

        /// <param name="onComplete">Called once the group is done, never on a canceled one.</param>
        /// <param name="commands">The commands the group is built with.</param>
        protected CompositeBoardCommand(Action onComplete, IBoardCommand[] commands)
        {
            _commands = commands ?? EmptyCommands;
            _onComplete = onComplete;
        }

        /// <summary>
        /// One of the commands the group was built with, null when the index is out of range.
        /// </summary>
        public IBoardCommand GetCommand(int index)
            => index >= 0 && index < _commands.Length ? _commands[index] : null;

        protected override void OnRun()
        {
            // The group starts on the frame it was run on, with no time passed for it yet.
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

        /// <summary>
        /// Carries the group one step further, on the frame it started and on every frame after.
        /// <br/> Call <see cref="BoardCommand.Complete"/> once nothing is left to carry.
        /// </summary>
        /// <param name="deltaTime">The seconds that passed since the previous frame, 0 on the frame the group started.</param>
        protected virtual void Step(float deltaTime)
        {
        }

        protected override void OnCancel()
        {
            // Whatever is left of the group gives up with it. A command that already finished
            // is not touched by this.
            for (int index = 0; index < _commands.Length; index++)
            {
                BoardCommandRunner.Cancel(_commands[index]);
            }
        }

        protected override void OnDispose()
        {
            for (int index = 0; index < _commands.Length; index++)
            {
                IBoardCommand command = _commands[index];
                if (command == null)
                {
                    continue;
                }

                try
                {
                    command.Dispose();
                }
                catch (Exception exception)
                {
                    // The rest of the group still has to be released.
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }
    }
}
