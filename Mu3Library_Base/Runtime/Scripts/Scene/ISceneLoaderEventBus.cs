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
    }
}
