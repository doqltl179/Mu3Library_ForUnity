using System;

namespace Mu3Library.Scene
{
    public interface ISceneLoaderEventBus
    {
        public event Action<string> OnSceneLoadStart;
        public event Action<string> OnSceneLoadEnd;
        public event Action<string, float> OnSceneLoadProgress;
        public event Action<string> OnAdditiveSceneUnloadStart;
        public event Action<string> OnAdditiveSceneUnloadEnd;
    }
}
