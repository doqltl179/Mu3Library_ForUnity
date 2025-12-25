using System.Collections;
using Mu3Library.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;
using Mu3Library.Observable;



#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Mu3Library.Scene
{
    public class SceneLoader : GenericSingleton<SceneLoader>
    {
        private int _loadingCount = 0;
        public bool IsLoading => _loadingCount > 0;

        private string _currentSceneName = "";
        public string CurrentSceneName => _currentSceneName;

        private HashSet<string> _currentAdditiveScenes = new();

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

        private readonly ObservableFloat _sceneLoadProgress = new();
        public float SceneLoadProgress => _sceneLoadProgress;

        public event Action<string> OnSceneLoadStart;
        public event Action<string> OnSceneLoadEnd;

        private IEnumerator _loadSingleSceneCoroutine = null;
        /// <summary>
        /// <br/> string: scene name
        /// <br/> IEnumerator: scene load process coroutine
        /// </summary>
        private Dictionary<string, IEnumerator> _loadAdditiveSceneCoroutines = new();
        /// <summary>
        /// <br/> string: scene name
        /// <br/> IEnumerator: scene unload process coroutine
        /// </summary>
        private Dictionary<string, IEnumerator> _unloadAdditiveSceneCoroutines = new();



        private void Awake()
        {
            _currentSceneName = SceneManager.GetActiveScene().name;
        }

        #region Utility

        public bool IsSceneLoadedAsAdditive(string sceneName)
        {
            return _currentAdditiveScenes.Contains(sceneName);
        }

#if UNITY_EDITOR
        public bool IsSceneLoadedAsAdditiveWithAssetPath(string assetPath)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            return _currentAdditiveScenes.Contains(sceneName);
        }
#endif

        public void RemoveProgressEvent(Action<float> callback)
        {
            _sceneLoadProgress.RemoveEvent(callback);
        }

        public void AddProgressEvent(Action<float> callback)
        {
            _sceneLoadProgress.AddEvent(callback);
        }

        #endregion

        #region Coroutine

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

            if (_loadSingleSceneCoroutine == null)
            {
                _loadSingleSceneCoroutine = LoadSingleSceneCoroutine(sceneName);
                StartCoroutine(_loadSingleSceneCoroutine);
            }
        }

        private IEnumerator LoadSingleSceneCoroutine(string sceneName)
        {
            _loadingCount++;

            OnSceneLoadStart?.Invoke(sceneName);

            AsyncOperation ao = SceneManager.LoadSceneAsync(sceneName);
            ao.allowSceneActivation = false;

            yield return StartCoroutine(LoadProcessCoroutine(ao));

            ao.allowSceneActivation = true;
            // Wait other process
            while (!ao.isDone)
            {
                yield return null;
            }

            _currentSceneName = sceneName;
            _currentAdditiveScenes.Clear();

            _loadingCount--;
            _loadSingleSceneCoroutine = null;

            OnSceneLoadEnd?.Invoke(sceneName);
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

            if (!_loadAdditiveSceneCoroutines.ContainsKey(sceneName))
            {
                IEnumerator process = LoadAdditiveSceneCoroutine(sceneName);
                _loadAdditiveSceneCoroutines.Add(sceneName, process);
                StartCoroutine(process);
            }
        }

        private IEnumerator LoadAdditiveSceneCoroutine(string sceneName)
        {
            _loadingCount++;

            OnSceneLoadStart?.Invoke(sceneName);

            AsyncOperation ao = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            ao.allowSceneActivation = false;

            yield return StartCoroutine(LoadProcessCoroutine(ao));

            ao.allowSceneActivation = true;
            // Wait other process
            while (!ao.isDone)
            {
                yield return null;
            }

            _currentAdditiveScenes.Add(sceneName);

            _loadingCount--;
            _loadAdditiveSceneCoroutines.Remove(sceneName);

            OnSceneLoadEnd?.Invoke(sceneName);
        }

        public void UnloadAdditiveScene(string sceneName)
        {
            if (!_currentAdditiveScenes.Contains(sceneName))
            {
                Debug.LogWarning($"Scene is not loaded. sceneName: {sceneName}");
                return;
            }

            if (!_unloadAdditiveSceneCoroutines.ContainsKey(sceneName))
            {
                IEnumerator process = UnloadAdditiveSceneCoroutine(sceneName);
                _unloadAdditiveSceneCoroutines.Add(sceneName, process);
                StartCoroutine(process);
            }
        }

        private IEnumerator UnloadAdditiveSceneCoroutine(string sceneName)
        {
            AsyncOperation ao = SceneManager.UnloadSceneAsync(sceneName);
            ao.allowSceneActivation = false;

            yield return StartCoroutine(UnloadProcessCoroutine(ao));

            ao.allowSceneActivation = true;
            // Wait other process
            while (!ao.isDone)
            {
                yield return null;
            }

            _currentAdditiveScenes.Remove(sceneName);

            _unloadAdditiveSceneCoroutines.Remove(sceneName);
        }

#if UNITY_EDITOR
        public void LoadSingleSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (_currentSceneName == sceneName)
            {
                return;
            }

            SceneAsset sceneAsset = LoadSceneAsset(assetPath);
            if (sceneAsset == null)
            {
                Debug.LogError($"SceneAsset not found. assetPath: {assetPath}");
                return;
            }

            if (_loadSingleSceneCoroutine == null)
            {
                _loadSingleSceneCoroutine = LoadSingleSceneCoroutineWithAssetPath(assetPath, physicsMode);
                StartCoroutine(_loadSingleSceneCoroutine);
            }
        }

        private IEnumerator LoadSingleSceneCoroutineWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            _loadingCount++;
            OnSceneLoadStart?.Invoke(sceneName);

            LoadSceneParameters parameters = new LoadSceneParameters(LoadSceneMode.Single, physicsMode);
            AsyncOperation ao = EditorSceneManager.LoadSceneAsyncInPlayMode(assetPath, parameters);
            ao.allowSceneActivation = false;

            yield return StartCoroutine(LoadProcessCoroutine(ao));

            _loadingCount--;

            ao.allowSceneActivation = true;
            // Wait other process
            while (!ao.isDone)
            {
                yield return null;
            }

            _currentSceneName = sceneName;
            _currentAdditiveScenes.Clear();

            _loadSingleSceneCoroutine = null;

            OnSceneLoadEnd?.Invoke(sceneName);
        }

        public void LoadAdditiveSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (_currentAdditiveScenes.Contains(sceneName))
            {
                return;
            }

            SceneAsset sceneAsset = LoadSceneAsset(assetPath);
            if (sceneAsset == null)
            {
                Debug.LogError($"SceneAsset not found. assetPath: {assetPath}");
                return;
            }

            if (!_loadAdditiveSceneCoroutines.ContainsKey(sceneName))
            {
                IEnumerator process = LoadAdditiveSceneCoroutineWithAssetPath(assetPath, physicsMode);
                _loadAdditiveSceneCoroutines.Add(sceneName, process);
                StartCoroutine(process);
            }
        }

        private IEnumerator LoadAdditiveSceneCoroutineWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            _loadingCount++;
            OnSceneLoadStart?.Invoke(sceneName);

            LoadSceneParameters parameters = new LoadSceneParameters(LoadSceneMode.Additive, physicsMode);
            AsyncOperation ao = EditorSceneManager.LoadSceneAsyncInPlayMode(assetPath, parameters);
            ao.allowSceneActivation = false;

            yield return StartCoroutine(LoadProcessCoroutine(ao));

            _loadingCount--;

            ao.allowSceneActivation = true;
            // Wait other process
            while (!ao.isDone)
            {
                yield return null;
            }

            _currentAdditiveScenes.Add(sceneName);

            _loadAdditiveSceneCoroutines.Remove(sceneName);

            OnSceneLoadEnd?.Invoke(sceneName);
        }

        public void UnloadAdditiveSceneWithAssetPath(string assetPath)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            if (!_currentAdditiveScenes.Contains(sceneName))
            {
                Debug.LogWarning($"Scene is not loaded. sceneName: {sceneName}");
                return;
            }

            if (!_unloadAdditiveSceneCoroutines.ContainsKey(sceneName))
            {
                IEnumerator process = UnloadAdditiveSceneCoroutineWithAssetPath(assetPath);
                _unloadAdditiveSceneCoroutines.Add(sceneName, process);
                StartCoroutine(process);
            }
        }

        private IEnumerator UnloadAdditiveSceneCoroutineWithAssetPath(string assetPath)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            AsyncOperation ao = EditorSceneManager.UnloadSceneAsync(sceneName);
            ao.allowSceneActivation = false;

            yield return StartCoroutine(UnloadProcessCoroutine(ao));

            ao.allowSceneActivation = true;
            // Wait other process
            while (!ao.isDone)
            {
                yield return null;
            }

            _currentAdditiveScenes.Remove(sceneName);

            _unloadAdditiveSceneCoroutines.Remove(sceneName);
        }
#endif

        private IEnumerator LoadProcessCoroutine(AsyncOperation ao)
        {
            _sceneLoadProgress.Set(0f);

            // Wait Scene Loading
            while (ao.progress < 0.9f)
            {
                _sceneLoadProgress.Set(ao.progress);
                yield return null;
            }

            // Fake Loading
            if (_useFakeLoading && _fakeLoadingTime > 0)
            {
                float timer = 0.0f;
                while (timer < _fakeLoadingTime)
                {
                    timer += Time.deltaTime;
                    _sceneLoadProgress.Set(Mathf.Lerp(0.9f, 1.0f, timer / _fakeLoadingTime));
                    yield return null;
                }
            }

            _sceneLoadProgress.Set(1.0f);
        }

        private IEnumerator UnloadProcessCoroutine(AsyncOperation ao)
        {
            // Wait Scene Unloading
            while (ao.progress < 0.9f)
            {
                yield return null;
            }
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

#if UNITY_EDITOR
        private SceneAsset LoadSceneAsset(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) ?
                AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPath) :
                null;
        }
#endif
    }
}