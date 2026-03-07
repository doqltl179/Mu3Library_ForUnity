using UnityEngine;

namespace Mu3Library.Audio
{
    public partial interface IAudioManager
    {
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
    }
}
