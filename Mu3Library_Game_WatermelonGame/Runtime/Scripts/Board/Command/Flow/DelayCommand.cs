using System;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Flow
{
    /// <summary>
    /// Waits for a while and then reports it.
    /// <br/> On its own it holds a <see cref="SequenceCommand"/> back between two steps, which is
    /// <br/> what gives a chain of board work its pacing.
    /// <br/> It counts the frame time the board is advanced with, so it follows
    /// <see cref="Time.timeScale"/> the way the falling items do.
    /// </summary>
    public class DelayCommand : BoardCommand
    {
        protected readonly float _duration;
        /// <summary>
        /// The seconds the command waits.
        /// </summary>
        public float Duration => _duration;

        protected float _elapsed;
        /// <summary>
        /// The seconds it has waited so far.
        /// </summary>
        public float Elapsed => _elapsed;

        /// <summary>
        /// How far the wait has come, from 0 to 1.
        /// </summary>
        public float Progress => _duration > 0.0f ? Mathf.Clamp01(_elapsed / _duration) : 1.0f;

        private readonly Action _onComplete;



        /// <param name="seconds">The seconds to wait. Zero finishes the command on the frame it started.</param>
        /// <param name="onComplete">Called once the wait is over, never on a canceled command.</param>
        public DelayCommand(float seconds, Action onComplete = null)
        {
            _duration = Mathf.Max(0.0f, seconds);
            _onComplete = onComplete;
        }

        protected override void OnRun()
        {
            if (_duration <= 0.0f)
            {
                Complete();
            }
        }

        protected override void OnUpdate(float deltaTime)
        {
            _elapsed += Mathf.Max(0.0f, deltaTime);

            if (_elapsed >= _duration)
            {
                Complete();
            }
        }

        protected override void OnComplete()
        {
            _onComplete?.Invoke();
        }
    }
}
