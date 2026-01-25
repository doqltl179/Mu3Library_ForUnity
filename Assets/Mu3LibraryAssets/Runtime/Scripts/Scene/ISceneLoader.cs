using System;

namespace Mu3Library.Scene
{
    public partial interface ISceneLoader
    {
        public bool IsLoading { get; }
        public int LoadingCount { get; }
        public string CurrentSceneName { get; }

        public bool UseFakeLoading { get; set; }
        public float FakeLoadingTime { get; set; }

        public event Action<string> OnSceneLoadStart;
        public event Action<string> OnSceneLoadEnd;
        public event Action<string, float> OnSceneLoadProgress;

        public bool IsSceneLoadedAsAdditive(string sceneName);

        public void LoadSingleScene(string sceneName);
        public void LoadAdditiveScene(string sceneName);
        public void UnloadAdditiveScene(string sceneName);
    }
}
