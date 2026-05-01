#if MU3LIBRARY_UNITASK_SUPPORT
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
            if (TryGetCachedAsset(key, out T cached))
            {
                return cached;
            }

            if (_assetHandleCache.TryGetValue(key, out AsyncOperationHandle existing) && existing.IsValid())
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
                        existingAsset = FinalizeCachedLoad<T>(key, existing);
                    }

                    return existingAsset;
                }

                return FinalizeCachedLoad<T>(key, existing);
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
            _assetHandleCache[key] = handle;

            T asset = null;
            try
            {
                await handle.ToUniTask();
            }
            finally
            {
                asset = FinalizeCachedLoad<T>(key, handle);
            }

            return asset;
        }

        public async UniTask<IList<T>> LoadAssetsAsync<T>(object key, Action<T> perAssetCallback = null)
        {
            Type cacheType = typeof(T);
            ListCacheKey cacheKey = ListCacheKey.Create(key, cacheType);
            if (TryGetCachedAsset(cacheKey, out IList<T> cached))
            {
                return cached;
            }

            if (_assetHandleCache.TryGetValue(cacheKey, out AsyncOperationHandle existing) && existing.IsValid())
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
                        existingAssets = FinalizeCachedLoad<IList<T>>(cacheKey, existing);
                    }

                    return existingAssets;
                }

                return FinalizeCachedLoad<IList<T>>(cacheKey, existing);
            }

            AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(key, perAssetCallback);
            _assetHandleCache[cacheKey] = handle;

            IList<T> assets = null;
            try
            {
                await handle.ToUniTask();
            }
            finally
            {
                assets = FinalizeCachedLoad<IList<T>>(cacheKey, handle);
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

            return locators;
        }

        public async UniTask InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            if (_isInitializing)
            {
                try
                {
                    await _initializeHandle.ToUniTask();
                }
                finally
                {
                    await UniTask.WaitUntil(() => !_isInitializing);
                }

                return;
            }

            _isInitializing = true;
            _initializeHandle = Addressables.InitializeAsync();
            if (_initializeHandle.IsDone)
            {
                OnInitializeCompleted(_initializeHandle);
                return;
            }

            try
            {
                await _initializeHandle.ToUniTask();
            }
            finally
            {
                if (_isInitializing)
                {
                    OnInitializeCompleted(_initializeHandle);
                }
            }
        }

        private T FinalizeCachedLoad<T>(object key, AsyncOperationHandle handle) where T : class
        {
            T asset = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result as T : null;
            if (asset != null)
            {
                _assetCache[key] = asset;
                _cachedAssetKeyMap[asset] = key;
            }
            else
            {
                _assetHandleCache.Remove(key);

                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            return asset;
        }
    }
}
#endif
