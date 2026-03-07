using System.Collections.Generic;
using UnityEngine;
using Mu3Library.DI;
using System;

using Object = UnityEngine.Object;

namespace Mu3Library.Audio
{
    /// <summary>
    /// Manages audio playback including BGM (Background Music), SFX (Sound Effects), and Environment sounds.
    /// Provides volume control, fading, and pooling for efficient audio management.
    /// </summary>
    public partial class AudioManager : IAudioManager, IAudioVolumeSettings, IAudioManagerEventBus, IUpdatable, IDisposable
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

        private const float DefaultMasterVolume = 0.8f;
        private float _masterVolume = DefaultMasterVolume;
        public float MasterVolume
        {
            get => _masterVolume;
            set => SetMasterVolume(value);
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
