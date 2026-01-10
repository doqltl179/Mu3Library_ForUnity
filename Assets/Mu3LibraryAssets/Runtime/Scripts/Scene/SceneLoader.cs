using System;
using System.Collections.Generic;
using Mu3Library.DI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mu3Library.Scene
{
    public partial class SceneLoader : IUpdatable, ISceneLoader
    {
        private class SceneOperation
        {
            public string SceneName;
            public AsyncOperation Operation;
            public bool IsAdditive;
            public bool IsUnload;
            public float FakeTimer;
            public float FakeDuration;
            public bool UseFakeLoading;
            public bool ActivationRequested;
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

        public event Action<string> OnSceneLoadStart;
        public event Action<string> OnSceneLoadEnd;

        private SceneOperation _singleSceneOperation = null;
        private readonly Dictionary<string, SceneOperation> _loadAdditiveSceneOperations = new();
        private readonly Dictionary<string, SceneOperation> _unloadAdditiveSceneOperations = new();



        public SceneLoader()
        {
            _currentSceneName = SceneManager.GetActiveScene().name;
        }

        #region Utility

        public bool IsSceneLoadedAsAdditive(string sceneName)
        {
            return _currentAdditiveScenes.Contains(sceneName);
        }

        #endregion

        #region Load

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

            if (_singleSceneOperation != null)
            {
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

            if (_loadAdditiveSceneOperations.ContainsKey(sceneName))
            {
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
                Debug.LogWarning($"Scene is not loaded. sceneName: {sceneName}");
                return;
            }

            if (_unloadAdditiveSceneOperations.ContainsKey(sceneName))
            {
                return;
            }

            AsyncOperation ao = SceneManager.UnloadSceneAsync(sceneName);
            ao.allowSceneActivation = false;

            _unloadAdditiveSceneOperations.Add(sceneName, CreateUnloadOperation(sceneName, ao));
        }

        #endregion

        public void Update()
        {
            UpdateSingleSceneOperation();
            UpdateLoadAdditiveOperations();
            UpdateUnloadAdditiveOperations();
        }

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
            OnSceneLoadEnd?.Invoke(sceneName);

            _singleSceneOperation = null;
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
                OnSceneLoadEnd?.Invoke(sceneName);

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
    }
}
