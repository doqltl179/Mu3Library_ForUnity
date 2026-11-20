#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using UnityEngine.SceneManagement;

namespace Mu3Library.Scene
{
    public partial interface ISceneLoader
    {
        public void PreloadSingleSceneWithAddressables(string key);
        public void ActivateSingleSceneWithAddressables(string key);
        public void LoadSingleSceneWithAddressables(string key);

        public void PreloadAdditiveSceneWithAddressables(string key);
        public void ActivateAdditiveSceneWithAddressables(string key);
        public void LoadAdditiveSceneWithAddressables(string key);

        public void UnloadAdditiveSceneWithAddressables(string key, bool autoReleaseHandle = true);
    }
}
#endif
