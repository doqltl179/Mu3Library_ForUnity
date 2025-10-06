using Mu3Library.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Audio
{
    public class SampleSceneAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _bgm01;
        [SerializeField] private AudioClip _bgm02;
        [SerializeField] private AudioClip _bgm03;

        [SerializeField] private AudioClip _sfx01;
        [SerializeField] private AudioClip _sfx02;
        [SerializeField] private AudioClip _sfx03;



        #region UI Event
        public void PlayBgm01() => PlayBgm(_bgm01);
        public void PlayBgm02() => PlayBgm(_bgm02);
        public void PlayBgm03() => PlayBgm(_bgm03);

        public void PlaySfx01() => PlaySfx(_sfx01);
        public void PlaySfx02() => PlaySfx(_sfx02);
        public void PlaySfx03() => PlaySfx(_sfx03);
        #endregion

        private void PlayBgm(AudioClip clip)
        {
            AudioManager.Instance.PlayBgm(clip);
        }

        private void PlaySfx(AudioClip clip)
        {
            AudioManager.Instance.PlaySfx(clip);
        }
    }
}