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
