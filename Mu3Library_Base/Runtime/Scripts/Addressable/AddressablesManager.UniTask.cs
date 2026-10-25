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

        // The catalog calls go through the callback path, which reads the handle inside its completion
        // callback: with auto-release, Addressables releases the handle right after the callbacks ran,
        // before an await on the handle itself resumes. A chain operation it does not release is released
        // by this manager on the next ResourceManager update instead, which can come after such an await
        // resumed, so the callback is the one place that reads every handle while it is valid. The
        // result is only taken over there, and the await resumes on the player loop as before, so the
        // caller's code never runs inside an Addressables callback, where a WaitForCompletion would
        // re-enter the ResourceManager update.

        public async UniTask<IList<string>> CheckForCatalogUpdatesAsync(bool autoReleaseHandle = true)
        {
            bool isCompleted = false;
            IList<string> catalogs = null;
            CheckForCatalogUpdates(result =>
            {
                catalogs = result;
                isCompleted = true;
            }, autoReleaseHandle);

            await UniTask.WaitUntil(() => isCompleted);
            return catalogs;
        }

        public UniTask<IList<IResourceLocator>> UpdateCatalogsAsync(bool autoReleaseHandle = true)
            => UpdateCatalogsAsync(autoCleanBundleCache: false, autoReleaseHandle: autoReleaseHandle);

        public async UniTask<IList<IResourceLocator>> UpdateCatalogsAsync(bool autoCleanBundleCache, bool autoReleaseHandle)
        {
            bool isCompleted = false;
            IList<IResourceLocator> locators = null;
            UpdateCatalogs(result =>
            {
                locators = result;
                isCompleted = true;
            }, autoCleanBundleCache, autoReleaseHandle);

            await UniTask.WaitUntil(() => isCompleted);
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
                    // Mu3Library does not reference UniTask.Addressables, so this binds to UniTask's
                    // IEnumerator extension, which waits for IsDone and does not throw for a failed
                    // handle. OnInitializeCompleted logs the failure and reports it through
                    // InitializeError and OnInitializeResult; the catch keeps this path as quiet as
                    // Initialize(Action) should the handle extension ever be the one that binds.
                    await handle.ToUniTask();
                }
                catch (Exception)
                {
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
