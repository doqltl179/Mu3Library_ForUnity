#if MU3LIBRARY_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Mu3Library.Scene
{
    public partial interface ISceneLoader
    {
        public UniTask<bool> LoadSingleSceneAsync(string sceneName, CancellationToken cancellationToken = default);
        public UniTask<bool> LoadAdditiveSceneAsync(string sceneName, CancellationToken cancellationToken = default);
        public UniTask<bool> UnloadAdditiveSceneAsync(string sceneName, CancellationToken cancellationToken = default);
    }
}
#endif
