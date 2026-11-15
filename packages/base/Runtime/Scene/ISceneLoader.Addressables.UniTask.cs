#if MU3LIBRARY_UNITASK_SUPPORT && MU3LIBRARY_ADDRESSABLES_SUPPORT
using Cysharp.Threading.Tasks;

namespace Mu3Library.Scene
{
    public partial interface ISceneLoader
    {
        public UniTask<bool> PreloadSingleSceneWithAddressablesAsync(string key);
        public UniTask<bool> ActivateSingleSceneWithAddressablesAsync(string key);
        public UniTask<bool> LoadSingleSceneWithAddressablesAsync(string key);

        public UniTask<bool> PreloadAdditiveSceneWithAddressablesAsync(string key);
        public UniTask<bool> ActivateAdditiveSceneWithAddressablesAsync(string key);
        public UniTask<bool> LoadAdditiveSceneWithAddressablesAsync(string key);
        public UniTask<bool> UnloadAdditiveSceneWithAddressablesAsync(string key, bool autoReleaseHandle = true);
    }
}
#endif
