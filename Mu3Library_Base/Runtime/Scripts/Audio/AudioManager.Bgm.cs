using System;
using UnityEngine;

namespace Mu3Library.Audio
{
    public partial class AudioManager
    {
        private AudioController _bgmMainController = null;
        private AudioController _bgmSubController = null;

        private const float DefaultBgmVolume = 0.8f;

        private float _bgmVolume = DefaultBgmVolume;
        public float BgmVolume
        {
            get => _bgmVolume;
            set => SetBgmVolume(value);
        }

        private float _calculatedBgmVolume = DefaultMasterVolume * DefaultBgmVolume;
        public float CalculatedBgmVolume => _calculatedBgmVolume;



        public void FadeInBgm(float fadeTime = 1.0f)
        {
            if (_bgmMainController == null)
            {
                return;
            }
            else if (_bgmMainController.IsPaused)
            {
                _bgmMainController.UnPause();
            }
            else if (!_bgmMainController.IsPlaying && !_bgmMainController.IsInLoopInterval)
            {
                _bgmMainController.Play();
            }

            _bgmMainController.FadeIn(fadeTime);
        }

        public void FadeOutBgm(float fadeTime = 1.0f)
        {
            // IsCompleted covers: finished naturally or Stop() was called.
            // FadeOut() already handles IsInLoopInterval by calling the callback immediately.
            if (_bgmMainController == null || _bgmMainController.IsCompleted)
            {
                return;
            }

            _bgmMainController.FadeOut(fadeTime, _bgmMainController.Pause);
        }

        public void PauseBgm()
        {
            if (_bgmMainController != null)
            {
                _bgmMainController.Pause();
            }
            if (_bgmSubController != null)
            {
                _bgmSubController.Pause();
            }
        }

        public void PlayBgm(AudioClip clip) => PlayBgm(clip, AudioSourceSettings.BgmStandard);

        public void PlayBgm(AudioClip clip, AudioSourceSettings settings)
        {
            if (clip == null)
            {
                Debug.LogError($"BGM clip is NULL.");
                return;
            }

            if (_bgmMainController != null)
            {
                if (_bgmMainController.IsPlaying && _bgmMainController.IsSameClip(clip))
                {
                    Debug.LogWarning($"Requested clip is same with current clip. clip: {clip.name}");
                    return;
                }

                InitializeAudioController(_bgmMainController, clip, settings);
            }
            else
            {
                AudioSource source = CreateAudioSource("BgmSource");
                _bgmMainController = CreateAudioController<BgmController>(source, clip, settings);
            }

            _bgmMainController.SetMixerGroup(_bgmMixerGroup);
            _bgmMainController.FadeVolume = 1.0f;
            _bgmMainController.RecalculateVolume();
            _bgmMainController.Play();
        }

        /// <summary>
        /// Dips the BGM to <paramref name="duckVolume"/>, holds it, and brings it back, which is
        /// what lets a voice line or a heavy SFX sit on top of the music for a moment.
        /// </summary>
        public void DuckBgm(float duckVolume = 0.3f, float fadeOutTime = 0.2f, float holdTime = 0.5f, float fadeInTime = 0.5f)
        {
            if (_bgmMainController == null || _bgmMainController.IsCompleted)
            {
                return;
            }

            _bgmMainController.Duck(duckVolume, fadeOutTime, holdTime, fadeInTime);
        }

        public void PlayBgmForce(AudioClip clip) => PlayBgmForce(clip, AudioSourceSettings.BgmStandard);

        public void PlayBgmForce(AudioClip clip, AudioSourceSettings settings)
        {
            if (_bgmMainController != null && _bgmMainController.IsPlaying)
            {
                _bgmMainController.Stop();
            }

            PlayBgm(clip, settings);
        }

        public void PlayBgmForceWithKey(string key) => PlayBgmForceWithKey(key, AudioSourceSettings.BgmStandard);

        public void PlayBgmForceWithKey(string key, AudioSourceSettings settings)
        {
            if (TryGetCachedAudioResource(key, out AudioClip clip))
            {
                PlayBgmForce(clip, settings);
            }
        }

        public void PlayBgmWithKey(string key) => PlayBgmWithKey(key, AudioSourceSettings.BgmStandard);

        public void PlayBgmWithKey(string key, AudioSourceSettings settings)
        {
            if (TryGetCachedAudioResource(key, out AudioClip clip))
            {
                PlayBgm(clip, settings);
            }
        }

        public void ResetBgmVolume()
        {
            SetBgmVolume(DefaultBgmVolume);
        }

        public void StopBgm()
        {
            CancelPlaylist();

            if (_bgmMainController != null)
            {
                _bgmMainController.Stop();
            }
            if (_bgmSubController != null)
            {
                _bgmSubController.Stop();
            }
        }

        public void TransitionBgm(AudioClip clip) => TransitionBgm(clip, 1.0f, AudioSourceSettings.BgmStandard);

        public void TransitionBgm(AudioClip clip, float transitionTime) => TransitionBgm(clip, transitionTime, AudioSourceSettings.BgmStandard);

        public void TransitionBgm(AudioClip clip, float transitionTime, AudioSourceSettings settings)
            => TransitionBgmInternal(clip, transitionTime, settings, null);

        /// <summary>
        /// The transition body the callback and the UniTask paths share. Returns the controller
        /// the new track plays on, null when the clip is missing, and reports the end of its
        /// fade-in, which is what ends the transition.
        /// </summary>
        internal AudioController TransitionBgmInternal(AudioClip clip, float transitionTime, AudioSourceSettings settings, Action onFadedIn)
        {
            if (clip == null)
            {
                Debug.LogError($"BGM clip is NULL.");
                return null;
            }

            AudioController from = _bgmMainController;
            if (from != null)
            {
                from.FadeOut(transitionTime, from.Stop);
            }

            AudioController to = _bgmSubController;
            if (to == null)
            {
                AudioSource source = CreateAudioSource("BgmSource");
                to = CreateAudioController<BgmController>(source, clip, settings);
            }

            to.SetMixerGroup(_bgmMixerGroup);

            if (!to.IsPlaying || !to.IsSameClip(clip))
            {
                InitializeAudioController(to, clip, settings);
                to.FadeVolume = 0.0f;
                to.RecalculateVolume();
                to.Play();
            }

            to.FadeIn(transitionTime, onFadedIn);

            _bgmMainController = to;
            _bgmSubController = from;

            return to;
        }

        public void TransitionBgmWithKey(string key) => TransitionBgmWithKey(key, 1.0f, AudioSourceSettings.BgmStandard);

        public void TransitionBgmWithKey(string key, float transitionTime) => TransitionBgmWithKey(key, transitionTime, AudioSourceSettings.BgmStandard);

        public void TransitionBgmWithKey(string key, float transitionTime, AudioSourceSettings settings)
        {
            if (TryGetCachedAudioResource(key, out AudioClip clip))
            {
                TransitionBgm(clip, transitionTime, settings);
            }
        }

        public void UnPauseBgm()
        {
            if (_bgmMainController != null)
            {
                _bgmMainController.UnPause();
            }
            if (_bgmSubController != null)
            {
                _bgmSubController.UnPause();
            }
        }

        private void SetBgmVolume(float value)
        {
            value = Mathf.Clamp01(value);
            if (_bgmVolume == value)
            {
                return;
            }

            _bgmVolume = value;
            _calculatedBgmVolume = _masterVolume * value;

            if (_bgmMainController != null)
            {
                _bgmMainController.RecalculateVolume();
            }
            if (_bgmSubController != null)
            {
                _bgmSubController.RecalculateVolume();
            }

            OnBgmVolumeChanged?.Invoke(_bgmVolume);
        }

    }
}
