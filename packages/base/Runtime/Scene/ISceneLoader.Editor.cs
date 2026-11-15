#if UNITY_EDITOR
using UnityEngine.SceneManagement;

namespace Mu3Library.Scene
{
    public partial interface ISceneLoader
    {
        public bool IsSceneLoadedAsAdditiveWithAssetPath(string assetPath);

        public void PreloadSingleSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None);
        public void ActivateSingleSceneWithAssetPath(string assetPath);
        public void LoadSingleSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None);

        public void PreloadAdditiveSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None);
        public void ActivateAdditiveSceneWithAssetPath(string assetPath);
        public void LoadAdditiveSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None);

        public void UnloadAdditiveSceneWithAssetPath(string assetPath);
    }
}
#endif
