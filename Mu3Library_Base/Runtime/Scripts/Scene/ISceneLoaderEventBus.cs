using System;
using Mu3Library.Foundation.Event;

namespace Mu3Library.Scene
{
    public enum SceneCommandType
    {
        PreloadSingle,
        ActivateSingle,
        LoadSingle,
        PreloadAdditive,
        ActivateAdditive,
        LoadAdditive,
        UnloadAdditive,
    }

    public enum SceneCommandRejectReason
    {
        InvalidSceneName,
        SceneNotFound,
        Busy,
        NotPreloaded,
        NotLoaded,
    }

    public readonly struct SceneCommandRejectedInfo
    {
        public string Target { get; }
        public SceneCommandType CommandType { get; }
        public SceneCommandRejectReason Reason { get; }

        public bool IsAdditive =>
            CommandType == SceneCommandType.PreloadAdditive ||
            CommandType == SceneCommandType.ActivateAdditive ||
            CommandType == SceneCommandType.LoadAdditive ||
            CommandType == SceneCommandType.UnloadAdditive;

        public bool IsUnload => CommandType == SceneCommandType.UnloadAdditive;

        public SceneCommandRejectedInfo(string target, SceneCommandType commandType, SceneCommandRejectReason reason)
        {
            Target = target;
            CommandType = commandType;
            Reason = reason;
        }
    }

    public readonly struct SceneLifecycleInfo
    {
        public string Target { get; }
        public string ResolvedSceneName { get; }
        public bool HasResolvedSceneName { get; }
        public bool IsAdditive { get; }
        public bool IsAddressables { get; }
        public ScenePhase Phase { get; }
        public float Progress { get; }

        public SceneLifecycleInfo(
            string target,
            string resolvedSceneName,
            bool hasResolvedSceneName,
            bool isAdditive,
            bool isAddressables,
            ScenePhase phase,
            float progress)
        {
            Target = target;
            ResolvedSceneName = resolvedSceneName;
            HasResolvedSceneName = hasResolvedSceneName;
            IsAdditive = isAdditive;
            IsAddressables = isAddressables;
            Phase = phase;
            Progress = progress;
        }
    }

    public interface ISceneLoaderEventBus
    {
        public event Action<string> OnSingleSceneLoadStarted;
        public event Action<string, float> OnSingleScenePreloadProgress;
        public event Action<string> OnSingleScenePreloaded;
        public event Action<string> OnSingleSceneLoaded;
        public event Action<string, string> OnSingleSceneChanged;
        public event Action<SceneLifecycleInfo> OnSingleSceneLifecycle;

        public event Action<string> OnAdditiveSceneLoadStarted;
        public event Action<string, float> OnAdditiveScenePreloadProgress;
        public event Action<string> OnAdditiveScenePreloaded;
        public event Action<string> OnAdditiveSceneLoaded;
        public event Action<SceneLifecycleInfo> OnAdditiveSceneLifecycle;

        public event Action<string, float> OnAdditiveSceneUnloadProgress;
        public event Action<string> OnAdditiveSceneUnloaded;

        public event Action<SceneCommandRejectedInfo> OnSceneCommandRejected;

        public ISubscriptionInfo SubscribeOnSingleSceneLoadStartedOnce(Action<string> callback);
        public ISubscriptionInfo SubscribeOnSingleSceneLoadStartedOnce(Action<string> callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnSingleScenePreloadedOnce(Action<string> callback);
        public ISubscriptionInfo SubscribeOnSingleScenePreloadedOnce(Action<string> callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnSingleSceneLoadedOnce(Action<string> callback);
        public ISubscriptionInfo SubscribeOnSingleSceneLoadedOnce(Action<string> callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnSingleSceneChangedOnce(Action<string, string> callback);
        public ISubscriptionInfo SubscribeOnSingleSceneChangedOnce(Action<string, string> callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnSingleSceneLifecycleOnce(Action<SceneLifecycleInfo> callback);
        public ISubscriptionInfo SubscribeOnSingleSceneLifecycleOnce(Action<SceneLifecycleInfo> callback, Action onDisposed);

        public ISubscriptionInfo SubscribeOnAdditiveSceneLoadStartedOnce(Action<string> callback);
        public ISubscriptionInfo SubscribeOnAdditiveSceneLoadStartedOnce(Action<string> callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnAdditiveScenePreloadedOnce(Action<string> callback);
        public ISubscriptionInfo SubscribeOnAdditiveScenePreloadedOnce(Action<string> callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnAdditiveSceneLoadedOnce(Action<string> callback);
        public ISubscriptionInfo SubscribeOnAdditiveSceneLoadedOnce(Action<string> callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnAdditiveSceneUnloadedOnce(Action<string> callback);
        public ISubscriptionInfo SubscribeOnAdditiveSceneUnloadedOnce(Action<string> callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnAdditiveSceneLifecycleOnce(Action<SceneLifecycleInfo> callback);
        public ISubscriptionInfo SubscribeOnAdditiveSceneLifecycleOnce(Action<SceneLifecycleInfo> callback, Action onDisposed);
    }
}
