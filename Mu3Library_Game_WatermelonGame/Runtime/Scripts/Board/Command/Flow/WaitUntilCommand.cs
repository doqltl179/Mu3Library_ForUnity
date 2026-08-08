using System;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Flow
{
    /// <summary>
    /// Waits until the board is in the state a project asks for, which is what a command that has
    /// <br/> to act on a settled board needs: <c>new WaitUntilCommand(() =&gt; context.Items.Count &lt; 10)</c>.
    /// <br/> A wait that runs out of time is canceled instead of completed, so the
    /// <see cref="SequenceCommand"/> around it gives up as well rather than acting on a board that
    /// <br/> never got there.
    /// </summary>
    public class WaitUntilCommand : BoardCommand
    {
        private readonly Func<bool> _predicate;

        protected readonly float _timeout;
        /// <summary>
        /// The seconds the command waits at most, zero to wait for as long as it takes.
        /// </summary>
        public float Timeout => _timeout;

        protected float _elapsed;
        public float Elapsed => _elapsed;

        protected bool _isTimedOut;
        /// <summary>
        /// True when the command was canceled because the state it waited for never came.
        /// </summary>
        public bool IsTimedOut => _isTimedOut;

        private readonly Action _onComplete;



        /// <param name="predicate">Checked every frame, the command completes as soon as it holds.</param>
        /// <param name="timeoutSeconds">The seconds to wait at most, zero or less to wait for as long as it takes.</param>
        /// <param name="onComplete">Called once the state was reached, never on a canceled command.</param>
        public WaitUntilCommand(Func<bool> predicate, float timeoutSeconds = 0.0f, Action onComplete = null)
        {
            _predicate = predicate;
            _timeout = Mathf.Max(0.0f, timeoutSeconds);
            _onComplete = onComplete;
        }

        protected override void OnRun()
        {
            if (_predicate == null)
            {
                Debug.LogWarning("A wait without a condition cannot be run.");
                Cancel();
                return;
            }

            // The board can already be in the state that was waited for.
            Check(0.0f);
        }

        protected override void OnUpdate(float deltaTime)
        {
            Check(deltaTime);
        }

        protected override void OnComplete()
        {
            _onComplete?.Invoke();
        }

        private void Check(float deltaTime)
        {
            _elapsed += Mathf.Max(0.0f, deltaTime);

            if (_predicate())
            {
                Complete();
                return;
            }

            if (_timeout <= 0.0f || _elapsed < _timeout)
            {
                return;
            }

            _isTimedOut = true;

            Cancel();
        }
    }
}
