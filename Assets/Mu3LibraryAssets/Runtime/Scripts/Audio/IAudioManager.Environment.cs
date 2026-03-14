using UnityEngine;

namespace Mu3Library.Audio
{
    public partial interface IAudioManager
    {
        public int EnvironmentInstanceCountMax { get; set; }

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

        public void PlayEnvironmentWithKey(string key);
        public void PlayEnvironmentWithKey(string key, AudioSourceSettings settings);
        public void PlayEnvironmentWithKey(string key, Vector3 position);
        public void PlayEnvironmentWithKey(string key, AudioSourceSettings settings, Vector3 position);
        public void StopFirstEnvironmentWithKey(string key);
        public void FadeInEnvironmentWithKey(string key);
        public void FadeInEnvironmentWithKey(string key, float fadeTime);
        public void FadeOutFirstEnvironmentWithKey(string key);
        public void FadeOutFirstEnvironmentWithKey(string key, float fadeTime);
    }
}
