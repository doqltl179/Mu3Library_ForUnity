using System;
using System.Collections;
using UnityEngine;

namespace Mu3Library.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public abstract class AudioController : MonoBehaviour
    {
        protected IAudioVolumeSettings _audioVolumeSettings;

        private AudioSource m_source;
        private AudioSource _source
        {
            get
            {
                if (m_source == null)
                {
                    m_source = GetComponent<AudioSource>();
                }

                return m_source;
            }
        }

        public Vector3 Position
        {
            get => _source.transform.position;
            set => _source.transform.position = value;
        }

        private float _clipVolume = 1.0f;
        public float ClipVolume => _clipVolume;

        protected abstract float _categoryVolume { get; }
        public float CategoryVolume => _categoryVolume;

        protected float _masterVolume => _audioVolumeSettings != null ? _audioVolumeSettings.MasterVolume : 1.0f;
        public float MasterVolume => _masterVolume;

        private float _fadeVolume = 1.0f;
        public float FadeVolume
        {
            get => _fadeVolume;
            set => _fadeVolume = value;
        }

        public bool IsPlaying => _source.isPlaying;

        private bool _isPaused = false;
        public bool IsPaused => _isPaused;

        private float _normalizedTime => _source.clip != null && _source.clip.length > 0f
            ? _source.time / _source.clip.length
            : 0f;
        public float NormalizedTime => _normalizedTime;

        private int _loopCount = -1;
        private float _loopInterval = 0f;

        private bool _isInLoopInterval = false;
        private bool _isCompleted = false;

        private IEnumerator _loopCoroutine = null;

        /// <summary>True while waiting between loop cycles.</summary>
        public bool IsInLoopInterval => _isInLoopInterval;

        /// <summary>
        /// True after all loop cycles have finished naturally,
        /// or after <see cref="Stop"/> has been called.
        /// </summary>
        public bool IsCompleted => _isCompleted;

        private IEnumerator _fadeCoroutine = null;

        /// <summary>
        /// Fired when the controller finishes all loop cycles naturally.
        /// Not fired when <see cref="Stop"/> is called explicitly.
        /// </summary>
        public event Action OnCompleted;


        protected virtual void OnDisable()
        {
            StopFade();
            StopLoop();
        }

        #region Utility

        internal void SetVolumeSettings(IAudioVolumeSettings audioVolumeSettings)
        {
            _audioVolumeSettings = audioVolumeSettings;
        }

        /// <summary>
        /// Sets loop count and interval. Call before <see cref="Play"/>.
        /// loopCount &lt;= 0 means infinite. loopCount == 1 means play once.
        /// </summary>
        internal void SetLoopSettings(int loopCount, float loopInterval)
        {
            _loopCount = loopCount;
            _loopInterval = Mathf.Max(0f, loopInterval);
        }

        public void FadeOut(float fadeTime = 1.0f, Action callback = null)
        {
            // If currently in a loop interval, there is no audio playing to fade —
            // skip the fade and execute the callback immediately.
            if (_isInLoopInterval)
            {
                callback?.Invoke();
                return;
            }

            StopFade();

            _fadeCoroutine = FadeOutCoroutine(fadeTime, callback);
            StartCoroutine(_fadeCoroutine);
        }

        public void FadeIn(float fadeTime = 1.0f, Action callback = null)
        {
            StopFade();

            _fadeCoroutine = FadeInCoroutine(fadeTime, callback);
            StartCoroutine(_fadeCoroutine);
        }

        public void SetClipVolume(float clipVolume)
        {
            _clipVolume = clipVolume;
            CalculateVolume();
        }

        public void RecalculateVolume()
        {
            CalculateVolume();
        }

        public void SetClip(AudioClip clip)
        {
            _source.clip = clip;
        }

        public void Play()
        {
            _isPaused = false;
            _isCompleted = false;

            StopLoop();

            _loopCoroutine = LoopCoroutine();
            StartCoroutine(_loopCoroutine);
        }

        public void Stop()
        {
            _isPaused = false;

            StopFade();
            StopLoop();

            _source.loop = false;
            _source.Stop();

            _isCompleted = true;
        }

        public void Pause()
        {
            _isPaused = true;
            _source.Pause();
        }

        public void UnPause()
        {
            _isPaused = false;
            _source.UnPause();
        }

        public void SetAudioParameters(AudioBaseSettings settings)
        {
            _source.priority = settings.Priority;
            _source.pitch = settings.Pitch;
            _source.panStereo = settings.StereoPan;
        }

        public void SetAudioParameters(Audio3dSoundSettings settings)
        {
            settings.WriteToTarget(_source);
        }

        public bool IsSameClip(AudioClip clip)
        {
            return _source == null || _source.clip == null ? false : _source.clip == clip;
        }

        public void SetActive(bool value)
        {
            _source.gameObject.SetActive(value);
        }

        #endregion

        private void StopFade()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
        }

        private void StopLoop()
        {
            if (_loopCoroutine != null)
            {
                StopCoroutine(_loopCoroutine);
                _loopCoroutine = null;
            }

            _isInLoopInterval = false;
        }

        /// <summary>
        /// Drives looping and interval logic for this controller.
        /// When infinite loop with no interval is requested, delegates to
        /// <c>AudioSource.loop</c> for seamless sample-accurate looping.
        /// </summary>
        private IEnumerator LoopCoroutine()
        {
            bool isInfinite = _loopCount <= 0;

            // Fast path: infinite loop with no interval.
            // AudioSource handles looping at the sample level — no audible gap.
            if (isInfinite && _loopInterval <= 0f)
            {
                _source.loop = true;
                _source.Play();
                yield break;
            }

            // General path: finite loop count or interval between cycles.
            _source.loop = false;
            int remaining = isInfinite ? -1 : _loopCount;

            while (remaining != 0)
            {
                _isInLoopInterval = false;
                _source.Play();

                // Wait until the clip finishes (respects pause).
                // NormalizedTime threshold avoids precision issues at the very end of a clip.
                yield return new WaitUntil(
                    () => !_isPaused && (_source.clip == null || _normalizedTime >= 0.97f));

                _source.Stop();

                if (remaining > 0) remaining--;
                if (remaining == 0) break;

                // Loop interval
                if (_loopInterval > 0f)
                {
                    _isInLoopInterval = true;

                    float elapsed = 0f;
                    while (elapsed < _loopInterval)
                    {
                        if (!_isPaused) elapsed += Time.deltaTime;
                        yield return null;
                    }

                    _isInLoopInterval = false;
                }
            }

            _isCompleted = true;
            _loopCoroutine = null;
            OnCompleted?.Invoke();
        }

        private IEnumerator FadeInCoroutine(float fadeTime = 1.0f, Action callback = null)
        {
            float timer = fadeTime * _fadeVolume;

            while (timer < fadeTime)
            {
                timer += Time.deltaTime;

                _fadeVolume = Mathf.Clamp01(timer / fadeTime);
                CalculateVolume();

                yield return null;
            }
            CalculateVolume();

            _fadeCoroutine = null;

            callback?.Invoke();
        }

        private IEnumerator FadeOutCoroutine(float fadeTime = 1.0f, Action callback = null)
        {
            float timer = fadeTime * (1.0f - _fadeVolume);

            while (timer < fadeTime)
            {
                timer += Time.deltaTime;

                _fadeVolume = Mathf.Clamp01(1.0f - (timer / fadeTime));
                CalculateVolume();

                yield return null;
            }
            CalculateVolume();

            _fadeCoroutine = null;

            callback?.Invoke();
        }

        private void CalculateVolume()
        {
            _source.volume = GetCalculatedVolume();
        }

        private float GetCalculatedVolume()
        {
            return _clipVolume * _categoryVolume * _masterVolume * _fadeVolume;
        }
    }
}
