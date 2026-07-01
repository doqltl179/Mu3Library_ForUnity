#if MU3LIBRARY_UNITASK_SUPPORT && MU3LIBRARY_ADDRESSABLES_SUPPORT
using Cysharp.Threading.Tasks;

namespace Mu3Library.Scene
{
    public partial class SceneLoader
    {
        public UniTask<bool> PreloadSingleSceneWithAddressablesAsync(string key)
        {
            return RunSceneCommandAsync(
                key,
                startCommand: name => TryPreloadSingleSceneWithAddressables(name, autoActivate: false),
                isCompleted: IsSingleAddressableScenePreloaded,
                isInProgress: IsSingleSceneOperationInProgressFor);
        }

        public UniTask<bool> ActivateSingleSceneWithAddressablesAsync(string key)
        {
            return RunSceneCommandAsync(
                key,
                startCommand: TryActivateSingleSceneWithAddressables,
                isCompleted: IsSingleAddressableSceneLoaded,
                isInProgress: IsSingleSceneOperationInProgressFor);
        }

        public UniTask<bool> LoadSingleSceneWithAddressablesAsync(string key)
        {
            return RunSceneCommandAsync(
                key,
                startCommand: name => TryPreloadSingleSceneWithAddressables(name, autoActivate: true),
                isCompleted: IsSingleAddressableSceneLoaded,
                isInProgress: IsSingleSceneOperationInProgressFor);
        }

        public UniTask<bool> PreloadAdditiveSceneWithAddressablesAsync(string key)
        {
            return RunSceneCommandAsync(
                key,
                startCommand: name => TryPreloadAdditiveSceneWithAddressables(name, autoActivate: false),
                isCompleted: IsAdditiveScenePreloaded,
                isInProgress: IsAdditiveSceneOperationInProgressFor);
        }

        public UniTask<bool> ActivateAdditiveSceneWithAddressablesAsync(string key)
        {
            return RunSceneCommandAsync(
                key,
                startCommand: TryActivateAdditiveSceneWithAddressables,
                isCompleted: IsAdditiveSceneLoaded,
                isInProgress: IsAdditiveSceneOperationInProgressFor);
        }

        public UniTask<bool> LoadAdditiveSceneWithAddressablesAsync(string key)
        {
            return RunSceneCommandAsync(
                key,
                startCommand: name => TryPreloadAdditiveSceneWithAddressables(name, autoActivate: true),
                isCompleted: IsAdditiveSceneLoaded,
                isInProgress: IsAdditiveSceneOperationInProgressFor);
        }

        public UniTask<bool> UnloadAdditiveSceneWithAddressablesAsync(string key, bool autoReleaseHandle = true)
        {
            return RunSceneCommandAsync(
                key,
                startCommand: name => TryStartUnloadAdditiveSceneWithAddressables(name, autoReleaseHandle),
                isCompleted: IsAdditiveSceneUnloaded,
                isInProgress: IsAdditiveSceneUnloadInProgressFor);
        }

        private bool IsSingleAddressableScenePreloaded(string key)
        {
            if (_currentSingleSceneStatusName == key && !TryGetSingleSceneOperation(out _))
            {
                return true;
            }

            return TryGetSingleSceneOperation(out SceneOperation operation)
                && operation.SceneName == key
                && (operation.Phase == ScenePhase.Preloaded || operation.Phase == ScenePhase.Activating || operation.Phase == ScenePhase.Loaded);
        }

        private bool IsSingleAddressableSceneLoaded(string key)
        {
            if (_currentSingleSceneStatusName == key && !TryGetSingleSceneOperation(out _))
            {
                return true;
            }

            return TryGetSingleSceneOperation(out SceneOperation operation)
                && operation.SceneName == key
                && operation.Phase == ScenePhase.Loaded;
        }
    }
}
#endif
