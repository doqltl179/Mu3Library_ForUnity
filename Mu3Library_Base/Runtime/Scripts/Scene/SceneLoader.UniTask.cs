#if MU3LIBRARY_UNITASK_SUPPORT
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Mu3Library.Scene
{
    public partial class SceneLoader
    {
        public UniTask<bool> PreloadSingleSceneAsync(string sceneName)
        {
            return RunSceneCommandAsync(
                sceneName,
                startCommand: name => TryPreloadSingleScene(name, autoActivate: false),
                isCompleted: IsSingleScenePreloaded,
                isInProgress: IsSingleSceneOperationInProgressFor);
        }

        public UniTask<bool> ActivateSingleSceneAsync(string sceneName)
        {
            return RunSceneCommandAsync(
                sceneName,
                startCommand: TryActivateSingleScene,
                isCompleted: IsSingleSceneLoaded,
                isInProgress: IsSingleSceneOperationInProgressFor);
        }

        public UniTask<bool> LoadSingleSceneAsync(string sceneName)
        {
            return RunSceneCommandAsync(
                sceneName,
                startCommand: name => TryPreloadSingleScene(name, autoActivate: true),
                isCompleted: IsSingleSceneLoaded,
                isInProgress: IsSingleSceneOperationInProgressFor);
        }

        public UniTask<bool> PreloadAdditiveSceneAsync(string sceneName)
        {
            return RunSceneCommandAsync(
                sceneName,
                startCommand: name => TryPreloadAdditiveScene(name, autoActivate: false),
                isCompleted: IsAdditiveScenePreloaded,
                isInProgress: IsAdditiveSceneOperationInProgressFor);
        }

        public UniTask<bool> ActivateAdditiveSceneAsync(string sceneName)
        {
            return RunSceneCommandAsync(
                sceneName,
                startCommand: TryActivateAdditiveScene,
                isCompleted: IsAdditiveSceneLoaded,
                isInProgress: IsAdditiveSceneOperationInProgressFor);
        }

        public UniTask<bool> LoadAdditiveSceneAsync(string sceneName)
        {
            return RunSceneCommandAsync(
                sceneName,
                startCommand: name => TryPreloadAdditiveScene(name, autoActivate: true),
                isCompleted: IsAdditiveSceneLoaded,
                isInProgress: IsAdditiveSceneOperationInProgressFor);
        }

        public UniTask<bool> UnloadAdditiveSceneAsync(string sceneName)
        {
            return RunSceneCommandAsync(
                sceneName,
                startCommand: TryStartUnloadAdditiveScene,
                isCompleted: IsAdditiveSceneUnloaded,
                isInProgress: IsAdditiveSceneUnloadInProgressFor);
        }

        private async UniTask<bool> RunSceneCommandAsync(
            string sceneName,
            Func<string, bool> startCommand,
            Func<string, bool> isCompleted,
            Func<string, bool> isInProgress)
        {
            if (!startCommand(sceneName))
            {
                return false;
            }

            if (isCompleted(sceneName))
            {
                return true;
            }

            if (!isInProgress(sceneName))
            {
                return false;
            }

            await UniTask.WaitUntil(() => isCompleted(sceneName) || !isInProgress(sceneName));
            return isCompleted(sceneName);
        }

        private bool IsSingleScenePreloaded(string sceneName)
        {
            return TryGetSingleSceneStatus(out SceneStatus status)
                && status.SceneName == sceneName
                && (status.Phase == ScenePhase.Preloaded || status.Phase == ScenePhase.Activating || status.Phase == ScenePhase.Loaded);
        }

        private bool IsSingleSceneLoaded(string sceneName)
        {
            return TryGetSingleSceneStatus(out SceneStatus status)
                && status.SceneName == sceneName
                && status.Phase == ScenePhase.Loaded;
        }

        private bool IsSingleSceneOperationInProgressFor(string sceneName)
        {
            return TryGetSingleSceneOperation(out SceneOperation operation) && operation.SceneName == sceneName;
        }

        private bool IsAdditiveScenePreloaded(string sceneName)
        {
            return TryGetAdditiveSceneStatus(sceneName, out SceneStatus status)
                && (status.Phase == ScenePhase.Preloaded || status.Phase == ScenePhase.Activating || status.Phase == ScenePhase.Loaded);
        }

        private bool IsAdditiveSceneLoaded(string sceneName)
        {
            return TryGetAdditiveSceneStatus(sceneName, out SceneStatus status)
                && status.Phase == ScenePhase.Loaded;
        }

        private bool IsAdditiveSceneUnloaded(string sceneName)
        {
            return !TryGetAdditiveSceneStatus(sceneName, out _);
        }

        private bool IsAdditiveSceneOperationInProgressFor(string sceneName)
        {
            return TryGetAdditiveSceneOperation(sceneName, out SceneOperation operation) && !operation.IsUnload;
        }

        private bool IsAdditiveSceneUnloadInProgressFor(string sceneName)
        {
            return TryGetAdditiveSceneOperation(sceneName, out SceneOperation operation) && operation.IsUnload;
        }
    }
}
#endif
