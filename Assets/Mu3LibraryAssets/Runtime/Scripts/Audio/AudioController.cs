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

        public float NormalizedTime => _source.time / _source.clip.length;

        private IEnumerator _fadeCoroutine = null;



        protected virtual void OnDisable()
        {
            StopFade();
        }

        #region Utility
        internal void SetVolumeSettings(IAudioVolumeSettings audioVolumeSettings)
        {
            _audioVolumeSettings = audioVolumeSettings;
        }

        public void FadeOut(float fadeTime = 1.0f, Action callback = null)
        {
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

        public void Play() {
            _isPaused = false;
            _source.Play();
        }

        public void Stop()
        {
            _isPaused = false;
            _source.Stop();
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

        public void SetAudioParameters(AudioBaseParameters parameters)
        {
            _source.priority = parameters.Priority;
            _source.pitch = parameters.Pitch;
            _source.panStereo = parameters.StereoPan;
            _source.spatialBlend = parameters.SpatialBlend;
            _source.reverbZoneMix = parameters.ReverbZoneMix;
        }

        public void SetAudioParameters(Audio3dSoundSettings parameters)
        {
            _source.dopplerLevel = parameters.DopplerLevel;
            _source.spread = parameters.Spread;

            if (parameters.AudioSourceCurveType != null && parameters.AudioSourceCurve != null)
            {
                _source.SetCustomCurve(parameters.AudioSourceCurveType.Value, parameters.AudioSourceCurve);
            }
            else
            {
                if (parameters.AudioSourceCurve != null)
                {
                    _source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, parameters.AudioSourceCurve);
                }
                else
                {
                    _source.rolloffMode = AudioRolloffMode.Linear;
                }
            }

            _source.minDistance = parameters.MinDistance;
            _source.maxDistance = parameters.MaxDistance;
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
