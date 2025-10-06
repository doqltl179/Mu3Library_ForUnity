using Mu3Library.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Audio
{
    public class SampleSceneAudioController : MonoBehaviour
    {
        [Header("Elements")]
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _bgmVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;

        [Header("BGM")]
        [SerializeField] private AudioClip _bgm01;
        [SerializeField] private AudioClip _bgm02;
        [SerializeField] private AudioClip _bgm03;

        [Header("SFX")]
        [SerializeField] private AudioClip _sfx01;
        [SerializeField] private AudioClip _sfx02;
        [SerializeField] private AudioClip _sfx03;



        private void Start()
        {
            _masterVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.
            MasterVolume);
            _bgmVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.BgmVolume);
            _sfxVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.SfxVolume);
        }

        #region UI Event
        public void OnMasterVolumeChanged(float value) => AudioManager.Instance.
        MasterVolume = value;
        public void OnBgmVolumeChanged(float value) => AudioManager.Instance.BgmVolume = value;
        public void OnSfxVolumeChanged(float value) => AudioManager.Instance.SfxVolume = value;

        public void PlayBgm01() => PlayBgm(_bgm01);
        public void PlayBgm02() => PlayBgm(_bgm02);
        public void PlayBgm03() => PlayBgm(_bgm03);

        public void TransitionBgm01() => TransitionBgm(_bgm01);
        public void TransitionBgm02() => TransitionBgm(_bgm02);
        public void TransitionBgm03() => TransitionBgm(_bgm03);

        public void PlaySfx01() => PlaySfx(_sfx01);
        public void PlaySfx02() => PlaySfx(_sfx02);
        public void PlaySfx03() => PlaySfx(_sfx03);

        public void FadeIn() => AudioManager.Instance.FadeInBgm();
        public void FadeOut() => AudioManager.Instance.FadeOutBgm();
        #endregion

        private void PlayBgm(AudioClip clip)
        {
            AudioManager.Instance.PlayBgm(clip);
        }

        private void TransitionBgm(AudioClip clip)
        {
            AudioManager.Instance.TransitionBgm(clip);
        }

        private void PlaySfx(AudioClip clip)
        {
            AudioManager.Instance.PlaySfx(clip);
        }
    }
}