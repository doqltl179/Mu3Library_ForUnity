using System;
using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board.Command;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    public partial class BoardController
    {
        protected readonly List<IBoardCommand> _commands = new();

        /// <summary>
        /// Commands handed to the board while the list was being advanced. A command that enqueues
        /// <br/> the next one is the normal case, and the list must not grow while it is walked.
        /// </summary>
        private readonly List<IBoardCommand> _pendingCommands = new();

        /// <summary>
        /// Commands that are to be dropped whether or not they report themselves finished, which
        /// <br/> is how a command that cannot be canceled still leaves the board.
        /// </summary>
        private readonly List<IBoardCommand> _removingCommands = new();

        /// <summary>
        /// Reused by <see cref="CancelCommands{T}"/>, so cancelling can change the list it selected from.
        /// </summary>
        private readonly List<IBoardCommand> _commandSelection = new();

        private bool _isUpdatingCommands;

        /// <summary>
        /// The commands the board is running, in the order they are advanced.
        /// <br/> A command enqueued during the current frame joins them when the board is done
        /// <br/> advancing the ones it already had.
        /// </summary>
        public IReadOnlyList<IBoardCommand> Commands => _commands;

        /// <summary>
        /// How many commands the board holds, the ones that are still waiting to join included.
        /// </summary>
        public int CommandCount => _commands.Count + _pendingCommands.Count;

        /// <summary>
        /// Raised with the command the board accepted.
        /// </summary>
        public event Action<IBoardCommand> OnCommandEnqueued;

        /// <summary>
        /// Raised with a command the board is done with, right before it is disposed.
        /// <br/> Read <see cref="ICancelableBoardCommand.IsCanceled"/> to tell a command that
        /// <br/> reached its end from one that gave up.
        /// </summary>
        public event Action<IBoardCommand> OnCommandFinished;

        /// <summary>
        /// Raised with the command that threw and the exception it threw. The board logs it,
        /// <br/> cancels the command and carries on with the rest.
        /// </summary>
        public event Action<IBoardCommand, Exception> OnCommandFailed;

        #region Utility
        /// <summary>
        /// Hands a command to the board, which runs it from the next frame on and disposes it once
        /// <br/> it is finished.
        /// <br/> Commands are only advanced while the board is running, so one given to a prepared
        /// <br/> board waits for <see cref="GameStart"/>, and <see cref="Prepare"/> drops whatever
        /// <br/> is left.
        /// </summary>
        /// <returns>False when the command is missing, already finished, or already enqueued.</returns>
        public virtual bool EnqueueCommand(IBoardCommand command)
        {
            if (command == null)
            {
                Debug.LogWarning("A missing command cannot be enqueued.", this);
                return false;
            }

            if (command.IsCompleted)
            {
                Debug.LogWarning("A finished command cannot be enqueued.", this);
                return false;
            }

            if (ContainsCommand(command))
            {
                return false;
            }

            if (_isUpdatingCommands)
            {
                _pendingCommands.Add(command);
            }
            else
            {
                _commands.Add(command);
            }

            OnCommandEnqueued?.Invoke(command);

            return true;
        }

        /// <summary>
        /// Stops a command the board is holding and drops it.
        /// </summary>
        /// <returns>False when the board is not holding the command.</returns>
        public bool CancelCommand(IBoardCommand command)
        {
            if (command == null || !ContainsCommand(command))
            {
                return false;
            }

            BoardCommandRunner.Cancel(command);
            RequestCommandRemoval(command);

            if (!_isUpdatingCommands)
            {
                ProcessCommandChanges();
            }

            return true;
        }

        /// <summary>
        /// Stops every command of one kind, which is how a project ends its own board effect
        /// <br/> without holding on to the instance it enqueued.
        /// </summary>
        /// <returns>How many commands were stopped.</returns>
        public int CancelCommands<T>() where T : IBoardCommand
        {
            _commandSelection.Clear();

            SelectCommands<T>(_commands);
            SelectCommands<T>(_pendingCommands);

            for (int index = 0; index < _commandSelection.Count; index++)
            {
                IBoardCommand command = _commandSelection[index];

                BoardCommandRunner.Cancel(command);
                RequestCommandRemoval(command);
            }

            int canceledCount = _commandSelection.Count;
            _commandSelection.Clear();

            if (!_isUpdatingCommands)
            {
                ProcessCommandChanges();
            }

            return canceledCount;
        }

        /// <summary>
        /// Stops every command the board holds.
        /// </summary>
        public void CancelAllCommands()
        {
            CancelCommands<IBoardCommand>();
        }

        /// <summary>
        /// True while the board holds a command of one kind, so a project can keep a power-up from
        /// <br/> running twice at once.
        /// </summary>
        public bool HasCommand<T>() where T : IBoardCommand
            => ContainsCommandOfType<T>(_commands) || ContainsCommandOfType<T>(_pendingCommands);
        #endregion

        protected void UpdateCommands()
        {
            _isUpdatingCommands = true;

            try
            {
                // The commands enqueued since the previous frame join in, and the ones that
                // finished then leave, before anything is advanced.
                ProcessCommandChanges();

                float deltaTime = Time.deltaTime;

                for (int index = 0; index < _commands.Count; index++)
                {
                    IBoardCommand command = _commands[index];
                    if (BoardCommandRunner.IsFinished(command))
                    {
                        continue;
                    }

                    try
                    {
                        BoardCommandRunner.Advance(command, deltaTime);
                    }
                    catch (Exception exception)
                    {
                        // One broken command must not stop the rest of the board work.
                        Debug.LogException(exception, this);

                        BoardCommandRunner.Cancel(command);
                        RequestCommandRemoval(command);

                        OnCommandFailed?.Invoke(command, exception);
                    }
                }

                ProcessCommandChanges();
            }
            finally
            {
                _isUpdatingCommands = false;
            }
        }

        /// <summary>
        /// Drops every command without reporting it, for the moments the board is torn down or
        /// <br/> prepared again.
        /// </summary>
        protected void ClearAllCommands()
        {
            _removingCommands.Clear();

            // A command that is still waiting to join holds what it reserved just like a running
            // one, so it is disposed here as well instead of only being dropped.
            DisposeCommands(_pendingCommands);
            DisposeCommands(_commands);
        }

        private static void DisposeCommands(List<IBoardCommand> commands)
        {
            // Disposing a command runs what it registered for the moment it gives up, and that
            // can reach the board again, so the list is drained instead of walked.
            while (commands.Count > 0)
            {
                int lastIndex = commands.Count - 1;
                IBoardCommand command = commands[lastIndex];

                commands.RemoveAt(lastIndex);

                command?.Dispose();
            }
        }

        /// <summary>
        /// Lets the finished commands go and takes the waiting ones in.
        /// </summary>
        private void ProcessCommandChanges()
        {
            for (int index = 0; index < _commands.Count; index++)
            {
                IBoardCommand command = _commands[index];

                // The request is taken even from a command that reported itself finished,
                // otherwise it would be left behind holding on to a disposed command.
                bool isRemovalRequested = TakeCommandRemovalRequest(command);

                if (command != null && !command.IsCompleted && !isRemovalRequested)
                {
                    continue;
                }

                _commands.RemoveAt(index);
                index--;

                if (command == null)
                {
                    continue;
                }

                OnCommandFinished?.Invoke(command);

                command.Dispose();
            }

            if (_pendingCommands.Count == 0)
            {
                return;
            }

            // The event above can enqueue, and it lands in the pending list while the board is
            // updating, so the list is only cleared after everything in it has been taken over.
            _commands.AddRange(_pendingCommands);
            _pendingCommands.Clear();
        }

        private bool ContainsCommand(IBoardCommand command)
            => _commands.Contains(command) || _pendingCommands.Contains(command);

        private void RequestCommandRemoval(IBoardCommand command)
        {
            if (command == null || _removingCommands.Contains(command))
            {
                return;
            }

            _removingCommands.Add(command);
        }

        private bool TakeCommandRemovalRequest(IBoardCommand command)
            => _removingCommands.Remove(command);

        private void SelectCommands<T>(List<IBoardCommand> commands) where T : IBoardCommand
        {
            for (int index = 0; index < commands.Count; index++)
            {
                IBoardCommand command = commands[index];
                if (command is not T || command.IsCompleted)
                {
                    continue;
                }

                _commandSelection.Add(command);
            }
        }

        private static bool ContainsCommandOfType<T>(List<IBoardCommand> commands) where T : IBoardCommand
        {
            for (int index = 0; index < commands.Count; index++)
            {
                IBoardCommand command = commands[index];
                if (command is T && !command.IsCompleted)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
