using UnityEngine;

namespace Mu3Library.Audio
{
    public interface IAudioManager
    {
        public int SfxInstanceCountMax { get; set; }

        public AudioSourceSettings SourceSettings { get; set; }
        public AudioBaseSettings BaseSettings { get; set; }
        public Audio3dSoundSettings SoundSettings { get; set; }

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
        public void PauseSfxAll();
        public void UnPauseSfxAll();

        public void Stop();
        public void Pause();
        public void UnPause();
    }
}
