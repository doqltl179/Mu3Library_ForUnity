using UnityEngine;

namespace Mu3Library.Audio
{
    public partial interface IAudioManager
    {
        public int SfxInstanceCountMax { get; set; }

        public void FadeInSfx(AudioClip clip, float fadeTime = 1.0f);
        public void FadeInSfxAll(float fadeTime = 1.0f);
        public void FadeInSfxWithKey(string key);
        public void FadeInSfxWithKey(string key, float fadeTime);

        public void FadeOutFirstSfx(AudioClip clip, float fadeTime = 1.0f);
        public void FadeOutFirstSfxWithKey(string key);
        public void FadeOutFirstSfxWithKey(string key, float fadeTime);
        public void FadeOutSfxAll(float fadeTime = 1.0f);

        public void PauseSfxAll();

        public void PlaySfx(AudioClip clip);
        public void PlaySfx(AudioClip clip, AudioSourceSettings settings);
        public void PlaySfx(AudioClip clip, Vector3 position);
        public void PlaySfx(AudioClip clip, AudioSourceSettings settings, Vector3 position);
        public void PlaySfxWithKey(string key);
        public void PlaySfxWithKey(string key, AudioSourceSettings settings);
        public void PlaySfxWithKey(string key, Vector3 position);
        public void PlaySfxWithKey(string key, AudioSourceSettings settings, Vector3 position);

        public void StopFirstSfx(AudioClip clip);
        public void StopFirstSfxWithKey(string key);
        public void StopSfxAll();

        public void UnPauseSfxAll();
    }
}
