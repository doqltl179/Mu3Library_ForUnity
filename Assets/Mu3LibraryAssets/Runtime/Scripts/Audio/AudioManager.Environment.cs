using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Audio
{
    public partial class AudioManager
    {
        private readonly List<AudioController> _environmentControllers = new();
        private readonly Queue<AudioController> _environmentPool = new();
        private readonly List<AudioController> _environmentFadingOutControllers = new();

        private int _environmentSourceCountMax = 3;
        /// <summary>
        /// Maximum number of concurrent Environment instances.
        /// Min: 1, Max: 5
        /// </summary>
        public int EnvironmentInstanceCountMax
        {
            get => _environmentSourceCountMax;
            set => _environmentSourceCountMax = Mathf.Min(Mathf.Max(value, 1), 5);
        }

        private const float DefaultEnvironmentVolume = 0.8f;

        private float _environmentVolume = DefaultEnvironmentVolume;
        public float EnvironmentVolume
        {
            get => _environmentVolume;
            set => SetEnvironmentVolume(value);
        }

        private float _calculatedEnvironmentVolume = DefaultMasterVolume * DefaultEnvironmentVolume;
        public float CalculatedEnvironmentVolume => _calculatedEnvironmentVolume;



        public void ResetEnvironmentVolume()
        {
            SetEnvironmentVolume(DefaultEnvironmentVolume);
        }

        public void PlayEnvironment(AudioClip clip) => PlayEnvironment(clip, AudioSourceSettings.EnvironmentStandard, Vector3.zero);

        public void PlayEnvironment(AudioClip clip, AudioSourceSettings settings) => PlayEnvironment(clip, settings, Vector3.zero);

        public void PlayEnvironment(AudioClip clip, Vector3 position) => PlayEnvironment(clip, AudioSourceSettings.EnvironmentStandard, position);

        public void PlayEnvironment(AudioClip clip, AudioSourceSettings settings, Vector3 position)
        {
            if (clip == null)
            {
                Debug.LogError($"Environment clip is NULL.");
                return;
            }

            CleanupEnvironmentControllers();

            AudioController controller = null;

            if (_environmentControllers.Count < _environmentSourceCountMax)
            {
                if (_environmentPool.TryDequeue(out controller))
                {
                    InitializeAudioController(controller, clip, settings);
                }
                else
                {
                    AudioSource source = CreateEnvironmentSource();
                    controller = CreateAudioController<EnvironmentController>(source, clip, settings);
                }
            }
            else
            {
                controller = _environmentControllers[0];
                _environmentControllers.RemoveAt(0);

                InitializeAudioController(controller, clip, settings);
            }

            controller.SetActive(true);
            controller.Position = position;

            controller.Play();

            _environmentControllers.Add(controller);
        }

        public void StopFirstEnvironment(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            for (int i = 0; i < _environmentControllers.Count; i++)
            {
                AudioController controller = _environmentControllers[i];

                if (controller == null)
                {
                    _environmentControllers.RemoveAt(i);
                    i--;
                    continue;
                }

                if ((controller.IsPlaying || controller.IsInLoopInterval) && controller.IsSameClip(clip))
                {
                    PoolController(_environmentPool, controller);

                    _environmentControllers.RemoveAt(i);
                    break;
                }
            }
        }

        public void StopEnvironmentAll()
        {
            PoolEnvironmentAll();
        }

        public void PauseEnvironmentAll()
        {
            foreach (AudioController controller in _environmentControllers)
            {
                if (controller == null)
                {
                    continue;
                }

                controller.Pause();
            }
        }

        public void UnPauseEnvironmentAll()
        {
            foreach (AudioController controller in _environmentControllers)
            {
                if (controller == null)
                {
                    continue;
                }

                controller.UnPause();
            }
        }

        /// <summary>
        /// Plays the specified Environment clip with a fade in effect.
        /// Acts like PlayEnvironment but starts at volume 0 and fades in over fadeTime seconds.
        /// </summary>
        public void FadeInEnvironment(AudioClip clip, float fadeTime = 1.0f)
        {
            if (clip == null)
            {
                Debug.LogError($"Environment clip is NULL.");
                return;
            }

            CleanupEnvironmentControllers();

            AudioController controller = null;

            if (_environmentControllers.Count < _environmentSourceCountMax)
            {
                if (_environmentPool.TryDequeue(out controller))
                {
                    InitializeAudioController(controller, clip, AudioSourceSettings.EnvironmentStandard);
                }
                else
                {
                    AudioSource source = CreateEnvironmentSource();
                    controller = CreateAudioController<EnvironmentController>(source, clip, AudioSourceSettings.EnvironmentStandard);
                }
            }
            else
            {
                controller = _environmentControllers[0];
                _environmentControllers.RemoveAt(0);

                InitializeAudioController(controller, clip, AudioSourceSettings.EnvironmentStandard);
            }

            controller.SetActive(true);
            controller.Position = Vector3.zero;
            controller.FadeVolume = 0.0f;
            controller.RecalculateVolume();
            controller.Play();
            controller.FadeIn(fadeTime);

            _environmentControllers.Add(controller);
        }

        /// <summary>
        /// Fades in all active Environment controllers.
        /// </summary>
        public void FadeInEnvironmentAll(float fadeTime = 1.0f)
        {
            foreach (AudioController controller in _environmentControllers)
            {
                if (controller == null)
                {
                    continue;
                }

                controller.FadeIn(fadeTime);
            }
        }

        /// <summary>
        /// Fades out the first active Environment controller playing the specified clip, then pools it.
        /// If the controller is in a loop interval, it is pooled immediately without fading.
        /// </summary>
        public void FadeOutFirstEnvironment(AudioClip clip, float fadeTime = 1.0f)
        {
            if (clip == null)
            {
                return;
            }

            for (int i = 0; i < _environmentControllers.Count; i++)
            {
                AudioController controller = _environmentControllers[i];

                if (controller == null)
                {
                    _environmentControllers.RemoveAt(i);
                    i--;
                    continue;
                }

                if ((controller.IsPlaying || controller.IsInLoopInterval) && controller.IsSameClip(clip))
                {
                    _environmentControllers.RemoveAt(i);
                    _environmentFadingOutControllers.Add(controller);
                    controller.FadeOut(fadeTime, () =>
                    {
                        _environmentFadingOutControllers.Remove(controller);
                        PoolController(_environmentPool, controller);
                    });
                    break;
                }
            }
        }

        /// <summary>
        /// Fades out all active Environment controllers, then pools each one.
        /// </summary>
        public void FadeOutEnvironmentAll(float fadeTime = 1.0f)
        {
            if (_environmentControllers.Count == 0)
            {
                return;
            }

            // Snapshot and clear the tracking list so Update() does not interfere during fade.
            AudioController[] snapshot = _environmentControllers.ToArray();
            _environmentControllers.Clear();

            foreach (AudioController controller in snapshot)
            {
                if (controller == null)
                {
                    continue;
                }

                _environmentFadingOutControllers.Add(controller);
                controller.FadeOut(fadeTime, () =>
                {
                    _environmentFadingOutControllers.Remove(controller);
                    PoolController(_environmentPool, controller);
                });
            }
        }

        public void PlayEnvironmentWithKey(string key) => PlayEnvironmentWithKey(key, AudioSourceSettings.EnvironmentStandard, Vector3.zero);

        public void PlayEnvironmentWithKey(string key, AudioSourceSettings settings) => PlayEnvironmentWithKey(key, settings, Vector3.zero);

        public void PlayEnvironmentWithKey(string key, Vector3 position) => PlayEnvironmentWithKey(key, AudioSourceSettings.EnvironmentStandard, position);

        public void PlayEnvironmentWithKey(string key, AudioSourceSettings settings, Vector3 position)
        {
            if (TryGetCachedAudioResource(key, out AudioClip clip))
            {
                PlayEnvironment(clip, settings, position);
            }
        }

        public void StopFirstEnvironmentWithKey(string key)
        {
            if (TryGetCachedAudioResource(key, out AudioClip clip))
            {
                StopFirstEnvironment(clip);
            }
        }

        public void FadeInEnvironmentWithKey(string key) => FadeInEnvironmentWithKey(key, 1.0f);

        public void FadeInEnvironmentWithKey(string key, float fadeTime)
        {
            if (TryGetCachedAudioResource(key, out AudioClip clip))
            {
                FadeInEnvironment(clip, fadeTime);
            }
        }

        public void FadeOutFirstEnvironmentWithKey(string key) => FadeOutFirstEnvironmentWithKey(key, 1.0f);

        public void FadeOutFirstEnvironmentWithKey(string key, float fadeTime)
        {
            if (TryGetCachedAudioResource(key, out AudioClip clip))
            {
                FadeOutFirstEnvironment(clip, fadeTime);
            }
        }

        private void SetEnvironmentVolume(float value)
        {
            if (_environmentVolume == value)
            {
                return;
            }

            _environmentVolume = value;
            _calculatedEnvironmentVolume = _masterVolume * value;

            foreach (AudioController controller in _environmentControllers)
            {
                controller.RecalculateVolume();
            }

            OnEnvironmentVolumeChanged?.Invoke(_environmentVolume);
        }

        private AudioSource CreateEnvironmentSource()
        {
            GameObject instance = new GameObject("EnvironmentSource");
            instance.transform.SetParent(_rootTransform);

            AudioSource source = instance.AddComponent<AudioSource>();
            source.playOnAwake = false;

            return source;
        }

        private void PoolEnvironmentAll()
        {
            foreach (AudioController controller in _environmentControllers)
            {
                PoolController(_environmentPool, controller);
            }
            _environmentControllers.Clear();

            foreach (AudioController controller in _environmentFadingOutControllers)
            {
                PoolController(_environmentPool, controller);
            }
            _environmentFadingOutControllers.Clear();
        }

        private void CleanupEnvironmentControllers()
        {
            for (int i = 0; i < _environmentControllers.Count; i++)
            {
                if (_environmentControllers[i] != null)
                {
                    continue;
                }

                _environmentControllers.RemoveAt(i);
                i--;
            }
        }
    }
}
