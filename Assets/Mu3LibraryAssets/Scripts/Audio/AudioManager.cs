using System.Collections;
using System.Collections.Generic;
using Mu3Library.Utility;
using UnityEngine;

namespace Mu3Library.Audio
{
    public class AudioManager : GenericSingleton<AudioManager>
    {
        private List<AudioController> _sfxContainers = new();
        private Queue<AudioController> _sfxPool = new();

        private AudioController _bgmMainContainer = null;
        private AudioController _bgmSubContainer = null;
        private Queue<AudioController> _bgmPool = new();

        private float _masterVolume = 0.8f;
        public float MasterVolume
        {
            get => _masterVolume;
            set => SetMasterVolume(value);
        }

        private float _bgmVolume = 0.8f;
        public float BgmVolume
        {
            get => _bgmVolume;
            set => SetBgmVolume(value);
        }

        private float _sfxVolume = 1.0f;
        public float SfxVolume
        {
            get => _sfxVolume;
            set => SetSfxVolume(value);
        }

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

        private IEnumerator _bgmTransitionCoroutine = null;
        private IEnumerator _bgmFadeCoroutine = null;



        private void Update()
        {
            if (_sfxContainers.Count > 0)
            {
                for (int i = 0; i < _sfxContainers.Count; i++)
                {
                    AudioController controller = _sfxContainers[i];
                    AudioSource source = controller.Source;
                    if (source == null || source.clip == null)
                    {
                        _sfxContainers.RemoveAt(i);
                        i--;
                    }
                    else if (controller.NormalizedTime >= 0.97f)
                    {
                        PoolContainer(_sfxPool, controller);
                        _sfxContainers.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        #region Utility
        public void FadeOutBgm(float fadeTime = 1.0f)
        {

        }

        private IEnumerator FadeOutBgmCoroutine(float fadeTime = 1.0f)
        {
            yield return null;

            _bgmFadeCoroutine = null;
        }

        public void TransitionBgm(AudioClip clip, float transitionTime = 1.0f, AudioParameters? parameters = null)
        {
            if (_bgmTransitionCoroutine != null)
            {
                StopCoroutine(_bgmTransitionCoroutine);
                _bgmTransitionCoroutine = null;
            }

            if (clip == null)
            {
                Debug.LogError($"BGM clip is NULL.");
                return;
            }
            

        }

        private IEnumerator TransitionBgmCoroutine(AudioController to, float transitionTime = 1.0f)
        {
            yield return null;

            _bgmTransitionCoroutine = null;
        }

        private IEnumerator TransitionBgmCoroutine(AudioController from, AudioController to, float transitionTime = 1.0f)
        {
            yield return null;

            _bgmTransitionCoroutine = null;
        }

        public void PlayBgm(AudioClip clip, AudioParameters? parameters = null)
        {
            if (clip == null)
            {
                Debug.LogError($"BGM clip is NULL.");
                return;
            }

            if (_bgmMainContainer == null)
            {
                if (_bgmPool.Count > 0)
                {
                    _bgmMainContainer = _bgmPool.Dequeue();
                    InitializeAudioController(_bgmMainContainer, clip, parameters);
                }
                else
                {
                    AudioSource source = CreateBgmSource();
                    _bgmMainContainer = CreateAudioController(source, clip, parameters);
                }
            }
            else
            {
                InitializeAudioController(_bgmMainContainer, clip, parameters);
            }

            _bgmMainContainer.Play();
        }

        public void StopBgm()
        {
            PoolBgmAll();
        }

        public void PlaySfx(AudioClip clip, AudioParameters? parameters = null)
        {
            if (clip == null)
            {
                Debug.LogError($"SFX clip is NULL.");
                return;
            }

            AudioController controller = null;

            if (_sfxContainers.Count < _sfxSourceCountMax)
            {
                if (_sfxPool.Count > 0)
                {
                    controller = _sfxPool.Dequeue();
                    InitializeAudioController(controller, clip, parameters);
                }
                else
                {
                    AudioSource source = CreateSfxSource();
                    controller = CreateAudioController(source, clip, parameters);
                }
            }
            else
            {
                controller = _sfxContainers[0];
                _sfxContainers.RemoveAt(0);

                InitializeAudioController(controller, clip, parameters);
            }

            controller.SetActive(true);

            controller.Play();

            _sfxContainers.Add(controller);
        }

        public void StopSfxAll()
        {
            PoolSfxAll();
        }

        public void PauseSfxAll()
        {
            foreach (AudioController controller in _sfxContainers)
            {
                controller.Pause();
            }
        }

        public void UnPauseSfxAll()
        {
            foreach (AudioController controller in _sfxContainers)
            {
                controller.UnPause();
            }
        }

        public void PauseAll()
        {
            PauseSfxAll();
        }

        public void UnPauseAll()
        {
            UnPauseSfxAll();
        }

        public AudioParameters GetAudioStandardParameters()
        {
            return _standardParameters;
        }
        #endregion

        private void SetSfxVolume(float value)
        {
            foreach (AudioController controller in _sfxContainers)
            {
                controller.RecalculateVolume(value * _masterVolume);
            }

            _sfxVolume = value;
        }

        private void SetBgmVolume(float value)
        {
            if (_bgmMainContainer != null)
            {
                _bgmMainContainer.RecalculateVolume(value * _masterVolume);
            }
            if (_bgmSubContainer != null)
            {
                _bgmSubContainer.RecalculateVolume(value * _masterVolume);
            }
        }

        private void SetMasterVolume(float value)
        {
            foreach (AudioController controller in _sfxContainers)
            {
                controller.RecalculateVolume(_sfxVolume * value);
            }

            if (_bgmMainContainer != null)
            {
                _bgmMainContainer.RecalculateVolume(_bgmVolume * value);
            }
            if (_bgmSubContainer != null)
            {
                _bgmSubContainer.RecalculateVolume(_bgmVolume * value);
            }

            _masterVolume = value;
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

        private void PoolContainer(Queue<AudioController> pool, AudioController controller)
        {
            if (controller.Source == null)
            {
                Debug.LogError("AudioSource not found.");
                return;
            }

            if (controller.IsPlaying)
            {
                controller.Stop();
            }

            controller.SetActive(false);

            pool.Enqueue(controller);
        }

        private void PoolBgmAll()
        {
            if (_bgmMainContainer != null)
            {
                PoolContainer(_bgmPool, _bgmMainContainer);
                _bgmMainContainer = null;
            }
            if (_bgmSubContainer != null)
            {
                PoolContainer(_bgmPool, _bgmSubContainer);
                _bgmSubContainer = null;
            }
        }

        private void PoolSfxAll()
        {
            foreach (AudioController controller in _sfxContainers)
            {
                PoolContainer(_sfxPool, controller);
            }
            _sfxContainers.Clear();
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
            AudioSource source = controller.Source;
            if (source == null)
            {
                Debug.LogError("AudioSource not found.");
                return;
            }

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
            controller.SetVolume(p.Volume, _bgmVolume * _masterVolume);
            controller.SetAudioParameters(p.Base);
            controller.SetAudioParameters(p.SoundSettings);
        }

        private AudioController CreateAudioController(AudioSource source, AudioClip clip, AudioParameters? parameters = null)
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

            AudioController controller = source.gameObject.AddComponent<AudioController>();
            controller.SetClip(clip);
            controller.SetVolume(p.Volume, _bgmVolume * _masterVolume);
            controller.SetAudioParameters(p.Base);
            controller.SetAudioParameters(p.SoundSettings);

            return controller;
        }
    }
}
