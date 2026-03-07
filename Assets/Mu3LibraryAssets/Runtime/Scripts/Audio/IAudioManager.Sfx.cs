using UnityEngine;

namespace Mu3Library.Audio
{
    public partial interface IAudioManager
    {
        public int SfxInstanceCountMax { get; set; }

        public void PlaySfx(AudioClip clip);
        public void PlaySfx(AudioClip clip, AudioSourceSettings settings);
        public void PlaySfx(AudioClip clip, Vector3 position);
        public void PlaySfx(AudioClip clip, AudioSourceSettings settings, Vector3 position);
        public void StopFirstSfx(AudioClip clip);
        public void StopSfxAll();
        public void FadeInSfx(AudioClip clip, float fadeTime = 1.0f);
        public void FadeInSfxAll(float fadeTime = 1.0f);
        public void FadeOutFirstSfx(AudioClip clip, float fadeTime = 1.0f);
        public void FadeOutSfxAll(float fadeTime = 1.0f);
        public void PauseSfxAll();
        public void UnPauseSfxAll();
    }
}
