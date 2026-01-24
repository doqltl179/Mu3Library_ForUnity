using System;
using System.Collections.Generic;
using Mu3Library.DI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mu3Library.Scene
{
    public partial class SceneLoader : IUpdatable, ISceneLoader
    {
        private partial class SceneOperation
        {
            public string SceneName;
            public bool IsAdditive;
            public bool IsUnload;
            public float FakeTimer;
            public float FakeDuration;
            public bool UseFakeLoading;
            public bool ActivationRequested;
            public AsyncOperation Operation;
        }

        private int _loadingCount = 0;
        public bool IsLoading => _loadingCount > 0;

        private string _currentSceneName = "";
        public string CurrentSceneName => _currentSceneName;

        private readonly HashSet<string> _currentAdditiveScenes = new();

        private bool _useFakeLoading = true;
        public bool UseFakeLoading
        {
            get => _useFakeLoading;
            set => _useFakeLoading = value;
        }

        private float _fakeLoadingTime = 1.0f;
        public float FakeLoadingTime
        {
            get => _fakeLoadingTime;
            set => _fakeLoadingTime = value;
        }

        private SceneOperation _singleSceneOperation = null;
        private readonly Dictionary<string, SceneOperation> _loadAdditiveSceneOperations = new();
        private readonly Dictionary<string, SceneOperation> _unloadAdditiveSceneOperations = new();

        public event Action<string> OnSceneLoadStart;
        public event Action<string> OnSceneLoadEnd;
        public event Action<string, float> OnSceneLoadProgress;



        public SceneLoader()
        {
            _currentSceneName = SceneManager.GetActiveScene().name;
        }

        public void Update()
        {
            UpdateSingleSceneOperation();
            UpdateLoadAdditiveOperations();
            UpdateUnloadAdditiveOperations();
#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            UpdateAddressablesOperations();
#endif
        }

        #region Utility
        public bool IsSceneLoadedAsAdditive(string sceneName)
        {
            return _currentAdditiveScenes.Contains(sceneName);
        }

        public void LoadSingleScene(string sceneName)
        {
            if (_currentSceneName == sceneName)
            {
                return;
            }

            if (!IsSceneInBuild(sceneName))
            {
                Debug.LogError($"Scene not found. sceneName: {sceneName}");
                return;
            }

            if (IsSingleSceneOperationInProgress())
            {
                Debug.LogWarning($"Single scene load already in progress. reason=SingleInProgress sceneName: {sceneName}");
                return;
            }

            if (IsAdditiveSceneOperationInProgress())
            {
                Debug.LogWarning($"Cannot load single scene while additive scene is loading/unloading. reason=AdditiveInProgress sceneName: {sceneName}");
                return;
            }

            Debug.Log($"Load single scene start. sceneName: {sceneName}");

            _loadingCount++;
            OnSceneLoadStart?.Invoke(sceneName);

            AsyncOperation ao = SceneManager.LoadSceneAsync(sceneName);
            ao.allowSceneActivation = false;

            _singleSceneOperation = CreateLoadOperation(sceneName, ao, isAdditive: false);
        }

        public void LoadAdditiveScene(string sceneName)
        {
            if (_currentAdditiveScenes.Contains(sceneName))
            {
                return;
            }

            if (!IsSceneInBuild(sceneName))
            {
                Debug.LogError($"Scene not found. sceneName: {sceneName}");
                return;
            }

            if (IsSingleSceneOperationInProgress())
            {
                Debug.LogWarning($"Cannot load additive scene while single scene is loading/unloading. reason=SingleInProgress sceneName: {sceneName}");
                return;
            }

            if (_loadAdditiveSceneOperations.ContainsKey(sceneName))
            {
                Debug.LogWarning($"Additive scene load already in progress. reason=AdditiveInProgress sceneName: {sceneName}");
                return;
            }

            Debug.Log($"Load additive scene start. sceneName: {sceneName}");

            _loadingCount++;
            OnSceneLoadStart?.Invoke(sceneName);

            AsyncOperation ao = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            ao.allowSceneActivation = false;

            _loadAdditiveSceneOperations.Add(sceneName, CreateLoadOperation(sceneName, ao, isAdditive: true));
        }

        public void UnloadAdditiveScene(string sceneName)
        {
            if (!_currentAdditiveScenes.Contains(sceneName))
            {
                Debug.LogWarning($"Scene is not loaded. reason=NotLoaded sceneName: {sceneName}");
                return;
            }

            if (IsSingleSceneOperationInProgress())
            {
                Debug.LogWarning($"Cannot unload additive scene while single scene is loading/unloading. reason=SingleInProgress sceneName: {sceneName}");
                return;
            }

            if (_unloadAdditiveSceneOperations.ContainsKey(sceneName))
            {
                Debug.LogWarning($"Additive scene unload already in progress. reason=AdditiveInProgress sceneName: {sceneName}");
                return;
            }

            AsyncOperation ao = SceneManager.UnloadSceneAsync(sceneName);
            ao.allowSceneActivation = false;

            _unloadAdditiveSceneOperations.Add(sceneName, CreateUnloadOperation(sceneName, ao));
        }
        #endregion

        private bool IsSceneInBuild(string sceneName)
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;

            for (int i = 0; i < sceneCount; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneFileName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                if (sceneName == sceneFileName)
                {
                    return true;
                }
            }

            return false;
        }

        private SceneOperation CreateLoadOperation(string sceneName, AsyncOperation operation, bool isAdditive)
        {
            return new SceneOperation
            {
                SceneName = sceneName,
                Operation = operation,
                IsAdditive = isAdditive,
                IsUnload = false,
                FakeDuration = _fakeLoadingTime,
                UseFakeLoading = _useFakeLoading,
                ActivationRequested = false
            };
        }

        private SceneOperation CreateUnloadOperation(string sceneName, AsyncOperation operation)
        {
            return new SceneOperation
            {
                SceneName = sceneName,
                Operation = operation,
                IsAdditive = true,
                IsUnload = true,
                UseFakeLoading = false,
                ActivationRequested = false
            };
        }

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
        partial void UpdateAddressablesOperations();
#endif

        private void UpdateSingleSceneOperation()
        {
            if (_singleSceneOperation == null)
            {
                return;
            }

            if (!UpdateOperation(_singleSceneOperation))
            {
                return;
            }

            string sceneName = _singleSceneOperation.SceneName;
            Debug.Log($"Load single scene end. sceneName: {sceneName}");

            _currentSceneName = sceneName;
            _currentAdditiveScenes.Clear();

            _loadingCount--;
            _singleSceneOperation = null;

            OnSceneLoadEnd?.Invoke(sceneName);
        }

        private void UpdateLoadAdditiveOperations()
        {
            if (_loadAdditiveSceneOperations.Count == 0)
            {
                return;
            }

            List<string> completed = null;
            foreach (var pair in _loadAdditiveSceneOperations)
            {
                if (!UpdateOperation(pair.Value))
                {
                    continue;
                }

                string sceneName = pair.Value.SceneName;
                Debug.Log($"Load additive scene end. sceneName: {sceneName}");

                _currentAdditiveScenes.Add(sceneName);
                _loadingCount--;

                completed ??= new List<string>();
                completed.Add(pair.Key);
            }

            if (completed == null)
            {
                return;
            }

            foreach (string sceneName in completed)
            {
                _loadAdditiveSceneOperations.Remove(sceneName);

                OnSceneLoadEnd?.Invoke(sceneName);
            }
        }

        private void UpdateUnloadAdditiveOperations()
        {
            if (_unloadAdditiveSceneOperations.Count == 0)
            {
                return;
            }

            List<string> completed = null;
            foreach (var pair in _unloadAdditiveSceneOperations)
            {
                if (!UpdateOperation(pair.Value))
                {
                    continue;
                }

                _currentAdditiveScenes.Remove(pair.Value.SceneName);

                completed ??= new List<string>();
                completed.Add(pair.Key);
            }

            if (completed == null)
            {
                return;
            }

            foreach (string sceneName in completed)
            {
                _unloadAdditiveSceneOperations.Remove(sceneName);
            }
        }

        private bool UpdateOperation(SceneOperation operation)
        {
            OnSceneLoadProgress?.Invoke(operation.SceneName, GetOperationProgress(operation));

            if (operation.IsUnload)
            {
                if (operation.Operation.progress < 0.9f)
                {
                    return false;
                }

                if (!operation.ActivationRequested)
                {
                    operation.Operation.allowSceneActivation = true;
                    operation.ActivationRequested = true;
                }

                return operation.Operation.isDone;
            }

            if (operation.Operation.progress < 0.9f)
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
                operation.Operation.allowSceneActivation = true;
                operation.ActivationRequested = true;
            }

            return operation.Operation.isDone;
        }

        private float GetOperationProgress(SceneOperation operation)
        {
            float progress = operation.Operation.progress;
            if (operation.IsUnload)
            {
                return Mathf.Clamp01(progress);
            }

            float normalized = Mathf.Clamp01(progress / 0.9f);
            if (!operation.UseFakeLoading || operation.FakeDuration <= 0f)
            {
                return normalized;
            }

            if (normalized < 1f)
            {
                return normalized * 0.9f;
            }

            float fakeProgress = Mathf.Clamp01(operation.FakeTimer / operation.FakeDuration);
            return 0.9f + (fakeProgress * 0.1f);
        }

        private bool IsSingleSceneOperationInProgress()
        {
#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            return _singleSceneOperation != null || _singleAddressablesSceneOperation != null;
#else
            return _singleSceneOperation != null;
#endif
        }

        private bool IsAdditiveSceneOperationInProgress()
        {
            bool inProgress = _loadAdditiveSceneOperations.Count > 0 || _unloadAdditiveSceneOperations.Count > 0;
#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            inProgress |= _loadAdditiveAddressablesSceneOperations.Count > 0 || _unloadAdditiveAddressablesSceneOperations.Count > 0;
#endif
            return inProgress;
        }
    }
}
