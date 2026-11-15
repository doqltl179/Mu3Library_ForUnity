#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mu3Library.Audio
{
    public partial interface IAudioManager
    {
        public UniTask FadeInBgmAsync(float fadeTime = 1.0f);
        public UniTask FadeOutBgmAsync(float fadeTime = 1.0f);

        public UniTask TransitionBgmAsync(AudioClip clip, float transitionTime = 1.0f);
        public UniTask TransitionBgmAsync(AudioClip clip, float transitionTime, AudioSourceSettings settings);
        public UniTask TransitionBgmWithKeyAsync(string key, float transitionTime = 1.0f);
        public UniTask TransitionBgmWithKeyAsync(string key, float transitionTime, AudioSourceSettings settings);

        public UniTask DuckBgmAsync(float duckVolume = 0.3f, float fadeOutTime = 0.2f, float holdTime = 0.5f, float fadeInTime = 0.5f);
    }
}
#endif
