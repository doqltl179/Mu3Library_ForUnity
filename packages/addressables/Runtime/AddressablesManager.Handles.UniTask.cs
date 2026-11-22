#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;

namespace Mu3Library.Addressable
{
    public partial class AddressablesManager
    {
        public async UniTask<AssetHandle<T>> LoadAssetHandleAsync<T>(object key) where T : class
        {
            object cacheKey = CreateSingleAssetCacheKey<T>(key);
            T asset = await LoadAssetAsync<T>(key);
            return CreateHandle(key, cacheKey, asset);
        }
    }
}
#endif
