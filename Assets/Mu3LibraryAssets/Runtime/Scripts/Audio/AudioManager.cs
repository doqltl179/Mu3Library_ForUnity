using System;
using System.Collections.Generic;
using Mu3Library.Utility;
using UnityEngine;

namespace Mu3Library.Audio
{
    public class AudioManager : GenericSingleton<AudioManager>
    {
        private List<AudioController> _sfxControllers = new();
        private Queue<AudioController> _sfxPool = new();

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
        /// <br/> min: 1
        /// <br/> max: 10
        /// </summary>
        public int SfxInstanceCountMax
        {
            get => _sfxSourceCountMax;
            set => _sfxSourceCountMax = Mathf.Min(Mathf.Max(value, 1), 10);
        }

        private readonly AudioParameters _standardParameters = new()
        {
            Volume = 1.0f,
            Base = _standardBaseParameters,
            SoundSettings = _standard3dSoundSettings,
        };

        private static readonly AudioBaseParameters _standardBaseParameters = new()
        {
            Priority = 128,
            Pitch = 1.0f,
            StereoPan = 0.0f,
            SpatialBlend = 0.0f,
            ReverbZoneMix = 1.0f,
        };

        private static readonly Audio3dSoundSettings _standard3dSoundSettings = new()
        {
            DopplerLevel = 1.0f,
            Spread = 0.0f,
            AudioRolloffMode = AudioRolloffMode.Linear,
            MinDistance = 1.0f,
            MaxDistance = 500.0f,
        };

        public event Action<float> OnMasterVolumeChanged;
        public event Action<float> OnBgmVolumeChanged;
        public event Action<float> OnSfxVolumeChanged;



        private void Update()
        {
            if (_sfxControllers.Count > 0)
            {
                for (int i = 0; i < _sfxControllers.Count; i++)
                {
                    AudioController controller = _sfxControllers[i];
                    if (controller.NormalizedTime >= 0.97f)
                    {
                        PoolController(_sfxPool, controller);
                        _sfxControllers.RemoveAt(i);
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
            else if (!_bgmMainController.IsPlaying)
            {
                _bgmMainController.Play();
            }

            _bgmMainController.FadeIn(fadeTime);
        }

        public void FadeOutBgm(float fadeTime = 1.0f)
        {
            if (_bgmMainController == null || !_bgmMainController.IsPlaying)
            {
                return;
            }

            _bgmMainController.FadeOut(fadeTime, () => _bgmMainController.Pause());
        }

        public void TransitionBgm(AudioClip clip, float transitionTime = 1.0f, AudioParameters? parameters = null)
        {
            if (clip == null)
            {
                Debug.LogError($"BGM clip is NULL.");
                return;
            }

            AudioController from = _bgmMainController;
            if (from != null)
            {
                from.FadeOut(transitionTime, () => from.Stop());
            }

            AudioController to = _bgmSubController;
            if (to == null)
            {
                AudioSource source = CreateBgmSource();
                to = CreateAudioController<BgmController>(source, clip, parameters);
            }

            if (!to.IsPlaying || !to.IsSameClip(clip))
            {
                InitializeAudioController(to, clip, parameters);
                to.FadeVolume = 0.0f;
                to.RecalculateVolume();
                to.Play();
            }

            to.FadeIn(transitionTime);

            _bgmMainController = to;
            _bgmSubController = from;
        }

        public void PlayBgmForce(AudioClip clip, AudioParameters? parameters = null)
        {
            if (_bgmMainController != null && _bgmMainController.IsPlaying)
            {
                _bgmMainController.Stop();
            }

            PlayBgm(clip, parameters);
        }

        public void PlayBgm(AudioClip clip, AudioParameters? parameters = null)
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

                InitializeAudioController(_bgmMainController, clip, parameters);
            }
            else
            {
                AudioSource source = CreateBgmSource();
                _bgmMainController = CreateAudioController<BgmController>(source, clip, parameters);
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

        public void PlaySfx(AudioClip clip, AudioParameters? parameters = null)
        {
            if (clip == null)
            {
                Debug.LogError($"SFX clip is NULL.");
                return;
            }

            AudioController controller = null;

            if (_sfxControllers.Count < _sfxSourceCountMax)
            {
                if (_sfxPool.Count > 0)
                {
                    controller = _sfxPool.Dequeue();
                    InitializeAudioController(controller, clip, parameters);
                }
                else
                {
                    AudioSource source = CreateSfxSource();
                    controller = CreateAudioController<SfxController>(source, clip, parameters);
                }
            }
            else
            {
                controller = _sfxControllers[0];
                _sfxControllers.RemoveAt(0);

                InitializeAudioController(controller, clip, parameters);
            }

            controller.SetActive(true);

            controller.Play();

            _sfxControllers.Add(controller);
        }

        public void StopSfxAll()
        {
            PoolSfxAll();
        }

        public void PauseSfxAll()
        {
            foreach (AudioController controller in _sfxControllers)
            {
                controller.Pause();
            }
        }

        public void UnPauseSfxAll()
        {
            foreach (AudioController controller in _sfxControllers)
            {
                controller.UnPause();
            }
        }

        public void Stop()
        {
            StopSfxAll();
            StopBgm();
        }

        public void Pause()
        {
            PauseSfxAll();
            PauseBgm();
        }

        public void UnPause()
        {
            UnPauseSfxAll();
            UnPauseBgm();
        }

        public AudioParameters GetAudioStandardParameters()
        {
            return _standardParameters;
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

            OnSfxVolumeChanged?.Invoke(value);
        }

        private void SetBgmVolume(float value)
        {
            if(_bgmVolume == value)
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

            OnBgmVolumeChanged?.Invoke(value);
        }

        private void SetMasterVolume(float value)
        {
            if(_masterVolume == value)
            {
                return;
            }

            _masterVolume = value;
            _calculatedBgmVolume = value * _bgmVolume;
            _calculatedSfxVolume = value * _sfxVolume;

            foreach (AudioController controller in _sfxControllers)
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

            OnMasterVolumeChanged?.Invoke(value);
        }

        private AudioSource CreateSfxSource()
        {
            GameObject instance = new GameObject("SfxSource");
            instance.transform.SetParent(transform);

            AudioSource source = instance.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            return source;
        }

        private void PoolController(Queue<AudioController> pool, AudioController controller)
        {
            if (controller.IsPlaying)
            {
                controller.Stop();
            }

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
        }

        private AudioSource CreateBgmSource()
        {
            GameObject instance = new GameObject("BgmSource");
            instance.transform.SetParent(transform);

            AudioSource source = instance.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;

            return source;
        }

        private void InitializeAudioController(AudioController controller, AudioClip clip, AudioParameters? parameters = null)
        {
            if (controller.IsPlaying)
            {
                controller.Stop();
            }

            if (parameters == null)
            {
                parameters = _standardParameters;
            }
            AudioParameters p = parameters.Value;

            controller.SetClip(clip);
            controller.SetVolume(p.Volume);
            controller.SetAudioParameters(p.Base);
            controller.SetAudioParameters(p.SoundSettings);
        }

        private AudioController CreateAudioController<T>(AudioSource source, AudioClip clip, AudioParameters? parameters = null) where T : AudioController
        {
            if (source == null || clip == null)
            {
                Debug.LogError("AudioSource not found.");
                return null;
            }

            if (parameters == null)
            {
                parameters = _standardParameters;
            }
            AudioParameters p = parameters.Value;

            AudioController controller = source.gameObject.GetComponent<T>();
            if (controller == null)
            {
                controller = source.gameObject.AddComponent<T>();
            }

            controller.SetClip(clip);
            controller.SetVolume(p.Volume);
            controller.SetAudioParameters(p.Base);
            controller.SetAudioParameters(p.SoundSettings);

            return controller;
        }
    }
}
