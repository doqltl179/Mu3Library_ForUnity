#if MU3LIBRARY_UNITASK_SUPPORT
using UnityEngine;
using Cysharp.Threading.Tasks;

using Object = UnityEngine.Object;

namespace Mu3Library.Resource
{
    public partial class ResourceLoader
    {
        public async UniTask<T> LoadAsync<T>(string path) where T : class
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogError("Resource path is null or empty.");
                return null;
            }

            path = NormalizePath(path);

            if (TryGetCached(path, typeof(T), out Object cached))
            {
                return cached as T;
            }

            ResourceRequest request = Resources.LoadAsync(path, typeof(T));
            await request.ToUniTask();

            Object loaded = request.asset;
            if (loaded == null)
            {
                Debug.LogError($"Object not found in resources folder. path: {path}");
                return null;
            }

            Cache(path, typeof(T), loaded);
            return loaded as T;
        }
    }
}
#endif
