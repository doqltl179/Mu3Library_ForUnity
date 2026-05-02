using System;
using Mu3Library.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CompileGateBatchEntryPoint
{
    private enum SmokePhase
    {
        None,
        WaitForSinglePreloaded,
        WaitForSingleLoaded,
        WaitForAdditivePreloaded,
        WaitForAdditiveLoaded,
        WaitForAdditiveUnloaded,
        Completed,
    }

    private const string SmokeDirectoryPath = "Assets/__SceneLoaderSmoke";
    private const string SingleScenePath = SmokeDirectoryPath + "/SceneLoaderSmokeSingle.unity";
    private const string AdditiveScenePath = SmokeDirectoryPath + "/SceneLoaderSmokeAdditive.unity";
    private const string SingleSceneName = "SceneLoaderSmokeSingle";
    private const string AdditiveSceneName = "SceneLoaderSmokeAdditive";

    private static SceneLoader _sceneLoader;
    private static SmokePhase _smokePhase;
    private static double _smokeDeadline;
    private static int _smokeExitCode;
    private static bool _smokeExitRequested;

    private static int _singleLoadStartedCount;
    private static int _singlePreloadProgressCount;
    private static int _singlePreloadedCount;
    private static int _singleLoadedCount;
    private static int _singleChangedCount;

    private static int _additiveLoadStartedCount;
    private static int _additivePreloadProgressCount;
    private static int _additivePreloadedCount;
    private static int _additiveLoadedCount;
    private static int _additiveUnloadProgressCount;
    private static int _additiveUnloadedCount;

    private static int _rejectedCount;
    private static SceneCommandRejectedInfo _lastRejectedInfo;
    private static string _lastChangedSceneName;

    public static void Run()
    {
        Debug.Log("COMPILE_GATE_START");
        EditorApplication.update += WaitForCompile;
        WaitForCompile();
    }

    private static void WaitForCompile()
    {
        if (EditorApplication.isCompiling) {
            return;
        }

        EditorApplication.update -= WaitForCompile;

        if (EditorUtility.scriptCompilationFailed) {
            Debug.LogError("COMPILE_FAILED");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("COMPILE_OK");
        EditorApplication.Exit(0);
    }

    public static void RunSceneLoaderSmoke()
    {
        Debug.Log("SCENE_LOADER_SMOKE_START");

        try
        {
            PrepareSmokeScenes();
        }
        catch (Exception exception)
        {
            FailSmoke($"Failed to prepare smoke scenes. {exception}");
            return;
        }

        ResetSmokeState();
        EditorApplication.playModeStateChanged += OnSmokePlayModeStateChanged;

        if (EditorApplication.isPlaying)
        {
            StartSceneLoaderSmoke();
            return;
        }

        EditorApplication.EnterPlaymode();
    }

    private static void PrepareSmokeScenes()
    {
        if (!AssetDatabase.IsValidFolder(SmokeDirectoryPath))
        {
            AssetDatabase.CreateFolder("Assets", "__SceneLoaderSmoke");
        }

        CreateEmptySceneAsset(SingleScenePath);
        CreateEmptySceneAsset(AdditiveScenePath);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AssetDatabase.Refresh();
    }

    private static void CreateEmptySceneAsset(string scenePath)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!EditorSceneManager.SaveScene(scene, scenePath))
        {
            throw new InvalidOperationException($"Could not save smoke scene at '{scenePath}'.");
        }
    }

    private static void ResetSmokeState()
    {
        _sceneLoader = null;
        _smokePhase = SmokePhase.None;
        _smokeExitCode = 0;
        _smokeExitRequested = false;
        _smokeDeadline = 0d;

        _singleLoadStartedCount = 0;
        _singlePreloadProgressCount = 0;
        _singlePreloadedCount = 0;
        _singleLoadedCount = 0;
        _singleChangedCount = 0;

        _additiveLoadStartedCount = 0;
        _additivePreloadProgressCount = 0;
        _additivePreloadedCount = 0;
        _additiveLoadedCount = 0;
        _additiveUnloadProgressCount = 0;
        _additiveUnloadedCount = 0;

        _rejectedCount = 0;
        _lastRejectedInfo = default;
        _lastChangedSceneName = string.Empty;
    }

    private static void OnSmokePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            StartSceneLoaderSmoke();
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode && _smokeExitRequested)
        {
            FinalizeSmokeExit();
        }
    }

    private static void StartSceneLoaderSmoke()
    {
        if (_sceneLoader != null)
        {
            return;
        }

        _sceneLoader = new SceneLoader();
        _sceneLoader.OnSingleSceneLoadStarted += _ => _singleLoadStartedCount++;
        _sceneLoader.OnSingleScenePreloadProgress += (_, __) => _singlePreloadProgressCount++;
        _sceneLoader.OnSingleScenePreloaded += _ => _singlePreloadedCount++;
        _sceneLoader.OnSingleSceneLoaded += _ => _singleLoadedCount++;
        _sceneLoader.OnSingleSceneChanged += (_, loadedSceneName) =>
        {
            _singleChangedCount++;
            _lastChangedSceneName = loadedSceneName;
        };

        _sceneLoader.OnAdditiveSceneLoadStarted += _ => _additiveLoadStartedCount++;
        _sceneLoader.OnAdditiveScenePreloadProgress += (_, __) => _additivePreloadProgressCount++;
        _sceneLoader.OnAdditiveScenePreloaded += _ => _additivePreloadedCount++;
        _sceneLoader.OnAdditiveSceneLoaded += _ => _additiveLoadedCount++;
        _sceneLoader.OnAdditiveSceneUnloadProgress += (_, __) => _additiveUnloadProgressCount++;
        _sceneLoader.OnAdditiveSceneUnloaded += _ => _additiveUnloadedCount++;
        _sceneLoader.OnSceneCommandRejected += info =>
        {
            _rejectedCount++;
            _lastRejectedInfo = info;
        };

        EditorApplication.update += PumpSceneLoaderSmoke;

        _sceneLoader.ActivateAdditiveSceneWithAssetPath(AdditiveScenePath);
        Ensure(_rejectedCount == 1, "Expected a rejected activate-additive command before preloading.");
        Ensure(_lastRejectedInfo.CommandType == SceneCommandType.ActivateAdditive, "Rejected command type mismatch.");
        Ensure(_lastRejectedInfo.Reason == SceneCommandRejectReason.NotPreloaded, "Rejected command reason mismatch.");

        _sceneLoader.PreloadSingleSceneWithAssetPath(SingleScenePath);
        Ensure(_sceneLoader.LoadingCount == 1, "Single-scene preload should register exactly one tracked operation.");

        _smokePhase = SmokePhase.WaitForSinglePreloaded;
        ResetSmokeDeadline();
    }

    private static void PumpSceneLoaderSmoke()
    {
        try
        {
            _sceneLoader?.Update();

            if (EditorApplication.timeSinceStartup > _smokeDeadline)
            {
                FailSmoke($"Timed out while waiting for phase '{_smokePhase}'.");
                return;
            }

            switch (_smokePhase)
            {
                case SmokePhase.WaitForSinglePreloaded:
                    WaitForSinglePreloaded();
                    break;

                case SmokePhase.WaitForSingleLoaded:
                    WaitForSingleLoaded();
                    break;

                case SmokePhase.WaitForAdditivePreloaded:
                    WaitForAdditivePreloaded();
                    break;

                case SmokePhase.WaitForAdditiveLoaded:
                    WaitForAdditiveLoaded();
                    break;

                case SmokePhase.WaitForAdditiveUnloaded:
                    WaitForAdditiveUnloaded();
                    break;
            }
        }
        catch (Exception exception)
        {
            FailSmoke($"SceneLoader smoke failed. {exception}");
        }
    }

    private static void WaitForSinglePreloaded()
    {
        if (!_sceneLoader.TryGetSingleSceneStatus(out SceneStatus status) || status.SceneName != SingleSceneName)
        {
            return;
        }

        if (status.Phase != ScenePhase.Preloaded)
        {
            return;
        }

        Ensure(_singleLoadStartedCount == 1, "Single-scene load start should fire exactly once.");
        Ensure(_singlePreloadProgressCount >= 1, "Single-scene preload progress should fire at least once.");
        Ensure(_singlePreloadedCount == 1, "Single-scene preloaded callback should fire exactly once.");
        Ensure(_sceneLoader.LoadingCount == 1, "Single-scene preloaded state should still be tracked until activation.");

        _sceneLoader.ActivateSingleSceneWithAssetPath(SingleScenePath);
        _smokePhase = SmokePhase.WaitForSingleLoaded;
        ResetSmokeDeadline();
    }

    private static void WaitForSingleLoaded()
    {
        if (!_sceneLoader.TryGetSingleSceneStatus(out SceneStatus status) || status.SceneName != SingleSceneName)
        {
            return;
        }

        if (status.Phase != ScenePhase.Loaded)
        {
            return;
        }

        Ensure(_singleLoadedCount == 1, "Single-scene loaded callback should fire exactly once.");
        Ensure(_singleChangedCount == 1, "Single-scene changed callback should fire exactly once.");
        Ensure(_lastChangedSceneName == SingleSceneName, "Single-scene changed callback should report the loaded scene name.");
        Ensure(_sceneLoader.LoadingCount == 0, "Tracked single-scene operation should be cleared after activation.");

        _sceneLoader.PreloadAdditiveSceneWithAssetPath(AdditiveScenePath);
        Ensure(_sceneLoader.LoadingCount == 1, "Additive preload should register exactly one tracked operation.");

        _smokePhase = SmokePhase.WaitForAdditivePreloaded;
        ResetSmokeDeadline();
    }

    private static void WaitForAdditivePreloaded()
    {
        if (!_sceneLoader.TryGetAdditiveSceneStatus(AdditiveSceneName, out SceneStatus status))
        {
            return;
        }

        if (status.Phase != ScenePhase.Preloaded)
        {
            return;
        }

        Ensure(_additiveLoadStartedCount == 1, "Additive load start should fire exactly once.");
        Ensure(_additivePreloadProgressCount >= 1, "Additive preload progress should fire at least once.");
        Ensure(_additivePreloadedCount == 1, "Additive preloaded callback should fire exactly once.");

        _sceneLoader.ActivateAdditiveSceneWithAssetPath(AdditiveScenePath);
        _smokePhase = SmokePhase.WaitForAdditiveLoaded;
        ResetSmokeDeadline();
    }

    private static void WaitForAdditiveLoaded()
    {
        if (!_sceneLoader.TryGetAdditiveSceneStatus(AdditiveSceneName, out SceneStatus status))
        {
            return;
        }

        if (status.Phase != ScenePhase.Loaded)
        {
            return;
        }

        Ensure(_additiveLoadedCount == 1, "Additive loaded callback should fire exactly once.");
        Ensure(_sceneLoader.IsSceneLoadedAsAdditiveWithAssetPath(AdditiveScenePath), "Loaded additive scene should be discoverable by asset path.");
        Ensure(_sceneLoader.LoadingCount == 0, "Tracked additive load should be cleared after activation.");

        _sceneLoader.UnloadAdditiveSceneWithAssetPath(AdditiveScenePath);
        Ensure(_sceneLoader.LoadingCount == 1, "Additive unload should register exactly one tracked operation.");

        _smokePhase = SmokePhase.WaitForAdditiveUnloaded;
        ResetSmokeDeadline();
    }

    private static void WaitForAdditiveUnloaded()
    {
        if (_sceneLoader.TryGetAdditiveSceneStatus(AdditiveSceneName, out _))
        {
            return;
        }

        Ensure(_additiveUnloadProgressCount >= 1, "Additive unload progress should fire at least once.");
        Ensure(_additiveUnloadedCount == 1, "Additive unloaded callback should fire exactly once.");
        Ensure(_sceneLoader.LoadingCount == 0, "Tracked additive unload should be cleared after completion.");

        _smokePhase = SmokePhase.Completed;
        RequestSmokeExit(0, "SCENE_LOADER_SMOKE_OK");
    }

    private static void ResetSmokeDeadline()
    {
        _smokeDeadline = EditorApplication.timeSinceStartup + 15d;
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void FailSmoke(string message)
    {
        Debug.LogError(message);
        RequestSmokeExit(1, "SCENE_LOADER_SMOKE_FAILED");
    }

    private static void RequestSmokeExit(int exitCode, string marker)
    {
        if (_smokeExitRequested)
        {
            return;
        }

        _smokeExitRequested = true;
        _smokeExitCode = exitCode;
        Debug.Log(marker);
        EditorApplication.update -= PumpSceneLoaderSmoke;

        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
            return;
        }

        FinalizeSmokeExit();
    }

    private static void FinalizeSmokeExit()
    {
        EditorApplication.playModeStateChanged -= OnSmokePlayModeStateChanged;
        EditorApplication.update -= PumpSceneLoaderSmoke;
        CleanupSmokeScenes();
        EditorApplication.Exit(_smokeExitCode);
    }

    private static void CleanupSmokeScenes()
    {
        if (AssetDatabase.IsValidFolder(SmokeDirectoryPath))
        {
            AssetDatabase.DeleteAsset(SmokeDirectoryPath);
            AssetDatabase.Refresh();
        }
    }
}