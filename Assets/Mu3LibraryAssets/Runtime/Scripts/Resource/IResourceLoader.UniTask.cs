#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mu3Library.Resource
{
    public partial interface IResourceLoader
    {
        public UniTask<T> LoadAsync<T>(string path) where T : Object;
        public UniTask<T[]> LoadAllAsync<T>(string path) where T : Object;
    }
}
#endif
