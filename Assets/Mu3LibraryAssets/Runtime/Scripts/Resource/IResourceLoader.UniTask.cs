#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;

namespace Mu3Library.Resource
{
    public partial interface IResourceLoader
    {
        public UniTask<T> LoadAsync<T>(string path) where T : class;
    }
}
#endif
