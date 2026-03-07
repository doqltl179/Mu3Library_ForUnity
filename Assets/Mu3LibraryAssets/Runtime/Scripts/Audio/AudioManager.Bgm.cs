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



        public void ResetBgmVolume()
        {
            SetBgmVolume(DefaultBgmVolume);
        }

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

        public void TransitionBgm(AudioClip clip) => TransitionBgm(clip, 1.0f, AudioSourceSettings.BgmStandard);

        public void TransitionBgm(AudioClip clip, float transitionTime) => TransitionBgm(clip, transitionTime, AudioSourceSettings.BgmStandard);

        public void TransitionBgm(AudioClip clip, float transitionTime, AudioSourceSettings settings)
        {
            if (clip == null)
            {
                Debug.LogError($"BGM clip is NULL.");
                return;
            }

            AudioController from = _bgmMainController;
            if (from != null)
            {
                from.FadeOut(transitionTime, from.Stop);
            }

            AudioController to = _bgmSubController;
            if (to == null)
            {
                AudioSource source = CreateBgmSource();
                to = CreateAudioController<BgmController>(source, clip, settings);
            }

            if (!to.IsPlaying || !to.IsSameClip(clip))
            {
                InitializeAudioController(to, clip, settings);
                to.FadeVolume = 0.0f;
                to.RecalculateVolume();
                to.Play();
            }

            to.FadeIn(transitionTime);

            _bgmMainController = to;
            _bgmSubController = from;
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
                AudioSource source = CreateBgmSource();
                _bgmMainController = CreateAudioController<BgmController>(source, clip, settings);
            }

            _bgmMainController.FadeVolume = 1.0f;
            _bgmMainController.RecalculateVolume();
            _bgmMainController.Play();
        }

        public void StopBgm()
        {
            if (_bgmMainController != null)
            {
                _bgmMainController.Stop();
            }
            if (_bgmSubController != null)
            {
                _bgmSubController.Stop();
            }
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

        private AudioSource CreateBgmSource()
        {
            GameObject instance = new GameObject("BgmSource");
            instance.transform.SetParent(_rootTransform);

            AudioSource source = instance.AddComponent<AudioSource>();
            source.playOnAwake = false;

            return source;
        }
    }
}
