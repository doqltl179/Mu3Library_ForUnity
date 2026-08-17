#if MU3LIBRARY_ADDRESSABLES_SUPPORT && MU3LIBRARY_UNITASK_SUPPORT
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Mu3Library.Addressable
{
    public partial class AddressablesManager
    {




        public async UniTask<T> LoadAssetAsync<T>(object key) where T : class
        {
            // The cache key carries the requested type, matching the callback path, so one
            // address loaded as two different types holds two independent entries.
            object cacheKey = CreateSingleAssetCacheKey<T>(key);
            if (TryGetCachedAssetInternal(cacheKey, out T cached))
            {
                return cached;
            }

            if (TryGetValidCachedHandle(cacheKey, out AsyncOperationHandle existing))
            {
                if (!existing.IsDone)
                {
                    T existingAsset = null;
                    try
                    {
                        await existing.ToUniTask();
                    }
                    finally
                    {
                        existingAsset = FinalizeCachedLoad<T>(key, cacheKey, existing);
                    }

                    return existingAsset;
                }

                return FinalizeCachedLoad<T>(key, cacheKey, existing);
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
            RegisterHandleCache(key, cacheKey, handle);

            T asset = null;
            try
            {
                await handle.ToUniTask();
            }
            finally
            {
                asset = FinalizeCachedLoad<T>(key, cacheKey, handle);
            }

            return asset;
        }

        public async UniTask<IList<T>> LoadAssetsAsync<T>(object key, Action<T> perAssetCallback = null)
        {
            Type cacheType = typeof(T);
            ListCacheKey cacheKey = ListCacheKey.Create(key, cacheType);
            if (TryGetCachedAssetInternal(cacheKey, out IList<T> cached))
            {
                return cached;
            }

            if (TryGetValidCachedHandle(cacheKey, out AsyncOperationHandle existing))
            {
                if (!existing.IsDone)
                {
                    IList<T> existingAssets = null;
                    try
                    {
                        await existing.ToUniTask();
                    }
                    finally
                    {
                        existingAssets = FinalizeCachedLoad<IList<T>>(key, cacheKey, existing);
                    }

                    return existingAssets;
                }

                return FinalizeCachedLoad<IList<T>>(key, cacheKey, existing);
            }

            AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(key, perAssetCallback);
            RegisterHandleCache(key, cacheKey, handle);

            IList<T> assets = null;
            try
            {
                await handle.ToUniTask();
            }
            finally
            {
                assets = FinalizeCachedLoad<IList<T>>(key, cacheKey, handle);
            }

            return assets;
        }

        public async UniTask<Dictionary<string, T>> LoadAssetsWithKeysAsync<T>(object key)
        {
            ListCacheKey cacheKey = ListCacheKey.Create(key, typeof(AssetsWithKeysCacheMarker<T>));
            if (TryGetCachedAssetInternal(cacheKey, out Dictionary<string, T> cached))
            {
                return cached;
            }

            if (TryGetValidCachedHandle(cacheKey, out AsyncOperationHandle existing))
            {
                if (!existing.IsDone)
                {
                    Dictionary<string, T> existingAssets = null;
                    try
                    {
                        await existing.ToUniTask();
                    }
                    finally
                    {
                        existingAssets = FinalizeAssetsWithKeysLoad<T>(key, cacheKey, existing);
                    }

                    return existingAssets;
                }

                return FinalizeAssetsWithKeysLoad<T>(key, cacheKey, existing);
            }

            AsyncOperationHandle<Dictionary<string, T>> handle = CreateLoadAssetsWithKeysOperation<T>(key);
            RegisterHandleCache(key, cacheKey, handle);

            Dictionary<string, T> assets = null;
            try
            {
                await handle.ToUniTask();
            }
            finally
            {
                assets = FinalizeAssetsWithKeysLoad<T>(key, cacheKey, handle);
            }

            return assets;
        }

        public async UniTask<long> GetDownloadSizeAsync(object key)
        {
            AsyncOperationHandle<long> handle = Addressables.GetDownloadSizeAsync(key);
            long size = 0L;
            try
            {
                await handle.ToUniTask();
                size = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : 0L;
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            return size;
        }

        public async UniTask<long> GetDownloadSizeAsync(IEnumerable keys, Addressables.MergeMode mergeMode)
        {
            AsyncOperationHandle<IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation>> locationsHandle =
                Addressables.LoadResourceLocationsAsync(keys, mergeMode, typeof(object));
            try
            {
                await locationsHandle.ToUniTask();

                if (locationsHandle.Status != AsyncOperationStatus.Succeeded || locationsHandle.Result == null)
                {
                    return 0L;
                }

                AsyncOperationHandle<long> sizeHandle = Addressables.GetDownloadSizeAsync(locationsHandle.Result);
                long size = 0L;
                try
                {
                    await sizeHandle.ToUniTask();
                    size = sizeHandle.Status == AsyncOperationStatus.Succeeded ? sizeHandle.Result : 0L;
                }
                finally
                {
                    if (sizeHandle.IsValid())
                    {
                        Addressables.Release(sizeHandle);
                    }
                }

                return size;
            }
            finally
            {
                if (locationsHandle.IsValid())
                {
                    Addressables.Release(locationsHandle);
                }
            }
        }

        public async UniTask DownloadDependenciesAsync(object key, Action<float> progress = null, bool autoReleaseHandle = true)
        {
            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(key, autoReleaseHandle);
            TrackDownloadHandle(handle, progress);
            try
            {
                await handle.ToUniTask();
            }
            finally
            {
                if (!autoReleaseHandle && handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        public async UniTask DownloadDependenciesAsync(IEnumerable keys, Addressables.MergeMode mergeMode, Action<float> progress = null, bool autoReleaseHandle = true)
        {
            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(keys, mergeMode, autoReleaseHandle);
            TrackDownloadHandle(handle, progress);
            try
            {
                await handle.ToUniTask();
            }
            finally
            {
                if (!autoReleaseHandle && handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        public async UniTask<IList<string>> CheckForCatalogUpdatesAsync(bool autoReleaseHandle = true)
        {
            AsyncOperationHandle<List<string>> handle = Addressables.CheckForCatalogUpdates(autoReleaseHandle);
            IList<string> catalogs = null;
            try
            {
                await handle.ToUniTask();
                catalogs = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
            }
            finally
            {
                if (!autoReleaseHandle && handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            return catalogs;
        }

        public async UniTask<IList<IResourceLocator>> UpdateCatalogsAsync(bool autoReleaseHandle = true)
        {
            AsyncOperationHandle<List<IResourceLocator>> handle = Addressables.UpdateCatalogs(autoReleaseHandle);
            IList<IResourceLocator> locators = null;
            try
            {
                await handle.ToUniTask();
                locators = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
            }
            finally
            {
                if (!autoReleaseHandle && handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            if (locators != null)
            {
                RefreshLocatorKeys();
            }

            return locators;
        }

        public async UniTask InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            // BeginInitialize owns acquiring the handle and attaching the completion handler for
            // the callback and the UniTask paths alike, and leaves a running initialization alone.
            BeginInitialize();

            AsyncOperationHandle handle = _initializeHandle;
            if (_isInitializing && handle.IsValid() && !handle.IsDone)
            {
                try
                {
                    await handle.ToUniTask();
                }
                catch (Exception)
                {
                    // OnInitializeCompleted logs the failure and reports it through InitializeError
                    // and OnInitializeResult, so this path stays as quiet as Initialize(Action).
                }
            }

            if (_isInitializing)
            {
                await UniTask.WaitUntil(() => !_isInitializing);
            }
        }

    }
}
#endif
