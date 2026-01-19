using System.Collections.Generic;
using UnityEngine;
using Mu3Library.DI;

using IDisposable = System.IDisposable;

namespace Mu3Library.Audio
{
    public class AudioManager : IAudioManager, IAudioVolumeSettings, IUpdatable, IDisposable
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

        private List<AudioController> _sfxControllers = new();
        private Queue<AudioController> _sfxPool = new();

        private AudioController _bgmMainController = null;
        private AudioController _bgmSubController = null;

        private AudioSourceSettings _sourceSettings = AudioSourceSettings.Standard;
        public AudioSourceSettings SourceSettings
        {
            get => _sourceSettings;
            set => _sourceSettings = value;
        }
        public AudioBaseSettings BaseSettings
        {
            get => _sourceSettings.BaseSettings;
            set => _sourceSettings.BaseSettings = value;
        }
        public Audio3dSoundSettings SoundSettings
        {
            get => _sourceSettings.SoundSettings;
            set => _sourceSettings.SoundSettings = value;
        }

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

        public event System.Action<float> OnMasterVolumeChanged;
        public event System.Action<float> OnBgmVolumeChanged;
        public event System.Action<float> OnSfxVolumeChanged;



        public void Dispose()
        {
            Stop();
            PoolSfxAll();
            _sfxPool.Clear();

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

            _bgmMainController.FadeOut(fadeTime, _bgmMainController.Pause);
        }

        public void TransitionBgm(AudioClip clip) => TransitionBgm(clip, 1.0f, _sourceSettings);

        public void TransitionBgm(AudioClip clip, float transitionTime) => TransitionBgm(clip, transitionTime, _sourceSettings);

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

        public void PlayBgmForce(AudioClip clip) => PlayBgmForce(clip, _sourceSettings);

        public void PlayBgmForce(AudioClip clip, AudioSourceSettings settings)
        {
            if (_bgmMainController != null && _bgmMainController.IsPlaying)
            {
                _bgmMainController.Stop();
            }

            PlayBgm(clip, settings);
        }

        public void PlayBgm(AudioClip clip) => PlayBgm(clip, _sourceSettings);

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

        public void PlaySfx(AudioClip clip) => PlaySfx(clip, _sourceSettings, Vector3.zero);

        public void PlaySfx(AudioClip clip, AudioSourceSettings settings) => PlaySfx(clip, settings, Vector3.zero);

        public void PlaySfx(AudioClip clip, Vector3 position) => PlaySfx(clip, _sourceSettings, position);

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

                if (controller.IsPlaying && controller.IsSameClip(clip))
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

        private void SetMasterVolume(float value)
        {
            if (_masterVolume == value)
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

            OnMasterVolumeChanged?.Invoke(_masterVolume);
        }

        private AudioSource CreateSfxSource()
        {
            GameObject instance = new GameObject("SfxSource");
            instance.transform.SetParent(_rootTransform);

            AudioSource source = instance.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            return source;
        }

        private void PoolController(Queue<AudioController> pool, AudioController controller)
        {
            if (controller == null)
            {
                return;
            }

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
            source.loop = true;

            return source;
        }

        private void InitializeAudioController(AudioController controller, AudioClip clip, AudioSourceSettings settings)
        {
            if (controller.IsPlaying)
            {
                controller.Stop();
            }

            AudioSourceSettings p = settings;

            controller.SetVolumeSettings(this);
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
            controller.SetClip(clip);
            controller.SetClipVolume(p.Volume);
            controller.SetAudioParameters(p.BaseSettings);
            controller.SetAudioParameters(p.SoundSettings);

            return controller;
        }
    }
}
