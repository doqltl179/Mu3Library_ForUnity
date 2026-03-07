using UnityEngine;

namespace Mu3Library.Audio
{
    public interface IAudioManager
    {
        public int SfxInstanceCountMax { get; set; }
        public int EnvironmentInstanceCountMax { get; set; }



        public void FadeInBgm(float fadeTime = 1.0f);
        public void FadeOutBgm(float fadeTime = 1.0f);
        public void TransitionBgm(AudioClip clip);
        public void TransitionBgm(AudioClip clip, float transitionTime);
        public void TransitionBgm(AudioClip clip, float transitionTime, AudioSourceSettings settings);
        public void PlayBgmForce(AudioClip clip);
        public void PlayBgmForce(AudioClip clip, AudioSourceSettings settings);
        public void PlayBgm(AudioClip clip);
        public void PlayBgm(AudioClip clip, AudioSourceSettings settings);
        public void StopBgm();
        public void PauseBgm();
        public void UnPauseBgm();

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

        public void PlayEnvironment(AudioClip clip);
        public void PlayEnvironment(AudioClip clip, AudioSourceSettings settings);
        public void PlayEnvironment(AudioClip clip, Vector3 position);
        public void PlayEnvironment(AudioClip clip, AudioSourceSettings settings, Vector3 position);
        public void StopFirstEnvironment(AudioClip clip);
        public void StopEnvironmentAll();
        public void FadeInEnvironment(AudioClip clip, float fadeTime = 1.0f);
        public void FadeInEnvironmentAll(float fadeTime = 1.0f);
        public void FadeOutFirstEnvironment(AudioClip clip, float fadeTime = 1.0f);
        public void FadeOutEnvironmentAll(float fadeTime = 1.0f);
        public void PauseEnvironmentAll();
        public void UnPauseEnvironmentAll();

        public void Stop();
        public void Pause();
        public void UnPause();
    }
}
