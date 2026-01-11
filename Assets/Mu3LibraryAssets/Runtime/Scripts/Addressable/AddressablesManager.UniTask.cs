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
                    await existing.ToUniTask();
                }

                T existingAsset = existing.Status == AsyncOperationStatus.Succeeded ? existing.Result as T : null;
                if (existingAsset != null)
                {
                    _assetCache[key] = existingAsset;
                }
                else
                {
                    _assetHandleCache.Remove(key);
                    Addressables.Release(existing);
                }

                return existingAsset;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
            _assetHandleCache[key] = handle;

            await handle.ToUniTask();

            T asset = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
            if (asset != null)
            {
                _assetCache[key] = asset;
            }
            else
            {
                _assetHandleCache.Remove(key);
                Addressables.Release(handle);
            }

            return asset;
        }

        public async UniTask<IList<T>> LoadAssetsAsync<T>(object key, Action<T> perAssetCallback = null)
        {
            if (TryGetCachedAsset(key, out IList<T> cached))
            {
                return cached;
            }

            if (_assetHandleCache.TryGetValue(key, out AsyncOperationHandle existing) && existing.IsValid())
            {
                if (!existing.IsDone)
                {
                    await existing.ToUniTask();
                }

                IList<T> existingAssets = existing.Status == AsyncOperationStatus.Succeeded ? existing.Result as IList<T> : null;
                if (existingAssets != null)
                {
                    _assetCache[key] = existingAssets;
                }
                else
                {
                    _assetHandleCache.Remove(key);
                    Addressables.Release(existing);
                }

                return existingAssets;
            }

            AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(key, perAssetCallback);
            _assetHandleCache[key] = handle;

            await handle.ToUniTask();

            IList<T> assets = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
            if (assets != null)
            {
                _assetCache[key] = assets;
            }
            else
            {
                _assetHandleCache.Remove(key);
                Addressables.Release(handle);
            }

            return assets;
        }

        public async UniTask<long> GetDownloadSizeAsync(object key)
        {
            AsyncOperationHandle<long> handle = Addressables.GetDownloadSizeAsync(key);
            await handle.ToUniTask();

            long size = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : 0L;
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }

            return size;
        }

        public async UniTask<long> GetDownloadSizeAsync(IEnumerable keys, Addressables.MergeMode mergeMode = Addressables.MergeMode.Union)
        {
            AsyncOperationHandle<IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation>> locationsHandle =
                Addressables.LoadResourceLocationsAsync(keys, mergeMode, typeof(object));
            await locationsHandle.ToUniTask();

            if (locationsHandle.Status != AsyncOperationStatus.Succeeded || locationsHandle.Result == null)
            {
                if (locationsHandle.IsValid())
                {
                    Addressables.Release(locationsHandle);
                }
                return 0L;
            }

            AsyncOperationHandle<long> sizeHandle = Addressables.GetDownloadSizeAsync(locationsHandle.Result);
            await sizeHandle.ToUniTask();

            long size = sizeHandle.Status == AsyncOperationStatus.Succeeded ? sizeHandle.Result : 0L;
            if (sizeHandle.IsValid())
            {
                Addressables.Release(sizeHandle);
            }

            if (locationsHandle.IsValid())
            {
                Addressables.Release(locationsHandle);
            }

            return size;
        }

        public async UniTask DownloadDependenciesAsync(object key, Action<float> progress = null, bool autoReleaseHandle = true)
        {
            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(key, autoReleaseHandle);
            TrackDownloadHandle(handle, progress);
            await handle.ToUniTask();

            if (!autoReleaseHandle && handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        public async UniTask DownloadDependenciesAsync(IEnumerable keys, Action<float> progress = null, bool autoReleaseHandle = true, Addressables.MergeMode mergeMode = Addressables.MergeMode.Union)
        {
            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(keys, mergeMode, autoReleaseHandle);
            TrackDownloadHandle(handle, progress);
            await handle.ToUniTask();

            if (!autoReleaseHandle && handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        public async UniTask<IList<string>> CheckForCatalogUpdates(bool autoReleaseHandle = true)
        {
            AsyncOperationHandle<List<string>> handle = Addressables.CheckForCatalogUpdates(autoReleaseHandle);
            await handle.ToUniTask();

            IList<string> catalogs = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
            if (!autoReleaseHandle && handle.IsValid())
            {
                Addressables.Release(handle);
            }

            return catalogs;
        }

        public async UniTask<IList<IResourceLocator>> UpdateCatalogs(bool autoReleaseHandle = true)
        {
            AsyncOperationHandle<List<IResourceLocator>> handle = Addressables.UpdateCatalogs(autoReleaseHandle);
            await handle.ToUniTask();

            IList<IResourceLocator> locators = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
            if (!autoReleaseHandle && handle.IsValid())
            {
                Addressables.Release(handle);
            }

            return locators;
        }
    }
}
#endif
