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

            if (_currentSceneName == sceneName && !TryGetSingleSceneOperation(out _))
            {
                return true;
            }

            SceneAsset sceneAsset = LoadSceneAsset(assetPath);
            if (sceneAsset == null)
            {
                return RejectSceneCommand(sceneName, commandType, SceneCommandRejectReason.SceneNotFound);
            }

            if (TryGetSingleSceneOperation(out SceneOperation existingOperation))
            {
                if (existingOperation.SceneName != sceneName)
                {
                    return RejectSceneCommand(sceneName, commandType, SceneCommandRejectReason.Busy);
                }

                if (autoActivate)
                {
                    RequestActivation(existingOperation);
                }

                return true;
            }

            if (IsAdditiveSceneOperationInProgress())
            {
                return RejectSceneCommand(sceneName, commandType, SceneCommandRejectReason.Busy);
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

            if (_currentSceneName == sceneName && !TryGetSingleSceneOperation(out _))
            {
                return true;
            }

            if (!TryGetSingleSceneOperation(out SceneOperation operation) || operation.SceneName != sceneName)
            {
                return RejectSceneCommand(sceneName, SceneCommandType.ActivateSingle, SceneCommandRejectReason.NotPreloaded);
            }

            if (operation.Phase != ScenePhase.Preloaded && operation.Phase != ScenePhase.Activating)
            {
                return RejectSceneCommand(sceneName, SceneCommandType.ActivateSingle, SceneCommandRejectReason.NotPreloaded);
            }

            RequestActivation(operation);
            return true;
        }

        private bool TryPreloadAdditiveSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode, bool autoActivate)
        {
            SceneCommandType commandType = autoActivate ? SceneCommandType.LoadAdditive : SceneCommandType.PreloadAdditive;
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(assetPath))
            {
                return RejectSceneCommand(assetPath, commandType, SceneCommandRejectReason.InvalidSceneName);
            }

            if (_currentAdditiveScenes.Contains(sceneName) && !TryGetAdditiveSceneOperation(sceneName, out _))
            {
                return true;
            }

            SceneAsset sceneAsset = LoadSceneAsset(assetPath);
            if (sceneAsset == null)
            {
                return RejectSceneCommand(sceneName, commandType, SceneCommandRejectReason.SceneNotFound);
            }

            if (TryGetAdditiveSceneOperation(sceneName, out SceneOperation existingOperation))
            {
                if (existingOperation.IsUnload)
                {
                    return RejectSceneCommand(sceneName, commandType, SceneCommandRejectReason.Busy);
                }

                if (autoActivate)
                {
                    RequestActivation(existingOperation);
                }

                return true;
            }

            if (IsSingleSceneOperationInProgress())
            {
                return RejectSceneCommand(sceneName, commandType, SceneCommandRejectReason.Busy);
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

            if (_currentAdditiveScenes.Contains(sceneName) && !TryGetAdditiveSceneOperation(sceneName, out _))
            {
                return true;
            }

            if (!TryGetAdditiveSceneOperation(sceneName, out SceneOperation operation) || operation.IsUnload)
            {
                return RejectSceneCommand(sceneName, SceneCommandType.ActivateAdditive, SceneCommandRejectReason.NotPreloaded);
            }

            if (operation.Phase != ScenePhase.Preloaded && operation.Phase != ScenePhase.Activating)
            {
                return RejectSceneCommand(sceneName, SceneCommandType.ActivateAdditive, SceneCommandRejectReason.NotPreloaded);
            }

            RequestActivation(operation);
            return true;
        }

        private bool TryStartUnloadAdditiveSceneWithAssetPath(string assetPath)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(assetPath))
            {
                return RejectSceneCommand(assetPath, SceneCommandType.UnloadAdditive, SceneCommandRejectReason.InvalidSceneName);
            }

            if (!_currentAdditiveScenes.Contains(sceneName) && !TryGetAdditiveSceneOperation(sceneName, out _))
            {
                return RejectSceneCommand(sceneName, SceneCommandType.UnloadAdditive, SceneCommandRejectReason.NotLoaded);
            }

            if (TryGetAdditiveSceneOperation(sceneName, out SceneOperation existingOperation))
            {
                if (existingOperation.IsUnload)
                {
                    return true;
                }

                return RejectSceneCommand(sceneName, SceneCommandType.UnloadAdditive, SceneCommandRejectReason.Busy);
            }

            if (IsSingleSceneOperationInProgress())
            {
                return RejectSceneCommand(sceneName, SceneCommandType.UnloadAdditive, SceneCommandRejectReason.Busy);
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
