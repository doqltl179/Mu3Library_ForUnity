#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Mu3Library.Scene
{
    public partial class SceneLoader
    {
        private partial class SceneOperation
        {
            public bool IsAddressables;
            public AsyncOperationHandle<SceneInstance> AddressablesHandle;
            public AsyncOperation ActivationOperation;
            public bool AutoReleaseHandle;
        }

        private SceneOperation _singleAddressablesSceneOperation = null;
        private readonly Dictionary<string, SceneOperation> _loadAdditiveAddressablesSceneOperations = new();
        private readonly Dictionary<string, SceneOperation> _unloadAdditiveAddressablesSceneOperations = new();
        private readonly Dictionary<string, AsyncOperationHandle<SceneInstance>> _loadedAddressableSceneHandles = new();



        public void PreloadSingleSceneWithAddressables(string key)
        {
            TryPreloadSingleSceneWithAddressables(key, autoActivate: false);
        }

        public void ActivateSingleSceneWithAddressables(string key)
        {
            TryActivateSingleSceneWithAddressables(key);
        }

        public void LoadSingleSceneWithAddressables(string key)
        {
            TryPreloadSingleSceneWithAddressables(key, autoActivate: true);
        }

        public void PreloadAdditiveSceneWithAddressables(string key)
        {
            TryPreloadAdditiveSceneWithAddressables(key, autoActivate: false);
        }

        public void ActivateAdditiveSceneWithAddressables(string key)
        {
            TryActivateAdditiveSceneWithAddressables(key);
        }

        public void LoadAdditiveSceneWithAddressables(string key)
        {
            TryPreloadAdditiveSceneWithAddressables(key, autoActivate: true);
        }

        public void UnloadAdditiveSceneWithAddressables(string key, bool autoReleaseHandle = true)
        {
            TryStartUnloadAdditiveSceneWithAddressables(key, autoReleaseHandle);
        }

        private bool TryPreloadSingleSceneWithAddressables(string key, bool autoActivate)
        {
            SceneCommandType commandType = autoActivate ? SceneCommandType.LoadSingle : SceneCommandType.PreloadSingle;
            if (string.IsNullOrEmpty(key))
            {
                return RejectSceneCommand(key, commandType, SceneCommandRejectReason.InvalidSceneName);
            }

            if (IsSingleSceneAlreadyCurrent(key, _currentSingleSceneStatusName))
            {
                return true;
            }

            SceneCommandGate gate = GuardSingleScenePreload(key, commandType, autoActivate);
            if (gate.IsSettled)
            {
                return gate.Result;
            }

            Debug.Log($"Addressable single scene preload start. key: {key}");

            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Single, activateOnLoad: false);
            _singleAddressablesSceneOperation = CreateAddressablesLoadOperation(key, handle, isAdditive: false, autoActivate: autoActivate);
            EmitStatusChanged(_singleAddressablesSceneOperation, force: true);
            return true;
        }

        private bool TryActivateSingleSceneWithAddressables(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return RejectSceneCommand(key, SceneCommandType.ActivateSingle, SceneCommandRejectReason.InvalidSceneName);
            }

            return ActivatePreloadedSingleScene(key, _currentSingleSceneStatusName);
        }

        private bool TryPreloadAdditiveSceneWithAddressables(string key, bool autoActivate)
        {
            SceneCommandType commandType = autoActivate ? SceneCommandType.LoadAdditive : SceneCommandType.PreloadAdditive;
            if (string.IsNullOrEmpty(key))
            {
                return RejectSceneCommand(key, commandType, SceneCommandRejectReason.InvalidSceneName);
            }

            if (IsAdditiveSceneAlreadyLoaded(key))
            {
                return true;
            }

            SceneCommandGate gate = GuardAdditiveScenePreload(key, commandType, autoActivate);
            if (gate.IsSettled)
            {
                return gate.Result;
            }

            Debug.Log($"Addressable additive scene preload start. key: {key}");

            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive, activateOnLoad: false);
            SceneOperation operation = CreateAddressablesLoadOperation(key, handle, isAdditive: true, autoActivate: autoActivate);
            _loadAdditiveAddressablesSceneOperations.Add(key, operation);
            EmitStatusChanged(operation, force: true);
            return true;
        }

        private bool TryActivateAdditiveSceneWithAddressables(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return RejectSceneCommand(key, SceneCommandType.ActivateAdditive, SceneCommandRejectReason.InvalidSceneName);
            }

            return ActivatePreloadedAdditiveScene(key);
        }

        private bool TryStartUnloadAdditiveSceneWithAddressables(string key, bool autoReleaseHandle)
        {
            if (string.IsNullOrEmpty(key))
            {
                return RejectSceneCommand(key, SceneCommandType.UnloadAdditive, SceneCommandRejectReason.InvalidSceneName);
            }

            SceneCommandGate gate = GuardAdditiveSceneUnload(key);
            if (gate.IsSettled)
            {
                return gate.Result;
            }

            if (!_loadedAddressableSceneHandles.TryGetValue(key, out AsyncOperationHandle<SceneInstance> handle) || !handle.IsValid())
            {
                return RejectSceneCommand(key, SceneCommandType.UnloadAdditive, SceneCommandRejectReason.NotLoaded);
            }

            Debug.Log($"Addressable additive scene unload start. key: {key}");

            AsyncOperationHandle<SceneInstance> unloadHandle = Addressables.UnloadSceneAsync(handle, autoReleaseHandle);
            SceneOperation operation = CreateAddressablesUnloadOperation(key, unloadHandle, handle, autoReleaseHandle);
            operation.SceneHandle = handle.Result.Scene.handle;
            _unloadAdditiveAddressablesSceneOperations.Add(key, operation);
            EmitStatusChanged(operation, force: true);
            return true;
        }

        private SceneOperation CreateAddressablesLoadOperation(string key, AsyncOperationHandle<SceneInstance> handle, bool isAdditive, bool autoActivate)
        {
            SceneOperation operation = new SceneOperation
            {
                SceneName = key,
                ResolvedSceneName = string.Empty,
                IsAddressables = true,
                AddressablesHandle = handle,
                ActivationOperation = null,
                IsAdditive = isAdditive,
                IsUnload = false,
                AutoActivate = autoActivate,
                ActivationRequested = false,
                Phase = ScenePhase.Preloading,
                Progress = 0f,
                LastReportedProgress = -1f,
                AutoReleaseHandle = false,
            };

            TrackAddressablesCompletion(handle, operation);

            return operation;
        }

        private SceneOperation CreateAddressablesUnloadOperation(
            string key,
            AsyncOperationHandle<SceneInstance> handle,
            AsyncOperationHandle<SceneInstance> loadedHandle,
            bool autoReleaseHandle)
        {
            SceneOperation operation = new SceneOperation
            {
                SceneName = key,
                ResolvedSceneName = ResolveAddressablesSceneName(loadedHandle),
                IsAddressables = true,
                AddressablesHandle = handle,
                ActivationOperation = null,
                IsAdditive = true,
                IsUnload = true,
                AutoActivate = false,
                ActivationRequested = false,
                Phase = ScenePhase.Unloading,
                Progress = 0f,
                LastReportedProgress = -1f,
                AutoReleaseHandle = autoReleaseHandle,
            };

            TrackAddressablesCompletion(handle, operation);

            return operation;
        }

        private static void TrackAddressablesCompletion(AsyncOperationHandle<SceneInstance> handle, SceneOperation operation)
        {
            handle.Completed += completed =>
            {
                operation.HasCompletionResult = true;
                operation.CompletedSuccessfully = completed.Status == AsyncOperationStatus.Succeeded;
            };
        }

        partial void UpdateAddressablesOperations()
        {
            UpdateSingleAddressablesSceneOperation();
            UpdateLoadAdditiveAddressablesOperations();
            UpdateUnloadAdditiveAddressablesOperations();
        }

        private void UpdateSingleAddressablesSceneOperation()
        {
            if (_singleAddressablesSceneOperation == null)
            {
                return;
            }

            if (!UpdateAddressablesOperation(_singleAddressablesSceneOperation))
            {
                return;
            }

            string key = _singleAddressablesSceneOperation.SceneName;
            bool succeeded = IsAddressablesOperationSuccessful(_singleAddressablesSceneOperation);
            Debug.Log($"Addressable single scene operation end. key: {key} succeeded={succeeded}");

            if (succeeded)
            {
                FinalizeSingleSceneLoaded(
                    _singleAddressablesSceneOperation,
                    ResolveLoadedSceneName(_singleAddressablesSceneOperation),
                    ResolveLoadedSceneHandle(_singleAddressablesSceneOperation));
            }
            else
            {
                Debug.LogError($"Addressable single scene load failed. key: {key}");
                ReleaseFailedLoadHandle(_singleAddressablesSceneOperation);
            }

            _singleAddressablesSceneOperation = null;
        }

        /// <summary>
        /// A load that failed leaves no scene behind, so nothing else will ever release its
        /// handle. It is released here, otherwise the failed operation stays referenced forever.
        /// </summary>
        private static void ReleaseFailedLoadHandle(SceneOperation operation)
        {
            if (operation != null && operation.AddressablesHandle.IsValid())
            {
                Addressables.Release(operation.AddressablesHandle);
            }
        }

        private void UpdateLoadAdditiveAddressablesOperations()
        {
            if (_loadAdditiveAddressablesSceneOperations.Count == 0)
            {
                return;
            }

            // Finalizing fires user callbacks that may start or stop other scene operations,
            // so the pass iterates a snapshot instead of the live dictionary.
            List<SceneOperation> operations = new(_loadAdditiveAddressablesSceneOperations.Values);
            foreach (SceneOperation operation in operations)
            {
                if (!UpdateAddressablesOperation(operation))
                {
                    continue;
                }

                string key = operation.SceneName;
                bool succeeded = IsAddressablesOperationSuccessful(operation);
                Debug.Log($"Addressable additive scene operation end. key: {key} succeeded={succeeded}");

                if (succeeded)
                {
                    FinalizeAdditiveSceneLoaded(operation, ResolveLoadedSceneName(operation), ResolveLoadedSceneHandle(operation));
                }
                else
                {
                    Debug.LogError($"Addressable additive scene load failed. key: {key}");
                    ReleaseFailedLoadHandle(operation);
                }

                RemoveTrackedOperation(_loadAdditiveAddressablesSceneOperations, operation);
            }
        }

        private void UpdateUnloadAdditiveAddressablesOperations()
        {
            if (_unloadAdditiveAddressablesSceneOperations.Count == 0)
            {
                return;
            }

            // Snapshot for the same reason as the additive load pass above.
            List<SceneOperation> operations = new(_unloadAdditiveAddressablesSceneOperations.Values);
            foreach (SceneOperation operation in operations)
            {
                if (!UpdateAddressablesOperation(operation))
                {
                    continue;
                }

                bool succeeded = IsAddressablesOperationSuccessful(operation);
                if (succeeded)
                {
                    FinalizeAdditiveSceneUnloaded(operation);
                }
                else
                {
                    Debug.LogError($"Addressable additive scene unload failed. key: {operation.SceneName}");
                }

                RemoveTrackedOperation(_unloadAdditiveAddressablesSceneOperations, operation);
            }
        }

        private bool UpdateAddressablesOperation(SceneOperation operation)
        {
            if (!operation.AddressablesHandle.IsValid())
            {
                return operation.HasCompletionResult;
            }

            if (operation.AddressablesHandle.Status == AsyncOperationStatus.Failed)
            {
                operation.HasCompletionResult = true;
                operation.CompletedSuccessfully = false;
                return true;
            }

            if (operation.HasUnitySceneEvent)
            {
                if (operation.IsUnload)
                {
                    return operation.AddressablesHandle.IsDone;
                }

                return operation.ActivationOperation == null || operation.ActivationOperation.isDone;
            }

            if (operation.IsUnload)
            {
                UpdateSceneOperationStatus(operation, ScenePhase.Unloading, Mathf.Clamp01(operation.AddressablesHandle.PercentComplete));
                return operation.AddressablesHandle.IsDone;
            }

            float progress = Mathf.Clamp01(operation.AddressablesHandle.PercentComplete);
            if (progress < 1.0f)
            {
                UpdateSceneOperationStatus(operation, ScenePhase.Preloading, progress);
                return false;
            }

            TryPopulateResolvedSceneName(operation);

            if (!operation.AutoActivate)
            {
                UpdateSceneOperationStatus(operation, ScenePhase.Preloaded, 1.0f);
                return false;
            }

            if (!operation.ActivationRequested)
            {
                UpdateSceneOperationStatus(operation, ScenePhase.Preloaded, 1.0f);
                BeginActivation(operation);
            }
            else
            {
                UpdateSceneOperationStatus(operation, ScenePhase.Activating, 1.0f);
            }

            return operation.ActivationOperation == null || operation.ActivationOperation.isDone;
        }

        private static bool IsAddressablesOperationSuccessful(SceneOperation operation)
        {
            if (operation.HasCompletionResult)
            {
                return operation.CompletedSuccessfully;
            }

            return operation.AddressablesHandle.IsValid() && operation.AddressablesHandle.Status == AsyncOperationStatus.Succeeded;
        }

        private void TryPopulateResolvedSceneName(SceneOperation operation)
        {
            if (operation == null || !string.IsNullOrEmpty(operation.ResolvedSceneName))
            {
                return;
            }

            string resolvedSceneName = ResolveAddressablesSceneName(operation.AddressablesHandle);
            if (!string.IsNullOrEmpty(resolvedSceneName))
            {
                SetResolvedSceneName(operation, resolvedSceneName);
            }
        }

        private static string ResolveAddressablesSceneName(AsyncOperationHandle<SceneInstance> handle)
        {
            if (!handle.IsValid() || handle.Status != AsyncOperationStatus.Succeeded)
            {
                return string.Empty;
            }

            UnityEngine.SceneManagement.Scene scene = handle.Result.Scene;
            return scene.IsValid() ? scene.name : string.Empty;
        }
    }
}
#endif
