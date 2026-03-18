namespace Mu3Library.Scene
{
    public partial interface ISceneLoader
    {
        public bool IsLoading { get; }
        public int LoadingCount { get; }
        public string CurrentSceneName { get; }

        public bool UseFakeLoading { get; set; }
        public float FakeLoadingTime { get; set; }

        public bool IsSceneLoadedAsAdditive(string sceneName);

        public void LoadSingleScene(string sceneName);
        public void LoadAdditiveScene(string sceneName);
        public void UnloadAdditiveScene(string sceneName);
    }
}
