namespace Mu3Library.Scene
{
    public enum ScenePhase
    {
        None,
        Preloading,
        Preloaded,
        Activating,
        Loaded,
        Unloading,
    }

    public readonly struct SceneStatus
    {
        public string SceneName { get; }
        public bool IsAdditive { get; }
        public ScenePhase Phase { get; }
        public float Progress { get; }

        public SceneStatus(string sceneName, bool isAdditive, ScenePhase phase, float progress)
        {
            SceneName = sceneName;
            IsAdditive = isAdditive;
            Phase = phase;
            Progress = progress;
        }
    }

    public partial interface ISceneLoader
    {
        public bool IsLoading { get; }
        public int LoadingCount { get; }
        public string CurrentSceneName { get; }

        public bool IsSceneLoadedAsAdditive(string sceneName);
        public bool TryGetSingleSceneStatus(out SceneStatus status);
        public bool TryGetAdditiveSceneStatus(string sceneName, out SceneStatus status);

        public void PreloadSingleScene(string sceneName);
        public void ActivateSingleScene(string sceneName);
        public void LoadSingleScene(string sceneName);

        public void PreloadAdditiveScene(string sceneName);
        public void ActivateAdditiveScene(string sceneName);
        public void LoadAdditiveScene(string sceneName);
        public void UnloadAdditiveScene(string sceneName);
    }
}
