using System.Collections.Generic;
using UnityEngine;
using Mu3Library.DI;
using System;

using Object = UnityEngine.Object;

namespace Mu3Library.Audio
{
    /// <summary>
    /// Manages audio playback including BGM (Background Music) and SFX (Sound Effects).
    /// Provides volume control, fading, and pooling for efficient audio management.
    /// </summary>
    public class AudioManager : IAudioManager, IAudioVolumeSettings, IAudioManagerEventBus, IUpdatable, IDisposable
    {
        private GameObject m_root;
        private GameObject _root
        {
            get
            {
                if (m_root == null)
                {
                    m_root = new GameObject("AudioManagerRoot");
                    Object.DontDestroyOnLoad(m_root);
                }

                return m_root;
            }
        }

        private Transform _rootTransform => _root.transform;

        private readonly List<AudioController> _sfxControllers = new();
        private readonly Queue<AudioController> _sfxPool = new();
        private readonly List<AudioController> _sfxFadingOutControllers = new();

        private readonly List<AudioController> _environmentControllers = new();
        private readonly Queue<AudioController> _environmentPool = new();
        private readonly List<AudioController> _environmentFadingOutControllers = new();

        private AudioController _bgmMainController = null;
        private AudioController _bgmSubController = null;

        private const float DefaultMasterVolume = 0.8f;
        private float _masterVolume = DefaultMasterVolume;
        public float MasterVolume
        {
            get => _masterVolume;
            set => SetMasterVolume(value);
        }

        private const float DefaultBgmVolume = 0.8f;
        private float _bgmVolume = DefaultBgmVolume;
        public float BgmVolume
        {
            get => _bgmVolume;
            set => SetBgmVolume(value);
        }
        private float _calculatedBgmVolume = DefaultMasterVolume * DefaultBgmVolume;
        public float CalculatedBgmVolume => _calculatedBgmVolume;

        private const float DefaultSfxVolume = 1.0f;
        private float _sfxVolume = DefaultSfxVolume;
        public float SfxVolume
        {
            get => _sfxVolume;
            set => SetSfxVolume(value);
        }
        private float _calculatedSfxVolume = DefaultMasterVolume * DefaultSfxVolume;
        public float CalculatedSfxVolume => _calculatedSfxVolume;

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

        private const float DefaultEnvironmentVolume = 0.8f;
        private float _environmentVolume = DefaultEnvironmentVolume;
        public float EnvironmentVolume
        {
            get => _environmentVolume;
            set => SetEnvironmentVolume(value);
        }
        private float _calculatedEnvironmentVolume = DefaultMasterVolume * DefaultEnvironmentVolume;
        public float CalculatedEnvironmentVolume => _calculatedEnvironmentVolume;

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

        public event Action<float> OnMasterVolumeChanged;
        public event Action<float> OnBgmVolumeChanged;
        public event Action<float> OnSfxVolumeChanged;
        public event Action<float> OnEnvironmentVolumeChanged;



        public void Dispose()
        {
            Stop();

            PoolSfxAll();
            _sfxPool.Clear();

            PoolEnvironmentAll();
            _environmentPool.Clear();

            if (m_root != null)
            {
                Object.Destroy(m_root);
            }
        }

        public void Update()
        {
            if (_sfxControllers.Count > 0)
            {
                for (int i = 0; i < _sfxControllers.Count; i++)
                {
                    AudioController controller = _sfxControllers[i];

                    if (controller == null)
                    {
                        _sfxControllers.RemoveAt(i);
                        i--;
                        continue;
                    }

                    if (controller.IsCompleted)
                    {
                        PoolController(_sfxPool, controller);
                        _sfxControllers.RemoveAt(i);
                        i--;
                    }
                }
            }

            if (_environmentControllers.Count > 0)
            {
                for (int i = 0; i < _environmentControllers.Count; i++)
                {
                    AudioController controller = _environmentControllers[i];

                    if (controller == null)
                    {
                        _environmentControllers.RemoveAt(i);
                        i--;
                        continue;
                    }

                    if (controller.IsCompleted)
                    {
                        PoolController(_environmentPool, controller);
                        _environmentControllers.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        #region Utility

        public void ResetVolumeAll()
        {
            SetMasterVolume(DefaultMasterVolume);
            SetBgmVolume(DefaultBgmVolume);
            SetSfxVolume(DefaultSfxVolume);
            SetEnvironmentVolume(DefaultEnvironmentVolume);
        }

        public void ResetMasterVolume()
        {
            SetMasterVolume(DefaultMasterVolume);
        }

        public void ResetBgmVolume()
        {
            SetBgmVolume(DefaultBgmVolume);
        }

        public void ResetSfxVolume()
        {
            SetSfxVolume(DefaultSfxVolume);
        }

        public void ResetEnvironmentVolume()
        {
            SetEnvironmentVolume(DefaultEnvironmentVolume);
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

        public void Stop()
        {
            StopSfxAll();
            StopEnvironmentAll();
            StopBgm();
        }

        public void Pause()
        {
            PauseSfxAll();
            PauseEnvironmentAll();
            PauseBgm();
        }

        public void UnPause()
        {
            UnPauseSfxAll();
            UnPauseEnvironmentAll();
            UnPauseBgm();
        }

        #endregion

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

        private void SetMasterVolume(float value)
        {
            if (_masterVolume == value)
            {
                return;
            }

            _masterVolume = value;
            _calculatedBgmVolume = value * _bgmVolume;
            _calculatedSfxVolume = value * _sfxVolume;
            _calculatedEnvironmentVolume = value * _environmentVolume;

            foreach (AudioController controller in _sfxControllers)
            {
                controller.RecalculateVolume();
            }

            foreach (AudioController controller in _environmentControllers)
            {
                controller.RecalculateVolume();
            }

            if (_bgmMainController != null)
            {
                _bgmMainController.RecalculateVolume();
            }
            if (_bgmSubController != null)
            {
                _bgmSubController.RecalculateVolume();
            }

            OnMasterVolumeChanged?.Invoke(_masterVolume);
        }

        private AudioSource CreateSfxSource()
        {
            GameObject instance = new GameObject("SfxSource");
            instance.transform.SetParent(_rootTransform);

            AudioSource source = instance.AddComponent<AudioSource>();
            source.playOnAwake = false;

            return source;
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

        private void PoolController(Queue<AudioController> pool, AudioController controller)
        {
            if (controller == null)
            {
                return;
            }

            // Stop() is safe to call even when already stopped; it handles loop/fade cleanup.
            controller.Stop();
            controller.SetActive(false);

            pool.Enqueue(controller);
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

        private AudioSource CreateBgmSource()
        {
            GameObject instance = new GameObject("BgmSource");
            instance.transform.SetParent(_rootTransform);

            AudioSource source = instance.AddComponent<AudioSource>();
            source.playOnAwake = false;

            return source;
        }

        private void InitializeAudioController(AudioController controller, AudioClip clip, AudioSourceSettings settings)
        {
            // Always stop before re-initializing to clean up any active loop or fade coroutines.
            controller.Stop();

            AudioSourceSettings p = settings;

            controller.SetVolumeSettings(this);
            controller.SetLoopSettings(p.LoopCount, p.LoopInterval);
            controller.SetClip(clip);
            controller.SetClipVolume(p.Volume);
            controller.SetAudioParameters(p.BaseSettings);
            controller.SetAudioParameters(p.SoundSettings);
        }

        private AudioController CreateAudioController<T>(AudioSource source, AudioClip clip, AudioSourceSettings settings) where T : AudioController
        {
            if (source == null || clip == null)
            {
                Debug.LogError("AudioSource not found.");
                return null;
            }

            AudioSourceSettings p = settings;

            AudioController controller = source.gameObject.GetComponent<T>();
            if (controller == null)
            {
                controller = source.gameObject.AddComponent<T>();
            }

            controller.SetVolumeSettings(this);
            controller.SetLoopSettings(p.LoopCount, p.LoopInterval);
            controller.SetClip(clip);
            controller.SetClipVolume(p.Volume);
            controller.SetAudioParameters(p.BaseSettings);
            controller.SetAudioParameters(p.SoundSettings);

            return controller;
        }
    }
}
