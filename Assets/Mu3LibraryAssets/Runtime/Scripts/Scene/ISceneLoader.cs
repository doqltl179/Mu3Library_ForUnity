using System;

namespace Mu3Library.Scene
{
    public partial interface ISceneLoader
    {
        public bool IsLoading { get; }
        public string CurrentSceneName { get; }
        public bool UseFakeLoading { get; set; }
        public float FakeLoadingTime { get; set; }

        public event Action<string> OnSceneLoadStart;
        public event Action<string> OnSceneLoadEnd;

        public bool IsSceneLoadedAsAdditive(string sceneName);

        public void LoadSingleScene(string sceneName);
        public void LoadAdditiveScene(string sceneName);
        public void UnloadAdditiveScene(string sceneName);
    }
}
