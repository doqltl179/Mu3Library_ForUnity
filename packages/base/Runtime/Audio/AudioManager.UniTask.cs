#if MU3LIBRARY_UNITASK_SUPPORT
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mu3Library.Audio
{
    public partial class AudioManager
    {
        /// <summary>
        /// Fades the BGM in and completes when the fade reached full volume.
        /// A fade replaced by a newer one before it finished completes as canceled.
        /// </summary>
        public UniTask FadeInBgmAsync(float fadeTime = 1.0f)
        {
            AudioController controller = _bgmMainController;
            if (controller == null)
            {
                return UniTask.CompletedTask;
            }
            else if (controller.IsPaused)
            {
                controller.UnPause();
            }
            else if (!controller.IsPlaying && !controller.IsInLoopInterval)
            {
                controller.Play();
            }

            return AwaitFade(controller, done => controller.FadeIn(fadeTime, done));
        }

        /// <summary>
        /// Fades the BGM out, pauses it, and completes when the fade reached silence.
        /// A fade replaced by a newer one before it finished completes as canceled.
        /// </summary>
        public UniTask FadeOutBgmAsync(float fadeTime = 1.0f)
        {
            AudioController controller = _bgmMainController;
            if (controller == null || controller.IsCompleted)
            {
                return UniTask.CompletedTask;
            }

            return AwaitFade(controller, done => controller.FadeOut(fadeTime, () =>
            {
                controller.Pause();
                done();
            }));
        }

        /// <summary>
        /// Runs <see cref="TransitionBgm(AudioClip, float, AudioSourceSettings)"/> and completes
        /// when the new track finished fading in. A transition replaced by a newer one before it
        /// finished completes as canceled.
        /// </summary>
        public UniTask TransitionBgmAsync(AudioClip clip, float transitionTime = 1.0f)
            => TransitionBgmAsync(clip, transitionTime, AudioSourceSettings.BgmStandard);

        public UniTask TransitionBgmAsync(AudioClip clip, float transitionTime, AudioSourceSettings settings)
        {
            return AwaitStartedFade(done => TransitionBgmInternal(clip, transitionTime, settings, done));
        }

        public UniTask TransitionBgmWithKeyAsync(string key, float transitionTime = 1.0f)
            => TransitionBgmWithKeyAsync(key, transitionTime, AudioSourceSettings.BgmStandard);

        public UniTask TransitionBgmWithKeyAsync(string key, float transitionTime, AudioSourceSettings settings)
        {
            if (!TryGetCachedAudioResource(key, out AudioClip clip))
            {
                return UniTask.CompletedTask;
            }

            return TransitionBgmAsync(clip, transitionTime, settings);
        }

        /// <summary>
        /// Runs <see cref="DuckBgm"/> and completes when the volume is back at full.
        /// A duck replaced by a newer fade before it finished completes as canceled.
        /// </summary>
        public UniTask DuckBgmAsync(float duckVolume = 0.3f, float fadeOutTime = 0.2f, float holdTime = 0.5f, float fadeInTime = 0.5f)
        {
            AudioController controller = _bgmMainController;
            if (controller == null || controller.IsCompleted)
            {
                return UniTask.CompletedTask;
            }

            return AwaitFade(controller, done => controller.Duck(duckVolume, fadeOutTime, holdTime, fadeInTime, done));
        }

        private static UniTask AwaitFade(AudioController controller, Action<Action> beginFade)
        {
            return AwaitStartedFade(done =>
            {
                beginFade(done);
                return controller;
            });
        }

        /// <summary>
        /// Bridges a callback fade into a task. The interruption hook is attached only after the
        /// fade started, because starting a fade stops the previous one and that interruption
        /// belongs to the previous awaiter, never to this one.
        /// </summary>
        private static UniTask AwaitStartedFade(Func<Action, AudioController> beginFade)
        {
            UniTaskCompletionSource tcs = new();
            AudioController controller = null;
            bool settled = false;
            Action interrupted = null;

            void Complete()
            {
                settled = true;
                if (controller != null && interrupted != null)
                {
                    controller.OnFadeInterrupted -= interrupted;
                }

                tcs.TrySetResult();
            }

            controller = beginFade(Complete);
            if (controller == null)
            {
                return UniTask.CompletedTask;
            }

            if (!settled)
            {
                interrupted = () =>
                {
                    controller.OnFadeInterrupted -= interrupted;
                    tcs.TrySetCanceled();
                };
                controller.OnFadeInterrupted += interrupted;
            }

            return tcs.Task;
        }
    }
}
#endif
