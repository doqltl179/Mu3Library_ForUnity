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

            if (_currentSingleSceneStatusName == key && !TryGetSingleSceneOperation(out _))
            {
                return true;
            }

            if (TryGetSingleSceneOperation(out SceneOperation existingOperation))
            {
                if (existingOperation.SceneName != key)
                {
                    return RejectSceneCommand(key, commandType, SceneCommandRejectReason.Busy);
                }

                if (autoActivate)
                {
                    RequestActivation(existingOperation);
                }

                return true;
            }

            if (IsAdditiveSceneOperationInProgress())
            {
                return RejectSceneCommand(key, commandType, SceneCommandRejectReason.Busy);
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

            if (_currentSingleSceneStatusName == key && !TryGetSingleSceneOperation(out _))
            {
                return true;
            }

            if (!TryGetSingleSceneOperation(out SceneOperation operation) || operation.SceneName != key)
            {
                return RejectSceneCommand(key, SceneCommandType.ActivateSingle, SceneCommandRejectReason.NotPreloaded);
            }

            if (operation.Phase != ScenePhase.Preloaded && operation.Phase != ScenePhase.Activating)
            {
                return RejectSceneCommand(key, SceneCommandType.ActivateSingle, SceneCommandRejectReason.NotPreloaded);
            }

            RequestActivation(operation);
            return true;
        }

        private bool TryPreloadAdditiveSceneWithAddressables(string key, bool autoActivate)
        {
            SceneCommandType commandType = autoActivate ? SceneCommandType.LoadAdditive : SceneCommandType.PreloadAdditive;
            if (string.IsNullOrEmpty(key))
            {
                return RejectSceneCommand(key, commandType, SceneCommandRejectReason.InvalidSceneName);
            }

            if (_currentAdditiveScenes.Contains(key) && !TryGetAdditiveSceneOperation(key, out _))
            {
                return true;
            }

            if (TryGetAdditiveSceneOperation(key, out SceneOperation existingOperation))
            {
                if (existingOperation.IsUnload)
                {
                    return RejectSceneCommand(key, commandType, SceneCommandRejectReason.Busy);
                }

                if (autoActivate)
                {
                    RequestActivation(existingOperation);
                }

                return true;
            }

            if (IsSingleSceneOperationInProgress())
            {
                return RejectSceneCommand(key, commandType, SceneCommandRejectReason.Busy);
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

            if (_currentAdditiveScenes.Contains(key) && !TryGetAdditiveSceneOperation(key, out _))
            {
                return true;
            }

            if (!TryGetAdditiveSceneOperation(key, out SceneOperation operation) || operation.IsUnload)
            {
                return RejectSceneCommand(key, SceneCommandType.ActivateAdditive, SceneCommandRejectReason.NotPreloaded);
            }

            if (operation.Phase != ScenePhase.Preloaded && operation.Phase != ScenePhase.Activating)
            {
                return RejectSceneCommand(key, SceneCommandType.ActivateAdditive, SceneCommandRejectReason.NotPreloaded);
            }

            RequestActivation(operation);
            return true;
        }

        private bool TryStartUnloadAdditiveSceneWithAddressables(string key, bool autoReleaseHandle)
        {
            if (string.IsNullOrEmpty(key))
            {
                return RejectSceneCommand(key, SceneCommandType.UnloadAdditive, SceneCommandRejectReason.InvalidSceneName);
            }

            if (!_currentAdditiveScenes.Contains(key) && !TryGetAdditiveSceneOperation(key, out _))
            {
                return RejectSceneCommand(key, SceneCommandType.UnloadAdditive, SceneCommandRejectReason.NotLoaded);
            }

            if (TryGetAdditiveSceneOperation(key, out SceneOperation existingOperation))
            {
                if (existingOperation.IsUnload)
                {
                    return true;
                }

                return RejectSceneCommand(key, SceneCommandType.UnloadAdditive, SceneCommandRejectReason.Busy);
            }

            if (IsSingleSceneOperationInProgress())
            {
                return RejectSceneCommand(key, SceneCommandType.UnloadAdditive, SceneCommandRejectReason.Busy);
            }

            if (!_loadedAddressableSceneHandles.TryGetValue(key, out AsyncOperationHandle<SceneInstance> handle) || !handle.IsValid())
            {
                return RejectSceneCommand(key, SceneCommandType.UnloadAdditive, SceneCommandRejectReason.NotLoaded);
            }

            Debug.Log($"Addressable additive scene unload start. key: {key}");

            AsyncOperationHandle<SceneInstance> unloadHandle = Addressables.UnloadSceneAsync(handle, autoReleaseHandle);
            SceneOperation operation = CreateAddressablesUnloadOperation(key, unloadHandle, autoReleaseHandle);
            _unloadAdditiveAddressablesSceneOperations.Add(key, operation);
            EmitStatusChanged(operation, force: true);
            return true;
        }

        private SceneOperation CreateAddressablesLoadOperation(string key, AsyncOperationHandle<SceneInstance> handle, bool isAdditive, bool autoActivate)
        {
            SceneOperation operation = new SceneOperation
            {
                SceneName = key,
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

            handle.Completed += completed =>
            {
                operation.HasCompletionResult = true;
                operation.CompletedSuccessfully = completed.Status == AsyncOperationStatus.Succeeded;
            };

            return operation;
        }

        private SceneOperation CreateAddressablesUnloadOperation(string key, AsyncOperationHandle<SceneInstance> handle, bool autoReleaseHandle)
        {
            SceneOperation operation = new SceneOperation
            {
                SceneName = key,
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

            handle.Completed += completed =>
            {
                operation.HasCompletionResult = true;
                operation.CompletedSuccessfully = completed.Status == AsyncOperationStatus.Succeeded;
            };

            return operation;
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
            string previousSceneName = _currentSceneName;
            bool succeeded = IsAddressablesOperationSuccessful(_singleAddressablesSceneOperation);
            Debug.Log($"Addressable single scene operation end. key: {key} succeeded={succeeded}");

            if (succeeded)
            {
                UnityEngine.SceneManagement.Scene scene = _singleAddressablesSceneOperation.AddressablesHandle.Result.Scene;
                _currentSceneName = scene.IsValid() ? scene.name : key;
                _currentSingleSceneStatusName = key;
                _currentAdditiveScenes.Clear();
                OnSingleSceneLoaded?.Invoke(key);
                OnSingleSceneChanged?.Invoke(previousSceneName, _currentSceneName);
            }
            else
            {
                Debug.LogError($"Addressable single scene load failed. key: {key}");
            }

            _singleAddressablesSceneOperation = null;
        }

        private void UpdateLoadAdditiveAddressablesOperations()
        {
            if (_loadAdditiveAddressablesSceneOperations.Count == 0)
            {
                return;
            }

            List<string> completed = null;
            foreach (var pair in _loadAdditiveAddressablesSceneOperations)
            {
                if (!UpdateAddressablesOperation(pair.Value))
                {
                    continue;
                }

                string key = pair.Value.SceneName;
                bool succeeded = IsAddressablesOperationSuccessful(pair.Value);
                Debug.Log($"Addressable additive scene operation end. key: {key} succeeded={succeeded}");

                if (succeeded)
                {
                    _loadedAddressableSceneHandles[key] = pair.Value.AddressablesHandle;
                    _currentAdditiveScenes.Add(key);
                    OnAdditiveSceneLoaded?.Invoke(key);
                }
                else
                {
                    Debug.LogError($"Addressable additive scene load failed. key: {key}");
                }

                completed ??= new List<string>();
                completed.Add(pair.Key);
            }

            if (completed == null)
            {
                return;
            }

            foreach (string key in completed)
            {
                _loadAdditiveAddressablesSceneOperations.Remove(key);
            }
        }

        private void UpdateUnloadAdditiveAddressablesOperations()
        {
            if (_unloadAdditiveAddressablesSceneOperations.Count == 0)
            {
                return;
            }

            List<string> completed = null;
            foreach (var pair in _unloadAdditiveAddressablesSceneOperations)
            {
                if (!UpdateAddressablesOperation(pair.Value))
                {
                    continue;
                }

                bool succeeded = IsAddressablesOperationSuccessful(pair.Value);
                if (succeeded)
                {
                    _currentAdditiveScenes.Remove(pair.Value.SceneName);
                    _loadedAddressableSceneHandles.Remove(pair.Value.SceneName);
                    OnAdditiveSceneUnloaded?.Invoke(pair.Value.SceneName);
                }
                else
                {
                    Debug.LogError($"Addressable additive scene unload failed. key: {pair.Value.SceneName}");
                }

                completed ??= new List<string>();
                completed.Add(pair.Key);
            }

            if (completed == null)
            {
                return;
            }

            foreach (string key in completed)
            {
                _unloadAdditiveAddressablesSceneOperations.Remove(key);
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
    }
}
#endif
