using Mu3Library.Audio;
using Mu3Library.DI;
using Mu3Library.Sample.Template.Common;
using Mu3Library.Scene;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template
{
    public class SampleAudioCore : CoreBase
    {
        private IAudioManager _audioManager;
        private IAudioVolumeSettings _audioVolumeSettings;
#if UNITY_EDITOR
        private IEditorSceneLoader _sceneLoader;
#else
        private ISceneLoader _sceneLoader;
#endif

        [Header("Volume")]
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _bgmVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;

        [Header("BGM")]
        [SerializeField] private Button _bgm01Button;
        [SerializeField] private Button _bgm02Button;
        [SerializeField] private Button _bgm03Button;
        [SerializeField] private Button _bgm01TransitionButton;
        [SerializeField] private Button _bgm02TransitionButton;
        [SerializeField] private Button _bgm03TransitionButton;
        [SerializeField] private Button _bgmFadeInButton;
        [SerializeField] private Button _bgmFadeOutButton;
        [SerializeField] private AudioClip _bgm01;
        [SerializeField] private AudioClip _bgm02;
        [SerializeField] private AudioClip _bgm03;

        [Header("SFX")]
        [SerializeField] private Button _sfx01Button;
        [SerializeField] private Button _sfx02Button;
        [SerializeField] private Button _sfx03Button;
        [SerializeField] private AudioClip _sfx01;
        [SerializeField] private AudioClip _sfx02;
        [SerializeField] private AudioClip _sfx03;

        [Header("Else")]
        [SerializeField] private Button _backButton;



        private void OnEnable()
        {
            RegisterUiEvents();
        }

        private void OnDisable()
        {
            UnregisterUiEvents();
        }

        protected override void Start()
        {
            base.Start();

            WaitForCore<CommonCore>(OnCommonCoreAdded);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _audioManager?.Stop();
        }

        private void OnCommonCoreAdded(CommonCore _)
        {
            _audioManager = GetFromCore<CommonCore, IAudioManager>();
            _audioVolumeSettings = GetFromCore<CommonCore, IAudioVolumeSettings>();

            _sceneLoader =
#if UNITY_EDITOR
                GetFromCore<CommonCore, IEditorSceneLoader>();
#else
                GetFromCore<CommonCore, ISceneLoader>();
#endif

            if (_audioVolumeSettings == null)
            {
                return;
            }

            _masterVolumeSlider.SetValueWithoutNotify(_audioVolumeSettings.MasterVolume);
            _bgmVolumeSlider.SetValueWithoutNotify(_audioVolumeSettings.BgmVolume);
            _sfxVolumeSlider.SetValueWithoutNotify(_audioVolumeSettings.SfxVolume);
        }

        private void RegisterUiEvents()
        {
            _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            _bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
            _sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

            _bgm01Button.onClick.AddListener(PlayBgm01);
            _bgm02Button.onClick.AddListener(PlayBgm02);
            _bgm03Button.onClick.AddListener(PlayBgm03);

            _bgm01TransitionButton.onClick.AddListener(TransitionBgm01);
            _bgm02TransitionButton.onClick.AddListener(TransitionBgm02);
            _bgm03TransitionButton.onClick.AddListener(TransitionBgm03);

            _bgmFadeInButton.onClick.AddListener(FadeIn);
            _bgmFadeOutButton.onClick.AddListener(FadeOut);

            _sfx01Button.onClick.AddListener(PlaySfx01);
            _sfx02Button.onClick.AddListener(PlaySfx02);
            _sfx03Button.onClick.AddListener(PlaySfx03);

            _backButton.onClick.AddListener(OnBackButtonClicked);
        }

        private void UnregisterUiEvents()
        {
            _masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            _bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);
            _sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);

            _bgm01Button.onClick.RemoveListener(PlayBgm01);
            _bgm02Button.onClick.RemoveListener(PlayBgm02);
            _bgm03Button.onClick.RemoveListener(PlayBgm03);

            _bgm01TransitionButton.onClick.RemoveListener(TransitionBgm01);
            _bgm02TransitionButton.onClick.RemoveListener(TransitionBgm02);
            _bgm03TransitionButton.onClick.RemoveListener(TransitionBgm03);

            _bgmFadeInButton.onClick.RemoveListener(FadeIn);
            _bgmFadeOutButton.onClick.RemoveListener(FadeOut);

            _sfx01Button.onClick.RemoveListener(PlaySfx01);
            _sfx02Button.onClick.RemoveListener(PlaySfx02);
            _sfx03Button.onClick.RemoveListener(PlaySfx03);

            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }

        #region UI Event
        private void OnMasterVolumeChanged(float value)
        {
            if (_audioVolumeSettings == null)
            {
                return;
            }

            _audioVolumeSettings.MasterVolume = value;
        }

        private void OnBgmVolumeChanged(float value)
        {
            if (_audioVolumeSettings == null)
            {
                return;
            }

            _audioVolumeSettings.BgmVolume = value;
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (_audioVolumeSettings == null)
            {
                return;
            }

            _audioVolumeSettings.SfxVolume = value;
        }

        private void PlayBgm01() => PlayBgm(_bgm01);
        private void PlayBgm02() => PlayBgm(_bgm02);
        private void PlayBgm03() => PlayBgm(_bgm03);

        private void TransitionBgm01() => TransitionBgm(_bgm01);
        private void TransitionBgm02() => TransitionBgm(_bgm02);
        private void TransitionBgm03() => TransitionBgm(_bgm03);

        private void PlaySfx01() => PlaySfx(_sfx01);
        private void PlaySfx02() => PlaySfx(_sfx02);
        private void PlaySfx03() => PlaySfx(_sfx03);

        private void FadeIn() => _audioManager?.FadeInBgm();
        private void FadeOut() => _audioManager?.FadeOutBgm();

        private void OnBackButtonClicked()
        {
#if UNITY_EDITOR
            _sceneLoader.LoadSingleSceneWithAssetPath(SceneNames.GetSceneAssetPath(SceneNames.Main));
#else
            _sceneLoader.LoadSingleScene(SceneNames.Main);
#endif
        }
        #endregion

        private void PlayBgm(AudioClip clip)
        {
            _audioManager?.PlayBgm(clip);
        }

        private void TransitionBgm(AudioClip clip)
        {
            _audioManager?.TransitionBgm(clip);
        }

        private void PlaySfx(AudioClip clip)
        {
            _audioManager?.PlaySfx(clip);
        }
    }
}
