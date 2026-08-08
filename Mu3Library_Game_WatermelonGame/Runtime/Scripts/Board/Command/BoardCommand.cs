namespace Mu3Library.Game.WatermelonGame.Board.Command
{
    /// <summary>
    /// The base every command in this package is built on.
    /// <br/> It owns the state the board reads and turns it into the hooks below, so a command only
    /// <br/> describes the work it does:
    /// <br/> <see cref="OnRun"/> starts it, <see cref="OnUpdate"/> carries it over several frames,
    /// <see cref="OnComplete"/> and <see cref="OnCancel"/> close it, and <see cref="OnDispose"/>
    /// <br/> hands back whatever it borrowed.
    /// <br/> The work ends when the command calls <see cref="Complete"/> or <see cref="Cancel"/>;
    /// <br/> until then the board keeps updating it.
    /// <br/> Implement <see cref="IBoardCommand"/> directly instead when a command has to own its
    /// <br/> own state machine.
    /// </summary>
    public abstract class BoardCommand : IUpdatableBoardCommand, ICancelableBoardCommand
    {
        protected bool _isRunning;
        public bool IsRunning => _isRunning;

        protected bool _isCompleted;
        public bool IsCompleted => _isCompleted;

        protected bool _isCanceled;
        public bool IsCanceled => _isCanceled;

        protected bool _isDisposed;
        public bool IsDisposed => _isDisposed;

        /// <summary>
        /// Where the command stands, read from the flags above.
        /// </summary>
        public BoardCommandState State
        {
            get
            {
                if (_isCanceled)
                {
                    return BoardCommandState.Canceled;
                }

                if (_isCompleted)
                {
                    return BoardCommandState.Completed;
                }

                return _isRunning ? BoardCommandState.Running : BoardCommandState.Pending;
            }
        }



        public void Run()
        {
            if (_isDisposed || _isRunning || _isCompleted)
            {
                return;
            }

            _isRunning = true;

            try
            {
                OnRun();
            }
            catch
            {
                // The command never got going, so it is closed here instead of being left
                // running forever on the board.
                Cancel();
                throw;
            }
        }

        public void Update(float deltaTime)
        {
            if (_isDisposed || _isCompleted || !_isRunning)
            {
                return;
            }

            try
            {
                OnUpdate(deltaTime);
            }
            catch
            {
                Cancel();
                throw;
            }
        }

        /// <summary>
        /// Stops the command before it reached its end. It does nothing on a finished command.
        /// </summary>
        public void Cancel()
        {
            if (_isDisposed || _isCompleted)
            {
                return;
            }

            _isRunning = false;
            _isCanceled = true;
            _isCompleted = true;

            OnCancel();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            // A command disposed while it still had work left is canceled first, so it releases
            // what it holds through the same path as any other cancellation.
            if (!_isCompleted)
            {
                _isRunning = false;
                _isCanceled = true;
                _isCompleted = true;

                OnCancel();
            }

            _isDisposed = true;

            OnDispose();
        }

        /// <summary>
        /// Closes the command after it reached its end. It does nothing on a finished command.
        /// </summary>
        protected void Complete()
        {
            if (_isDisposed || _isCompleted)
            {
                return;
            }

            _isRunning = false;
            _isCompleted = true;

            OnComplete();
        }

        /// <summary>
        /// Starts the work. Call <see cref="Complete"/> here for a command that is done within
        /// <br/> the frame it started on.
        /// </summary>
        protected abstract void OnRun();

        /// <summary>
        /// Carries the work one frame further, called every frame while the command runs.
        /// </summary>
        /// <param name="deltaTime">The seconds that passed since the previous frame.</param>
        protected virtual void OnUpdate(float deltaTime)
        {
        }

        /// <summary>
        /// The work reached its end. This is where a completion callback belongs.
        /// </summary>
        protected virtual void OnComplete()
        {
        }

        /// <summary>
        /// The work was given up, either through <see cref="Cancel"/> or because the command was
        /// <br/> disposed with work left. This is where what the command reserved is released.
        /// </summary>
        protected virtual void OnCancel()
        {
        }

        /// <summary>
        /// The command is dropped for good. It runs after <see cref="OnComplete"/> or
        /// <see cref="OnCancel"/> and is the last thing the command does.
        /// </summary>
        protected virtual void OnDispose()
        {
        }
    }
}
