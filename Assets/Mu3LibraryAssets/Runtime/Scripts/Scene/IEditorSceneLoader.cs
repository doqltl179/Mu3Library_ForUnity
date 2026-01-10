#if UNITY_EDITOR
using UnityEngine.SceneManagement;

namespace Mu3Library.Scene
{
    public interface IEditorSceneLoader : ISceneLoader
    {
        public bool IsSceneLoadedAsAdditiveWithAssetPath(string assetPath);
        public void LoadSingleSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None);
        public void LoadAdditiveSceneWithAssetPath(string assetPath, LocalPhysicsMode physicsMode = LocalPhysicsMode.None);
        public void UnloadAdditiveSceneWithAssetPath(string assetPath);
    }
}
#endif
