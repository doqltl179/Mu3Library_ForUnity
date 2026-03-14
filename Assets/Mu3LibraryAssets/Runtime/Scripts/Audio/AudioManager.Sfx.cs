using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Audio
{
    public partial class AudioManager
    {
        private readonly List<AudioController> _sfxControllers = new();
        private readonly Queue<AudioController> _sfxPool = new();
        private readonly List<AudioController> _sfxFadingOutControllers = new();

        private int _sfxSourceCountMax = 5;
        /// <summary>
        /// Maximum number of concurrent SFX instances.
        /// Min: 1, Max: 10
        /// </summary>
        public int SfxInstanceCountMax
        {
            get => _sfxSourceCountMax;
            set => _sfxSourceCountMax = Mathf.Min(Mathf.Max(value, 1), 10);
        }

        private const float DefaultSfxVolume = 1.0f;

        private float _sfxVolume = DefaultSfxVolume;
        public float SfxVolume
        {
            get => _sfxVolume;
            set => SetSfxVolume(value);
        }

        private float _calculatedSfxVolume = DefaultMasterVolume * DefaultSfxVolume;
        public float CalculatedSfxVolume => _calculatedSfxVolume;



        public void ResetSfxVolume()
        {
            SetSfxVolume(DefaultSfxVolume);
        }

        public void PlaySfx(AudioClip clip) => PlaySfx(clip, AudioSourceSettings.SfxStandard, Vector3.zero);

        public void PlaySfx(AudioClip clip, AudioSourceSettings settings) => PlaySfx(clip, settings, Vector3.zero);

        public void PlaySfx(AudioClip clip, Vector3 position) => PlaySfx(clip, AudioSourceSettings.SfxStandard, position);

        public void PlaySfx(AudioClip clip, AudioSourceSettings settings, Vector3 position)
        {
            if (clip == null)
            {
                Debug.LogError($"SFX clip is NULL.");
                return;
            }

            CleanupSfxControllers();

            AudioController controller = null;

            if (_sfxControllers.Count < _sfxSourceCountMax)
            {
                if (_sfxPool.TryDequeue(out controller))
                {
                    InitializeAudioController(controller, clip, settings);
                }
                else
                {
                    AudioSource source = CreateSfxSource();
                    controller = CreateAudioController<SfxController>(source, clip, settings);
                }
            }
            else
            {
                controller = _sfxControllers[0];
                _sfxControllers.RemoveAt(0);

                InitializeAudioController(controller, clip, settings);
            }

            controller.SetActive(true);
            controller.Position = position;

            controller.Play();

            _sfxControllers.Add(controller);
        }

        public void StopFirstSfx(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            for (int i = 0; i < _sfxControllers.Count; i++)
            {
                AudioController controller = _sfxControllers[i];

                if (controller == null)
                {
                    _sfxControllers.RemoveAt(i);
                    i--;
                    continue;
                }

                if ((controller.IsPlaying || controller.IsInLoopInterval) && controller.IsSameClip(clip))
                {
                    PoolController(_sfxPool, controller);

                    _sfxControllers.RemoveAt(i);
                    break;
                }
            }
        }

        public void StopSfxAll()
        {
            PoolSfxAll();
        }

        public void PauseSfxAll()
        {
            foreach (AudioController controller in _sfxControllers)
            {
                if (controller == null)
                {
                    continue;
                }

                controller.Pause();
            }
        }

        public void UnPauseSfxAll()
        {
            foreach (AudioController controller in _sfxControllers)
            {
                if (controller == null)
                {
                    continue;
                }

                controller.UnPause();
            }
        }

        /// <summary>
        /// Plays the specified SFX clip with a fade in effect.
        /// Acts like PlaySfx but starts at volume 0 and fades in over fadeTime seconds.
        /// </summary>
        public void FadeInSfx(AudioClip clip, float fadeTime = 1.0f)
        {
            if (clip == null)
            {
                Debug.LogError($"SFX clip is NULL.");
                return;
            }

            CleanupSfxControllers();

            AudioController controller = null;

            if (_sfxControllers.Count < _sfxSourceCountMax)
            {
                if (_sfxPool.TryDequeue(out controller))
                {
                    InitializeAudioController(controller, clip, AudioSourceSettings.SfxStandard);
                }
                else
                {
                    AudioSource source = CreateSfxSource();
                    controller = CreateAudioController<SfxController>(source, clip, AudioSourceSettings.SfxStandard);
                }
            }
            else
            {
                controller = _sfxControllers[0];
                _sfxControllers.RemoveAt(0);

                InitializeAudioController(controller, clip, AudioSourceSettings.SfxStandard);
            }

            controller.SetActive(true);
            controller.Position = Vector3.zero;
            controller.FadeVolume = 0.0f;
            controller.RecalculateVolume();
            controller.Play();
            controller.FadeIn(fadeTime);

            _sfxControllers.Add(controller);
        }

        /// <summary>
        /// Fades in all active SFX controllers.
        /// </summary>
        public void FadeInSfxAll(float fadeTime = 1.0f)
        {
            foreach (AudioController controller in _sfxControllers)
            {
                if (controller == null)
                {
                    continue;
                }

                controller.FadeIn(fadeTime);
            }
        }

        /// <summary>
        /// Fades out the first active SFX controller playing the specified clip, then pools it.
        /// If the controller is in a loop interval, it is pooled immediately without fading.
        /// </summary>
        public void FadeOutFirstSfx(AudioClip clip, float fadeTime = 1.0f)
        {
            if (clip == null)
            {
                return;
            }

            for (int i = 0; i < _sfxControllers.Count; i++)
            {
                AudioController controller = _sfxControllers[i];

                if (controller == null)
                {
                    _sfxControllers.RemoveAt(i);
                    i--;
                    continue;
                }

                if ((controller.IsPlaying || controller.IsInLoopInterval) && controller.IsSameClip(clip))
                {
                    _sfxControllers.RemoveAt(i);
                    _sfxFadingOutControllers.Add(controller);
                    controller.FadeOut(fadeTime, () =>
                    {
                        _sfxFadingOutControllers.Remove(controller);
                        PoolController(_sfxPool, controller);
                    });
                    break;
                }
            }
        }

        /// <summary>
        /// Fades out all active SFX controllers, then pools each one.
        /// </summary>
        public void FadeOutSfxAll(float fadeTime = 1.0f)
        {
            if (_sfxControllers.Count == 0)
            {
                return;
            }

            // Snapshot and clear the tracking list so Update() does not interfere during fade.
            AudioController[] snapshot = _sfxControllers.ToArray();
            _sfxControllers.Clear();

            foreach (AudioController controller in snapshot)
            {
                if (controller == null)
                {
                    continue;
                }

                _sfxFadingOutControllers.Add(controller);
                controller.FadeOut(fadeTime, () =>
                {
                    _sfxFadingOutControllers.Remove(controller);
                    PoolController(_sfxPool, controller);
                });
            }
        }

        public void PlaySfxWithKey(string key) => PlaySfxWithKey(key, AudioSourceSettings.SfxStandard, Vector3.zero);

        public void PlaySfxWithKey(string key, AudioSourceSettings settings) => PlaySfxWithKey(key, settings, Vector3.zero);

        public void PlaySfxWithKey(string key, Vector3 position) => PlaySfxWithKey(key, AudioSourceSettings.SfxStandard, position);

        public void PlaySfxWithKey(string key, AudioSourceSettings settings, Vector3 position)
        {
            if (TryGetCachedAudioResource(key, out AudioClip clip))
            {
                PlaySfx(clip, settings, position);
            }
        }

        public void StopFirstSfxWithKey(string key)
        {
            if (TryGetCachedAudioResource(key, out AudioClip clip))
            {
                StopFirstSfx(clip);
            }
        }

        public void FadeInSfxWithKey(string key) => FadeInSfxWithKey(key, 1.0f);

        public void FadeInSfxWithKey(string key, float fadeTime)
        {
            if (TryGetCachedAudioResource(key, out AudioClip clip))
            {
                FadeInSfx(clip, fadeTime);
            }
        }

        public void FadeOutFirstSfxWithKey(string key) => FadeOutFirstSfxWithKey(key, 1.0f);

        public void FadeOutFirstSfxWithKey(string key, float fadeTime)
        {
            if (TryGetCachedAudioResource(key, out AudioClip clip))
            {
                FadeOutFirstSfx(clip, fadeTime);
            }
        }

        private void SetSfxVolume(float value)
        {
            if (_sfxVolume == value)
            {
                return;
            }

            _sfxVolume = value;
            _calculatedSfxVolume = _masterVolume * value;

            foreach (AudioController controller in _sfxControllers)
            {
                controller.RecalculateVolume();
            }

            OnSfxVolumeChanged?.Invoke(_sfxVolume);
        }

        private AudioSource CreateSfxSource()
        {
            GameObject instance = new GameObject("SfxSource");
            instance.transform.SetParent(_rootTransform);

            AudioSource source = instance.AddComponent<AudioSource>();
            source.playOnAwake = false;

            return source;
        }

        private void PoolSfxAll()
        {
            foreach (AudioController controller in _sfxControllers)
            {
                PoolController(_sfxPool, controller);
            }
            _sfxControllers.Clear();

            foreach (AudioController controller in _sfxFadingOutControllers)
            {
                PoolController(_sfxPool, controller);
            }
            _sfxFadingOutControllers.Clear();
        }

        private void CleanupSfxControllers()
        {
            for (int i = 0; i < _sfxControllers.Count; i++)
            {
                if (_sfxControllers[i] != null)
                {
                    continue;
                }

                _sfxControllers.RemoveAt(i);
                i--;
            }
        }
    }
}
