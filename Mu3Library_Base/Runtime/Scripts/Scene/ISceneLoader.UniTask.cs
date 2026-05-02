#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;

namespace Mu3Library.Scene
{
    public partial interface ISceneLoader
    {
        public UniTask<bool> PreloadSingleSceneAsync(string sceneName);
        public UniTask<bool> ActivateSingleSceneAsync(string sceneName);
        public UniTask<bool> LoadSingleSceneAsync(string sceneName);

        public UniTask<bool> PreloadAdditiveSceneAsync(string sceneName);
        public UniTask<bool> ActivateAdditiveSceneAsync(string sceneName);
        public UniTask<bool> LoadAdditiveSceneAsync(string sceneName);
        public UniTask<bool> UnloadAdditiveSceneAsync(string sceneName);
    }
}
#endif
