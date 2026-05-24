using System;

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

    public interface ISceneLoaderEventBus
    {
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

        public uint SubscribeOnSingleSceneLoadStartedOnce(Action<string> callback);
        public uint SubscribeOnSingleSceneLoadStartedOnce(Action<string> callback, Action onDisposed);
        public uint SubscribeOnSingleScenePreloadedOnce(Action<string> callback);
        public uint SubscribeOnSingleScenePreloadedOnce(Action<string> callback, Action onDisposed);
        public uint SubscribeOnSingleSceneLoadedOnce(Action<string> callback);
        public uint SubscribeOnSingleSceneLoadedOnce(Action<string> callback, Action onDisposed);
        public uint SubscribeOnSingleSceneChangedOnce(Action<string, string> callback);
        public uint SubscribeOnSingleSceneChangedOnce(Action<string, string> callback, Action onDisposed);

        public uint SubscribeOnAdditiveSceneLoadStartedOnce(Action<string> callback);
        public uint SubscribeOnAdditiveSceneLoadStartedOnce(Action<string> callback, Action onDisposed);
        public uint SubscribeOnAdditiveScenePreloadedOnce(Action<string> callback);
        public uint SubscribeOnAdditiveScenePreloadedOnce(Action<string> callback, Action onDisposed);
        public uint SubscribeOnAdditiveSceneLoadedOnce(Action<string> callback);
        public uint SubscribeOnAdditiveSceneLoadedOnce(Action<string> callback, Action onDisposed);
        public uint SubscribeOnAdditiveSceneUnloadedOnce(Action<string> callback);
        public uint SubscribeOnAdditiveSceneUnloadedOnce(Action<string> callback, Action onDisposed);
    }
}
