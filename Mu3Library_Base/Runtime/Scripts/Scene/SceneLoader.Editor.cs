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

        public void PreloadSingleSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None)
        {
            TryPreloadSingleSceneWithAssetPath(assetPath, physicsMode, autoActivate: false);
        }

        public void ActivateSingleSceneWithAssetPath(string assetPath)
        {
            TryActivateSingleSceneWithAssetPath(assetPath);
        }

        public void LoadSingleSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None)
        {
            TryPreloadSingleSceneWithAssetPath(assetPath, physicsMode, autoActivate: true);
        }

        public void PreloadAdditiveSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None)
        {
            TryPreloadAdditiveSceneWithAssetPath(assetPath, physicsMode, autoActivate: false);
        }

        public void ActivateAdditiveSceneWithAssetPath(string assetPath)
        {
            TryActivateAdditiveSceneWithAssetPath(assetPath);
        }

        public void LoadAdditiveSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None)
        {
            TryPreloadAdditiveSceneWithAssetPath(assetPath, physicsMode, autoActivate: true);
        }

        public void UnloadAdditiveSceneWithAssetPath(string assetPath)
        {
            TryStartUnloadAdditiveSceneWithAssetPath(assetPath);
        }

        private bool TryPreloadSingleSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode, bool autoActivate)
        {
            SceneCommandType commandType = autoActivate ? SceneCommandType.LoadSingle : SceneCommandType.PreloadSingle;
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(assetPath))
            {
                return RejectSceneCommand(assetPath, commandType, SceneCommandRejectReason.InvalidSceneName);
            }

            if (IsSingleSceneAlreadyCurrent(sceneName, _currentSceneName))
            {
                return true;
            }

            SceneAsset sceneAsset = LoadSceneAsset(assetPath);
            if (sceneAsset == null)
            {
                return RejectSceneCommand(sceneName, commandType, SceneCommandRejectReason.SceneNotFound);
            }

            SceneCommandGate gate = GuardSingleScenePreload(sceneName, commandType, autoActivate);
            if (gate.IsSettled)
            {
                return gate.Result;
            }

            Debug.Log($"Editor single scene preload start. sceneName: {sceneName}");

            LoadSceneParameters parameters = new LoadSceneParameters(LoadSceneMode.Single, physicsMode);
            AsyncOperation ao = EditorSceneManager.LoadSceneAsyncInPlayMode(assetPath, parameters);
            ao.allowSceneActivation = false;

            _singleSceneOperation = CreateLoadOperation(sceneName, ao, isAdditive: false, autoActivate: autoActivate);
            EmitStatusChanged(_singleSceneOperation, force: true);
            return true;
        }

        private bool TryActivateSingleSceneWithAssetPath(string assetPath)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(assetPath))
            {
                return RejectSceneCommand(assetPath, SceneCommandType.ActivateSingle, SceneCommandRejectReason.InvalidSceneName);
            }

            return ActivatePreloadedSingleScene(sceneName, _currentSceneName);
        }

        private bool TryPreloadAdditiveSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode, bool autoActivate)
        {
            SceneCommandType commandType = autoActivate ? SceneCommandType.LoadAdditive : SceneCommandType.PreloadAdditive;
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(assetPath))
            {
                return RejectSceneCommand(assetPath, commandType, SceneCommandRejectReason.InvalidSceneName);
            }

            if (IsAdditiveSceneAlreadyLoaded(sceneName))
            {
                return true;
            }

            SceneAsset sceneAsset = LoadSceneAsset(assetPath);
            if (sceneAsset == null)
            {
                return RejectSceneCommand(sceneName, commandType, SceneCommandRejectReason.SceneNotFound);
            }

            SceneCommandGate gate = GuardAdditiveScenePreload(sceneName, commandType, autoActivate);
            if (gate.IsSettled)
            {
                return gate.Result;
            }

            Debug.Log($"Editor additive scene preload start. sceneName: {sceneName}");

            LoadSceneParameters parameters = new LoadSceneParameters(LoadSceneMode.Additive, physicsMode);
            AsyncOperation ao = EditorSceneManager.LoadSceneAsyncInPlayMode(assetPath, parameters);
            ao.allowSceneActivation = false;

            SceneOperation operation = CreateLoadOperation(sceneName, ao, isAdditive: true, autoActivate: autoActivate);
            _loadAdditiveSceneOperations.Add(sceneName, operation);
            EmitStatusChanged(operation, force: true);
            return true;
        }

        private bool TryActivateAdditiveSceneWithAssetPath(string assetPath)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(assetPath))
            {
                return RejectSceneCommand(assetPath, SceneCommandType.ActivateAdditive, SceneCommandRejectReason.InvalidSceneName);
            }

            return ActivatePreloadedAdditiveScene(sceneName);
        }

        private bool TryStartUnloadAdditiveSceneWithAssetPath(string assetPath)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(assetPath))
            {
                return RejectSceneCommand(assetPath, SceneCommandType.UnloadAdditive, SceneCommandRejectReason.InvalidSceneName);
            }

            SceneCommandGate gate = GuardAdditiveSceneUnload(sceneName);
            if (gate.IsSettled)
            {
                return gate.Result;
            }

            AsyncOperation ao = EditorSceneManager.UnloadSceneAsync(sceneName);

            SceneOperation operation = CreateUnloadOperation(sceneName, ao);
            operation.SceneHandle = ResolveBuiltInSceneHandle(sceneName);
            _unloadAdditiveSceneOperations.Add(sceneName, operation);
            EmitStatusChanged(operation, force: true);
            return true;
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
