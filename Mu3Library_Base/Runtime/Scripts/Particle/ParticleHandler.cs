using System;
using UnityEngine;

namespace Mu3Library.Particle
{
    /// <summary>
    /// Provides convenient control over the ParticleSystem on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleHandler : MonoBehaviour
    {
        private ParticleSystem m_particleSystem;
        private ParticleSystem _particleSystem => m_particleSystem ??= GetComponent<ParticleSystem>();

        private bool _completionPending;
        private bool _suppressCompletion;

        public ParticleSystem ParticleSystem => _particleSystem;
        public bool IsPlaying => _particleSystem.isPlaying;
        public bool IsPaused => _particleSystem.isPaused;
        public bool IsStopped => _particleSystem.isStopped;
        public bool IsAlive => _particleSystem.IsAlive(true);
        public bool IsLooping => _particleSystem.main.loop;

        public bool Loop
        {
            get => IsLooping;
            set => SetLoop(value);
        }

        public event Action OnPlayed;
        public event Action OnStopped;
        public event Action OnPaused;
        public event Action OnUnPaused;
        public event Action OnCleared;
        public event Action OnRestarted;
        public event Action<bool> OnLoopChanged;

        /// <summary>
        /// <br/> Fired when the particle system finishes naturally.
        /// <br/> It is not fired by <see cref="Stop"/> or <see cref="Clear"/>.
        /// <br/> Looping systems do not fire it while <see cref="IsLooping"/> is true.
        /// </summary>
        public event Action OnCompleted;



        private void Update()
        {
            bool isAlive = _particleSystem.IsAlive(true);

            // Direct ParticleSystem.Play() calls are also treated as a new playback cycle.
            if (_suppressCompletion && _particleSystem.isPlaying)
            {
                _suppressCompletion = false;
            }

            if (!_suppressCompletion &&
                !_completionPending &&
                (_particleSystem.isPlaying || isAlive))
            {
                _completionPending = true;
            }

            if (!_suppressCompletion &&
                _completionPending &&
                !isAlive &&
                _particleSystem.isStopped)
            {
                _completionPending = false;
                OnCompleted?.Invoke();
            }
        }

        public void Play(bool withChildren = true)
        {
            _suppressCompletion = false;
            _completionPending = true;
            _particleSystem.Play(withChildren);
            OnPlayed?.Invoke();
        }

        public void PlayLoop(bool withChildren = true)
        {
            SetLoop(true);
            Play(withChildren);
        }

        public void PlayOnce(bool withChildren = true)
        {
            SetLoop(false);
            Play(withChildren);
        }

        public void SetLoop(bool loop)
        {
            if (IsLooping == loop)
            {
                return;
            }

            ParticleSystem.MainModule main = _particleSystem.main;
            main.loop = loop;
            OnLoopChanged?.Invoke(loop);
        }

        public void Stop(bool clear = true)
        {
            _suppressCompletion = true;
            _completionPending = false;

            ParticleSystemStopBehavior stopBehavior = clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

            _particleSystem.Stop(true, stopBehavior);
            OnStopped?.Invoke();
        }

        public void Pause(bool withChildren = true)
        {
            _particleSystem.Pause(withChildren);
            OnPaused?.Invoke();
        }

        public void UnPause(bool withChildren = true)
        {
            _suppressCompletion = false;
            _completionPending = true;
            _particleSystem.Play(withChildren);
            OnUnPaused?.Invoke();
        }

        public void Clear()
        {
            _suppressCompletion = true;
            _completionPending = false;
            _particleSystem.Clear(true);
            OnCleared?.Invoke();
        }

        public void Restart(bool withChildren = true)
        {
            Stop();
            Play(withChildren);
            OnRestarted?.Invoke();
        }
    }
}
