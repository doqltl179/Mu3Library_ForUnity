#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mu3Library.Scene
{
    public partial class SceneLoader
    {
        public bool IsSceneLoadedAsAdditiveWithAssetPath(string assetPath)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            return _currentAdditiveScenes.Contains(sceneName);
        }

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

            if (_singleSceneOperation != null)
            {
                return;
            }

            Debug.Log($"Load single scene start. sceneName: {sceneName}");

            _loadingCount++;
            OnSceneLoadStart?.Invoke(sceneName);

            LoadSceneParameters parameters = new LoadSceneParameters(LoadSceneMode.Single, physicsMode);
            AsyncOperation ao = EditorSceneManager.LoadSceneAsyncInPlayMode(assetPath, parameters);
            ao.allowSceneActivation = false;

            _singleSceneOperation = CreateLoadOperation(sceneName, ao, isAdditive: false);
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

            if (_loadAdditiveSceneOperations.ContainsKey(sceneName))
            {
                return;
            }

            Debug.Log($"Load additive scene start. sceneName: {sceneName}");

            _loadingCount++;
            OnSceneLoadStart?.Invoke(sceneName);

            LoadSceneParameters parameters = new LoadSceneParameters(LoadSceneMode.Additive, physicsMode);
            AsyncOperation ao = EditorSceneManager.LoadSceneAsyncInPlayMode(assetPath, parameters);
            ao.allowSceneActivation = false;

            _loadAdditiveSceneOperations.Add(sceneName, CreateLoadOperation(sceneName, ao, isAdditive: true));
        }

        public void UnloadAdditiveSceneWithAssetPath(string assetPath)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            if (!_currentAdditiveScenes.Contains(sceneName))
            {
                Debug.LogWarning($"Scene is not loaded. sceneName: {sceneName}");
                return;
            }

            if (_unloadAdditiveSceneOperations.ContainsKey(sceneName))
            {
                return;
            }

            AsyncOperation ao = EditorSceneManager.UnloadSceneAsync(sceneName);
            ao.allowSceneActivation = false;

            _unloadAdditiveSceneOperations.Add(sceneName, CreateUnloadOperation(sceneName, ao));
        }

        private SceneAsset LoadSceneAsset(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) ?
                AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPath) :
                null;
        }
    }
}
#endif
