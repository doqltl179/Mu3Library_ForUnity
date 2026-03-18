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
            public bool AutoReleaseHandle;
        }

        private SceneOperation _singleAddressablesSceneOperation = null;
        private readonly Dictionary<string, SceneOperation> _loadAdditiveAddressablesSceneOperations = new();
        private readonly Dictionary<string, SceneOperation> _unloadAdditiveAddressablesSceneOperations = new();
        private readonly Dictionary<string, AsyncOperationHandle<SceneInstance>> _loadedAddressableSceneHandles = new();



        public void LoadSingleSceneWithAddressables(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (IsSingleSceneOperationInProgress())
            {
                Debug.LogWarning($"Single scene load already in progress. reason=SingleInProgress key: {key}");
                return;
            }

            if (IsAdditiveSceneOperationInProgress())
            {
                Debug.LogWarning($"Cannot load single scene while additive scene is loading/unloading. reason=AdditiveInProgress key: {key}");
                return;
            }

            if (_currentSceneName == key)
            {
                return;
            }

            Debug.Log($"Load addressable single scene start. key: {key}");

            _loadingCount++;
            OnSceneLoadStart?.Invoke(key);

            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Single, activateOnLoad: false);
            _singleAddressablesSceneOperation = CreateAddressablesLoadOperation(key, handle, isAdditive: false);
        }

        public void LoadAdditiveSceneWithAddressables(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (_currentAdditiveScenes.Contains(key))
            {
                return;
            }

            if (IsSingleSceneOperationInProgress())
            {
                Debug.LogWarning($"Cannot load additive scene while single scene is loading/unloading. reason=SingleInProgress key: {key}");
                return;
            }

            if (_loadAdditiveAddressablesSceneOperations.ContainsKey(key))
            {
                Debug.LogWarning($"Additive scene load already in progress. reason=AdditiveInProgress key: {key}");
                return;
            }

            Debug.Log($"Load addressable additive scene start. key: {key}");

            _loadingCount++;
            OnSceneLoadStart?.Invoke(key);

            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive, activateOnLoad: false);
            _loadAdditiveAddressablesSceneOperations.Add(key, CreateAddressablesLoadOperation(key, handle, isAdditive: true));
        }

        public void UnloadAdditiveSceneWithAddressables(string key, bool autoReleaseHandle = true)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (!_currentAdditiveScenes.Contains(key))
            {
                Debug.LogWarning($"Scene is not loaded. reason=NotLoaded key: {key}");
                return;
            }

            if (IsSingleSceneOperationInProgress())
            {
                Debug.LogWarning($"Cannot unload additive scene while single scene is loading/unloading. reason=SingleInProgress key: {key}");
                return;
            }

            if (_unloadAdditiveAddressablesSceneOperations.ContainsKey(key))
            {
                Debug.LogWarning($"Additive scene unload already in progress. reason=AdditiveInProgress key: {key}");
                return;
            }

            if (!_loadedAddressableSceneHandles.TryGetValue(key, out AsyncOperationHandle<SceneInstance> handle) || !handle.IsValid())
            {
                Debug.LogWarning($"Addressable scene handle not found. reason=HandleNotFound key: {key}");
                return;
            }

            AsyncOperationHandle<SceneInstance> unloadHandle = Addressables.UnloadSceneAsync(handle, autoReleaseHandle);
            _unloadAdditiveAddressablesSceneOperations.Add(key, CreateAddressablesUnloadOperation(key, unloadHandle, autoReleaseHandle));
        }

        private SceneOperation CreateAddressablesLoadOperation(string key, AsyncOperationHandle<SceneInstance> handle, bool isAdditive)
        {
            return new SceneOperation
            {
                SceneName = key,
                IsAddressables = true,
                AddressablesHandle = handle,
                IsAdditive = isAdditive,
                IsUnload = false,
                FakeDuration = _fakeLoadingTime,
                UseFakeLoading = _useFakeLoading,
                ActivationRequested = false,
                AutoReleaseHandle = false,
            };
        }

        private SceneOperation CreateAddressablesUnloadOperation(string key, AsyncOperationHandle<SceneInstance> handle, bool autoReleaseHandle)
        {
            return new SceneOperation
            {
                SceneName = key,
                IsAddressables = true,
                AddressablesHandle = handle,
                IsAdditive = true,
                IsUnload = true,
                UseFakeLoading = false,
                ActivationRequested = false,
                AutoReleaseHandle = autoReleaseHandle,
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
            Debug.Log($"Load addressable single scene end. key: {key}");

            if (_singleAddressablesSceneOperation.AddressablesHandle.IsValid() &&
                _singleAddressablesSceneOperation.AddressablesHandle.Status == AsyncOperationStatus.Succeeded)
            {
                UnityEngine.SceneManagement.Scene scene = _singleAddressablesSceneOperation.AddressablesHandle.Result.Scene;
                _currentSceneName = scene.IsValid() ? scene.name : key;
            }
            else
            {
                _currentSceneName = key;
            }

            _currentAdditiveScenes.Clear();

            _loadingCount--;
            _singleAddressablesSceneOperation = null;

            OnSceneLoadEnd?.Invoke(key);
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
                Debug.Log($"Load addressable additive scene end. key: {key}");

                if (pair.Value.AddressablesHandle.IsValid() && pair.Value.AddressablesHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    _loadedAddressableSceneHandles[key] = pair.Value.AddressablesHandle;
                }

                _currentAdditiveScenes.Add(key);
                _loadingCount--;

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

                OnSceneLoadEnd?.Invoke(key);
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

                _currentAdditiveScenes.Remove(pair.Value.SceneName);
                _loadedAddressableSceneHandles.Remove(pair.Value.SceneName);

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
                return true;
            }

            OnSceneLoadProgress?.Invoke(operation.SceneName, GetAddressablesOperationProgress(operation));

            if (operation.AddressablesHandle.Status == AsyncOperationStatus.Failed)
            {
                return true;
            }

            if (operation.IsUnload)
            {
                return operation.AddressablesHandle.IsDone;
            }

            if (operation.AddressablesHandle.PercentComplete < 1.0f)
            {
                return false;
            }

            if (operation.UseFakeLoading && operation.FakeDuration > 0f && operation.FakeTimer < operation.FakeDuration)
            {
                operation.FakeTimer += Time.deltaTime;
                return false;
            }

            if (!operation.ActivationRequested)
            {
                if (operation.AddressablesHandle.IsValid() && operation.AddressablesHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    AsyncOperation activation = operation.AddressablesHandle.Result.ActivateAsync();
                    if (activation != null && !activation.isDone)
                    {
                        operation.ActivationRequested = true;
                        return false;
                    }
                }

                operation.ActivationRequested = true;
            }

            return operation.AddressablesHandle.IsDone;
        }

        private float GetAddressablesOperationProgress(SceneOperation operation)
        {
            if (!operation.AddressablesHandle.IsValid())
            {
                return operation.IsUnload ? 1.0f : 0.0f;
            }

            float progress = Mathf.Clamp01(operation.AddressablesHandle.PercentComplete);
            if (operation.IsUnload)
            {
                return progress;
            }

            if (!operation.UseFakeLoading || operation.FakeDuration <= 0f)
            {
                return progress;
            }

            if (progress < 1f)
            {
                return progress * 0.9f;
            }

            float fakeProgress = Mathf.Clamp01(operation.FakeTimer / operation.FakeDuration);
            return 0.9f + (fakeProgress * 0.1f);
        }
    }
}
#endif
