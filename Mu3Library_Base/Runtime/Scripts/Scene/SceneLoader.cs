using System;
using System.Collections.Generic;
using Mu3Library.DI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mu3Library.Scene
{
    public partial class SceneLoader : IUpdatable, ISceneLoader, ISceneLoaderEventBus, IDisposable
    {
        private partial class SceneOperation
        {
            public string SceneName;
            public bool IsAdditive;
            public bool IsUnload;
            public bool AutoActivate;
            public bool ActivationRequested;
            public ScenePhase Phase;
            public float Progress;
            public bool HasReportedStatus;
            public ScenePhase LastReportedPhase;
            public float LastReportedProgress;
            public bool HasCompletionResult;
            public bool CompletedSuccessfully;
            public int SceneHandle;
            public bool HasUnitySceneEvent;
            public AsyncOperation Operation;
        }

        public int LoadingCount => GetTrackedOperationCount();
        public bool IsLoading => LoadingCount > 0;

        private string _currentSceneName = "";
        private string _currentSingleSceneStatusName = "";
        public string CurrentSceneName => _currentSceneName;

        private readonly HashSet<string> _currentAdditiveScenes = new();

        private SceneOperation _singleSceneOperation = null;
        private readonly Dictionary<string, SceneOperation> _loadAdditiveSceneOperations = new();
        private readonly Dictionary<string, SceneOperation> _unloadAdditiveSceneOperations = new();

        public event Action<string> OnSingleSceneLoadStarted;
        public event Action<string, float> OnSingleScenePreloadProgress;
        public event Action<string> OnSingleScenePreloaded;
        public event Action<string> OnSingleSceneLoaded;
        public event Action<string, string> OnSingleSceneChanged;

        public event Action<string> OnAdditiveSceneLoadStarted;
        public event Action<string, float> OnAdditiveScenePreloadProgress;
        public event Action<string> OnAdditiveScenePreloaded;
        public event Action<string> OnAdditiveSceneLoaded;

        public event Action<string, float> OnAdditiveSceneUnloadProgress;
        public event Action<string> OnAdditiveSceneUnloaded;

        public event Action<SceneCommandRejectedInfo> OnSceneCommandRejected;



        public SceneLoader()
        {
            _currentSceneName = SceneManager.GetActiveScene().name;
            _currentSingleSceneStatusName = _currentSceneName;

            SceneManager.sceneLoaded += HandleUnitySceneLoaded;
            SceneManager.sceneUnloaded += HandleUnitySceneUnloaded;
        }

        public void Dispose()
        {
            SceneManager.sceneLoaded -= HandleUnitySceneLoaded;
            SceneManager.sceneUnloaded -= HandleUnitySceneUnloaded;
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

        private void HandleUnitySceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode _)
        {
            if (TryEmitSingleSceneLoadedFromUnityEvent(scene))
            {
                return;
            }

            TryEmitAdditiveSceneLoadedFromUnityEvent(scene);
        }

        private void HandleUnitySceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            TryEmitAdditiveSceneUnloadedFromUnityEvent(scene);
        }

        #region Utility
        public bool IsSceneLoadedAsAdditive(string sceneName)
        {
            return _currentAdditiveScenes.Contains(sceneName);
        }

        public bool TryGetSingleSceneStatus(out SceneStatus status)
        {
            if (TryGetSingleSceneOperation(out SceneOperation operation))
            {
                status = CreateStatus(operation);
                return true;
            }

            if (!string.IsNullOrEmpty(_currentSceneName))
            {
                status = new SceneStatus(_currentSceneName, false, ScenePhase.Loaded, 1.0f);
                return true;
            }

            status = default;
            return false;
        }

        public bool TryGetAdditiveSceneStatus(string sceneName, out SceneStatus status)
        {
            if (TryGetAdditiveSceneOperation(sceneName, out SceneOperation operation))
            {
                status = CreateStatus(operation);
                return true;
            }

            if (_currentAdditiveScenes.Contains(sceneName))
            {
                status = new SceneStatus(sceneName, true, ScenePhase.Loaded, 1.0f);
                return true;
            }

            status = default;
            return false;
        }

        public void PreloadSingleScene(string sceneName)
        {
            TryPreloadSingleScene(sceneName, autoActivate: false);
        }

        public void ActivateSingleScene(string sceneName)
        {
            TryActivateSingleScene(sceneName);
        }

        public void LoadSingleScene(string sceneName)
        {
            TryPreloadSingleScene(sceneName, autoActivate: true);
        }

        public void PreloadAdditiveScene(string sceneName)
        {
            TryPreloadAdditiveScene(sceneName, autoActivate: false);
        }

        public void ActivateAdditiveScene(string sceneName)
        {
            TryActivateAdditiveScene(sceneName);
        }

        public void LoadAdditiveScene(string sceneName)
        {
            TryPreloadAdditiveScene(sceneName, autoActivate: true);
        }

        public void UnloadAdditiveScene(string sceneName)
        {
            TryStartUnloadAdditiveScene(sceneName);
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

        private bool TryPreloadSingleScene(string sceneName, bool autoActivate)
        {
            SceneCommandType commandType = autoActivate ? SceneCommandType.LoadSingle : SceneCommandType.PreloadSingle;
            if (!ValidateSceneCommand(sceneName, commandType))
            {
                return false;
            }

            if (_currentSceneName == sceneName && !TryGetSingleSceneOperation(out _))
            {
                return true;
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

            if (!IsSceneInBuild(sceneName))
            {
                return RejectSceneCommand(sceneName, commandType, SceneCommandRejectReason.SceneNotFound);
            }

            Debug.Log($"Single scene preload start. sceneName: {sceneName}");

            AsyncOperation ao = SceneManager.LoadSceneAsync(sceneName);
            ao.allowSceneActivation = false;

            _singleSceneOperation = CreateLoadOperation(sceneName, ao, isAdditive: false, autoActivate: autoActivate);
            EmitStatusChanged(_singleSceneOperation, force: true);
            return true;
        }

        private bool TryActivateSingleScene(string sceneName)
        {
            if (!ValidateSceneCommand(sceneName, SceneCommandType.ActivateSingle))
            {
                return false;
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

        private bool TryPreloadAdditiveScene(string sceneName, bool autoActivate)
        {
            SceneCommandType commandType = autoActivate ? SceneCommandType.LoadAdditive : SceneCommandType.PreloadAdditive;
            if (!ValidateSceneCommand(sceneName, commandType))
            {
                return false;
            }

            if (_currentAdditiveScenes.Contains(sceneName) && !TryGetAdditiveSceneOperation(sceneName, out _))
            {
                return true;
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

            if (!IsSceneInBuild(sceneName))
            {
                return RejectSceneCommand(sceneName, commandType, SceneCommandRejectReason.SceneNotFound);
            }

            Debug.Log($"Additive scene preload start. sceneName: {sceneName}");

            AsyncOperation ao = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            ao.allowSceneActivation = false;

            SceneOperation operation = CreateLoadOperation(sceneName, ao, isAdditive: true, autoActivate: autoActivate);
            _loadAdditiveSceneOperations.Add(sceneName, operation);
            EmitStatusChanged(operation, force: true);
            return true;
        }

        private bool TryActivateAdditiveScene(string sceneName)
        {
            if (!ValidateSceneCommand(sceneName, SceneCommandType.ActivateAdditive))
            {
                return false;
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

        private bool TryStartUnloadAdditiveScene(string sceneName)
        {
            if (!ValidateSceneCommand(sceneName, SceneCommandType.UnloadAdditive))
            {
                return false;
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

            Debug.Log($"Additive scene unload start. sceneName: {sceneName}");
            AsyncOperation ao = SceneManager.UnloadSceneAsync(sceneName);

            SceneOperation operation = CreateUnloadOperation(sceneName, ao);
            operation.SceneHandle = ResolveBuiltInSceneHandle(sceneName);
            _unloadAdditiveSceneOperations.Add(sceneName, operation);
            EmitStatusChanged(operation, force: true);
            return true;
        }

        private SceneOperation CreateLoadOperation(string sceneName, AsyncOperation operation, bool isAdditive, bool autoActivate)
        {
            return new SceneOperation
            {
                SceneName = sceneName,
                Operation = operation,
                IsAdditive = isAdditive,
                IsUnload = false,
                AutoActivate = autoActivate,
                ActivationRequested = false,
                Phase = ScenePhase.Preloading,
                Progress = 0f,
                LastReportedProgress = -1f,
                HasCompletionResult = false,
                CompletedSuccessfully = false,
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
                AutoActivate = false,
                ActivationRequested = false,
                Phase = ScenePhase.Unloading,
                Progress = 0f,
                LastReportedProgress = -1f,
                HasCompletionResult = false,
                CompletedSuccessfully = false,
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

            FinalizeSingleSceneLoaded(
                _singleSceneOperation,
                ResolveLoadedSceneName(_singleSceneOperation),
                ResolveLoadedSceneHandle(_singleSceneOperation));

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

                FinalizeAdditiveSceneLoaded(pair.Value, ResolveLoadedSceneHandle(pair.Value));
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

                FinalizeAdditiveSceneUnloaded(pair.Value);
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
            if (operation.HasUnitySceneEvent)
            {
                return operation.Operation.isDone;
            }

            if (operation.IsUnload)
            {
                return UpdateUnloadOperation(operation);
            }

            float progress = GetLoadOperationProgress(operation);

            if (operation.Operation.progress < 0.9f)
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

            return operation.Operation.isDone;
        }

        private bool UpdateUnloadOperation(SceneOperation operation)
        {
            if (operation.HasUnitySceneEvent)
            {
                return operation.Operation.isDone;
            }

            float progress = Mathf.Clamp01(operation.Operation.progress);
            UpdateSceneOperationStatus(operation, ScenePhase.Unloading, progress);

            return operation.Operation.isDone;
        }

        private float GetLoadOperationProgress(SceneOperation operation)
        {
            return Mathf.Clamp01(operation.Operation.progress / 0.9f);
        }

        private void RequestActivation(SceneOperation operation)
        {
            operation.AutoActivate = true;

            if (operation.Phase == ScenePhase.Preloaded)
            {
                BeginActivation(operation);
            }
        }

        private void BeginActivation(SceneOperation operation)
        {
            if (operation.ActivationRequested)
            {
                return;
            }

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            if (operation.IsAddressables)
            {
                operation.ActivationOperation = null;
                if (operation.AddressablesHandle.IsValid() && operation.AddressablesHandle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    operation.ActivationOperation = operation.AddressablesHandle.Result.ActivateAsync();
                }

                operation.ActivationRequested = true;
                UpdateSceneOperationStatus(operation, ScenePhase.Activating, 1.0f, force: true);
                return;
            }
#endif

            operation.Operation.allowSceneActivation = true;
            operation.ActivationRequested = true;
            UpdateSceneOperationStatus(operation, ScenePhase.Activating, 1.0f, force: true);
        }

        private void UpdateSceneOperationStatus(SceneOperation operation, ScenePhase phase, float progress, bool force = false)
        {
            operation.Phase = phase;
            operation.Progress = progress;
            EmitStatusChanged(operation, force);
        }

        private void EmitStatusChanged(SceneOperation operation, bool force = false)
        {
            SceneStatus status = CreateStatus(operation);
            bool firstReport = !operation.HasReportedStatus;
            bool phaseChanged = firstReport || operation.LastReportedPhase != status.Phase;
            bool progressChanged = firstReport || !Mathf.Approximately(operation.LastReportedProgress, status.Progress);

            if (!force && !phaseChanged && !progressChanged)
            {
                return;
            }

            EmitLifecycleCallbacks(status, phaseChanged, progressChanged);

            operation.HasReportedStatus = true;
            operation.LastReportedPhase = status.Phase;
            operation.LastReportedProgress = status.Progress;
        }

        private void EmitLifecycleCallbacks(SceneStatus status, bool phaseChanged, bool progressChanged)
        {
            switch (status.Phase)
            {
                case ScenePhase.Preloading:
                    if (phaseChanged)
                    {
                        if (status.IsAdditive)
                        {
                            OnAdditiveSceneLoadStarted?.Invoke(status.SceneName);
                        }
                        else
                        {
                            OnSingleSceneLoadStarted?.Invoke(status.SceneName);
                        }
                    }

                    if (!progressChanged)
                    {
                        break;
                    }

                    if (status.IsAdditive)
                    {
                        OnAdditiveScenePreloadProgress?.Invoke(status.SceneName, status.Progress);
                    }
                    else
                    {
                        OnSingleScenePreloadProgress?.Invoke(status.SceneName, status.Progress);
                    }
                    break;

                case ScenePhase.Preloaded:
                    if (!phaseChanged)
                    {
                        break;
                    }

                    if (status.IsAdditive)
                    {
                        OnAdditiveScenePreloaded?.Invoke(status.SceneName);
                    }
                    else
                    {
                        OnSingleScenePreloaded?.Invoke(status.SceneName);
                    }
                    break;

                case ScenePhase.Activating:
                    break;

                case ScenePhase.Unloading:
                    if (progressChanged)
                    {
                        OnAdditiveSceneUnloadProgress?.Invoke(status.SceneName, status.Progress);
                    }
                    break;
            }
        }

        private bool TryEmitSingleSceneLoadedFromUnityEvent(UnityEngine.SceneManagement.Scene scene)
        {
            if (TryFinalizeSingleSceneLoaded(_singleSceneOperation, scene))
            {
                return true;
            }

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            if (TryFinalizeSingleSceneLoaded(_singleAddressablesSceneOperation, scene))
            {
                return true;
            }
#endif

            return false;
        }

        private bool TryEmitAdditiveSceneLoadedFromUnityEvent(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var pair in _loadAdditiveSceneOperations)
            {
                if (TryFinalizeAdditiveSceneLoaded(pair.Value, scene))
                {
                    return true;
                }
            }

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            foreach (var pair in _loadAdditiveAddressablesSceneOperations)
            {
                if (TryFinalizeAdditiveSceneLoaded(pair.Value, scene))
                {
                    return true;
                }
            }
#endif

            return false;
        }

        private bool TryEmitAdditiveSceneUnloadedFromUnityEvent(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var pair in _unloadAdditiveSceneOperations)
            {
                if (TryFinalizeAdditiveSceneUnloaded(pair.Value, scene))
                {
                    return true;
                }
            }

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            foreach (var pair in _unloadAdditiveAddressablesSceneOperations)
            {
                if (TryFinalizeAdditiveSceneUnloaded(pair.Value, scene))
                {
                    return true;
                }
            }
#endif

            return false;
        }

        private bool TryFinalizeSingleSceneLoaded(SceneOperation operation, UnityEngine.SceneManagement.Scene scene)
        {
            if (!MatchesUnitySceneEvent(operation, scene))
            {
                return false;
            }

            FinalizeSingleSceneLoaded(operation, scene.name, scene.handle);
            return true;
        }

        private bool TryFinalizeAdditiveSceneLoaded(SceneOperation operation, UnityEngine.SceneManagement.Scene scene)
        {
            if (!MatchesUnitySceneEvent(operation, scene))
            {
                return false;
            }

            FinalizeAdditiveSceneLoaded(operation, scene.handle);
            return true;
        }

        private bool TryFinalizeAdditiveSceneUnloaded(SceneOperation operation, UnityEngine.SceneManagement.Scene scene)
        {
            if (!MatchesUnitySceneEvent(operation, scene))
            {
                return false;
            }

            FinalizeAdditiveSceneUnloaded(operation);
            return true;
        }

        private void FinalizeSingleSceneLoaded(SceneOperation operation, string loadedSceneName, int sceneHandle)
        {
            if (operation == null || operation.HasUnitySceneEvent)
            {
                return;
            }

            string previousSceneName = _currentSceneName;
            Debug.Log($"Single scene load end. sceneName: {operation.SceneName}");

            operation.SceneHandle = sceneHandle;
            operation.HasUnitySceneEvent = true;
            operation.Phase = ScenePhase.Loaded;
            operation.Progress = 1.0f;

            _currentSceneName = loadedSceneName;
            _currentSingleSceneStatusName = operation.SceneName;
            _currentAdditiveScenes.Clear();

            OnSingleSceneLoaded?.Invoke(operation.SceneName);
            OnSingleSceneChanged?.Invoke(previousSceneName, _currentSceneName);
        }

        private void FinalizeAdditiveSceneLoaded(SceneOperation operation, int sceneHandle)
        {
            if (operation == null || operation.HasUnitySceneEvent)
            {
                return;
            }

            Debug.Log($"Additive scene load end. sceneName: {operation.SceneName}");

            operation.SceneHandle = sceneHandle;
            operation.HasUnitySceneEvent = true;
            operation.Phase = ScenePhase.Loaded;
            operation.Progress = 1.0f;

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            if (operation.IsAddressables)
            {
                _loadedAddressableSceneHandles[operation.SceneName] = operation.AddressablesHandle;
            }
#endif

            _currentAdditiveScenes.Add(operation.SceneName);
            OnAdditiveSceneLoaded?.Invoke(operation.SceneName);
        }

        private void FinalizeAdditiveSceneUnloaded(SceneOperation operation)
        {
            if (operation == null || operation.HasUnitySceneEvent)
            {
                return;
            }

            Debug.Log($"Additive scene unload end. sceneName: {operation.SceneName}");

            operation.HasUnitySceneEvent = true;
            operation.Progress = 1.0f;

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            if (operation.IsAddressables)
            {
                _loadedAddressableSceneHandles.Remove(operation.SceneName);
            }
#endif

            _currentAdditiveScenes.Remove(operation.SceneName);
            OnAdditiveSceneUnloaded?.Invoke(operation.SceneName);
        }

        private SceneStatus CreateStatus(SceneOperation operation)
        {
            return new SceneStatus(operation.SceneName, operation.IsAdditive, operation.Phase, operation.Progress);
        }

        private bool MatchesUnitySceneEvent(SceneOperation operation, UnityEngine.SceneManagement.Scene scene)
        {
            if (operation == null || operation.HasUnitySceneEvent)
            {
                return false;
            }

            if (operation.SceneHandle != 0)
            {
                return operation.SceneHandle == scene.handle;
            }

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            if (operation.IsAddressables)
            {
                if (!operation.AddressablesHandle.IsValid() ||
                    operation.AddressablesHandle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    return false;
                }

                UnityEngine.SceneManagement.Scene addressableScene = operation.AddressablesHandle.Result.Scene;
                return addressableScene.IsValid() && addressableScene.handle == scene.handle;
            }
#endif

            return string.Equals(operation.SceneName, scene.name, StringComparison.Ordinal);
        }

        private static int ResolveBuiltInSceneHandle(string sceneName)
        {
            UnityEngine.SceneManagement.Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() ? scene.handle : 0;
        }

        private string ResolveLoadedSceneName(SceneOperation operation)
        {
#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            if (operation.IsAddressables &&
                operation.AddressablesHandle.IsValid() &&
                operation.AddressablesHandle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                UnityEngine.SceneManagement.Scene scene = operation.AddressablesHandle.Result.Scene;
                if (scene.IsValid())
                {
                    return scene.name;
                }
            }
#endif

            return operation.SceneName;
        }

        private int ResolveLoadedSceneHandle(SceneOperation operation)
        {
            if (operation.SceneHandle != 0)
            {
                return operation.SceneHandle;
            }

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            if (operation.IsAddressables &&
                operation.AddressablesHandle.IsValid() &&
                operation.AddressablesHandle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                UnityEngine.SceneManagement.Scene scene = operation.AddressablesHandle.Result.Scene;
                if (scene.IsValid())
                {
                    return scene.handle;
                }
            }
#endif

            return ResolveBuiltInSceneHandle(operation.SceneName);
        }

        private bool ValidateSceneCommand(string sceneName, SceneCommandType commandType)
        {
            if (!string.IsNullOrWhiteSpace(sceneName))
            {
                return true;
            }

            return RejectSceneCommand(sceneName, commandType, SceneCommandRejectReason.InvalidSceneName);
        }

        private bool RejectSceneCommand(string sceneName, SceneCommandType commandType, SceneCommandRejectReason reason)
        {
            string message = $"Scene command rejected. command={commandType} reason={reason} sceneName: {sceneName}";
            if (reason == SceneCommandRejectReason.InvalidSceneName || reason == SceneCommandRejectReason.SceneNotFound)
            {
                Debug.LogError(message);
            }
            else
            {
                Debug.LogWarning(message);
            }

            EmitRejectedCallback(sceneName, commandType, reason);
            return false;
        }

        private void EmitRejectedCallback(string sceneName, SceneCommandType commandType, SceneCommandRejectReason reason)
        {
            OnSceneCommandRejected?.Invoke(new SceneCommandRejectedInfo(sceneName, commandType, reason));
        }

        private bool TryGetSingleSceneOperation(out SceneOperation operation)
        {
            if (_singleSceneOperation != null)
            {
                if (_singleSceneOperation.HasUnitySceneEvent)
                {
                    _singleSceneOperation = null;
                }
                else
                {
                    operation = _singleSceneOperation;
                    return true;
                }
            }

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            if (_singleAddressablesSceneOperation != null)
            {
                if (_singleAddressablesSceneOperation.HasUnitySceneEvent)
                {
                    _singleAddressablesSceneOperation = null;
                }
                else
                {
                    operation = _singleAddressablesSceneOperation;
                    return true;
                }
            }
#endif

            operation = null;
            return false;
        }

        private bool TryGetAdditiveSceneOperation(string sceneName, out SceneOperation operation)
        {
            if (_loadAdditiveSceneOperations.TryGetValue(sceneName, out operation))
            {
                if (operation.HasUnitySceneEvent)
                {
                    _loadAdditiveSceneOperations.Remove(sceneName);
                }
                else
                {
                    return true;
                }
            }

            if (_unloadAdditiveSceneOperations.TryGetValue(sceneName, out operation))
            {
                if (operation.HasUnitySceneEvent)
                {
                    _unloadAdditiveSceneOperations.Remove(sceneName);
                }
                else
                {
                    return true;
                }
            }

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            if (_loadAdditiveAddressablesSceneOperations.TryGetValue(sceneName, out operation))
            {
                if (operation.HasUnitySceneEvent)
                {
                    _loadAdditiveAddressablesSceneOperations.Remove(sceneName);
                }
                else
                {
                    return true;
                }
            }

            if (_unloadAdditiveAddressablesSceneOperations.TryGetValue(sceneName, out operation))
            {
                if (operation.HasUnitySceneEvent)
                {
                    _unloadAdditiveAddressablesSceneOperations.Remove(sceneName);
                }
                else
                {
                    return true;
                }
            }
#endif

            operation = null;
            return false;
        }

        private bool IsSingleSceneOperationInProgress()
        {
            return TryGetSingleSceneOperation(out _);
        }

        private int GetTrackedOperationCount()
        {
            int count = 0;

            if (IsVisibleOperation(_singleSceneOperation))
            {
                count++;
            }

            count += CountVisibleOperations(_loadAdditiveSceneOperations);
            count += CountVisibleOperations(_unloadAdditiveSceneOperations);

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            if (IsVisibleOperation(_singleAddressablesSceneOperation))
            {
                count++;
            }

            count += CountVisibleOperations(_loadAdditiveAddressablesSceneOperations);
            count += CountVisibleOperations(_unloadAdditiveAddressablesSceneOperations);
#endif

            return count;
        }

        private bool IsAdditiveSceneOperationInProgress()
        {
            bool inProgress = HasVisibleOperations(_loadAdditiveSceneOperations) || HasVisibleOperations(_unloadAdditiveSceneOperations);
#if MU3LIBRARY_ADDRESSABLES_SUPPORT
            inProgress |= HasVisibleOperations(_loadAdditiveAddressablesSceneOperations) || HasVisibleOperations(_unloadAdditiveAddressablesSceneOperations);
#endif
            return inProgress;
        }

        private static bool IsVisibleOperation(SceneOperation operation)
        {
            return operation != null && !operation.HasUnitySceneEvent;
        }

        private static int CountVisibleOperations(Dictionary<string, SceneOperation> operations)
        {
            int count = 0;
            foreach (var pair in operations)
            {
                if (IsVisibleOperation(pair.Value))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasVisibleOperations(Dictionary<string, SceneOperation> operations)
        {
            foreach (var pair in operations)
            {
                if (IsVisibleOperation(pair.Value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
