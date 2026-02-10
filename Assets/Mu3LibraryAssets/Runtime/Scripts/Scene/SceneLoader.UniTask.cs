#if MU3LIBRARY_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mu3Library.Scene
{
    public partial class SceneLoader
    {
        public UniTask<bool> LoadSingleSceneAsync(string sceneName, CancellationToken cancellationToken = default)
        {
            return RunSceneOperationAsync(
                sceneName,
                startOperation: LoadSingleScene,
                subscribe: action => OnSceneLoadEnd += action,
                unsubscribe: action => OnSceneLoadEnd -= action,
                cancellationToken: cancellationToken);
        }

        public UniTask<bool> LoadAdditiveSceneAsync(string sceneName, CancellationToken cancellationToken = default)
        {
            return RunSceneOperationAsync(
                sceneName,
                startOperation: LoadAdditiveScene,
                subscribe: action => OnSceneLoadEnd += action,
                unsubscribe: action => OnSceneLoadEnd -= action,
                cancellationToken: cancellationToken);
        }

        public UniTask<bool> UnloadAdditiveSceneAsync(string sceneName, CancellationToken cancellationToken = default)
        {
            return RunSceneOperationAsync(
                sceneName,
                startOperation: UnloadAdditiveScene,
                subscribe: action => OnAdditiveSceneUnloadEnd += action,
                unsubscribe: action => OnAdditiveSceneUnloadEnd -= action,
                cancellationToken: cancellationToken);
        }

        private async UniTask<bool> RunSceneOperationAsync(
            string sceneName,
            Action<string> startOperation,
            Action<Action<string>> subscribe,
            Action<Action<string>> unsubscribe,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("Scene operation failed. sceneName is null or empty.");
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            UniTaskCompletionSource<bool> completionSource = new UniTaskCompletionSource<bool>();
            Action<string> onCompleted = null;
            onCompleted = completedSceneName =>
            {
                if (completedSceneName != sceneName)
                {
                    return;
                }

                completionSource.TrySetResult(true);
            };

            subscribe(onCompleted);
            int previousLoadingCount = LoadingCount;

            try
            {
                startOperation(sceneName);
                if (LoadingCount <= previousLoadingCount)
                {
                    return false;
                }

                using (cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken)))
                {
                    return await completionSource.Task;
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                unsubscribe(onCompleted);
            }
        }
    }
}
#endif
