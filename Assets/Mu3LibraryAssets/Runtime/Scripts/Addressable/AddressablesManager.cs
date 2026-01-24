#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using System;
using System.Collections;
using System.Collections.Generic;
using Mu3Library.DI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Mu3Library.Addressable
{
    public partial class AddressablesManager : IAddressablesManager, IUpdatable, IDisposable
    {
        private bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        private bool _isInitializing = false;

        private AsyncOperationHandle _initializeHandle;
        private float _lastInitializeProgress = -1.0f;
        private float _lastDownloadProgress = -1.0f;

        private readonly Dictionary<object, object> _assetCache = new();
        private readonly Dictionary<object, AsyncOperationHandle> _assetHandleCache = new();

        private sealed class DownloadTracker
        {
            public AsyncOperationHandle Handle;
            public Action<float> Progress;
            public float LastProgress = -1.0f;
        }
        private readonly List<DownloadTracker> _downloadTrackers = new();

        public event Action OnInitialized;
        public event Action<float> OnInitializeProgress;
        public event Action<float> OnDownloadProgress;



        public void Dispose()
        {
            if (_initializeHandle.IsValid())
            {
                Addressables.Release(_initializeHandle);
            }

            ClearCache();

            _downloadTrackers.Clear();

            _isInitialized = false;
            _isInitializing = false;
            _lastDownloadProgress = -1.0f;

            OnInitialized = null;
            OnInitializeProgress = null;
            OnDownloadProgress = null;
        }

        public void Update()
        {
            UpdateInitializeProgress();
            UpdateDownloadProgress();
        }

        public void Initialize(Action callback = null)
        {
            if (_isInitialized)
            {
                callback?.Invoke();
                return;
            }

            if (callback != null)
            {
                OnInitialized += callback;
            }

            if (_isInitializing)
            {
                return;
            }

            _isInitializing = true;

            _initializeHandle = Addressables.InitializeAsync();
            if (_initializeHandle.IsDone)
            {
                OnInitializeCompleted(_initializeHandle);
                return;
            }

            _initializeHandle.Completed += OnInitializeCompleted;
        }

        private void OnInitializeCompleted(AsyncOperationHandle handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _isInitialized = true;
                Debug.Log("Addressables initialized.");
            }
            else
            {
                Debug.LogError($"Addressables initialize failed.\r\n{handle.OperationException?.Message}");
            }

            _isInitializing = false;
            _lastInitializeProgress = handle.PercentComplete;
            OnInitializeProgress?.Invoke(_lastInitializeProgress);

            OnInitialized?.Invoke();
        }

        public void LoadAsset<T>(object key, Action<T> callback = null) where T : class
        {
            if (TryGetCachedAsset(key, out T cached))
            {
                callback?.Invoke(cached);
                return;
            }

            if (_assetHandleCache.TryGetValue(key, out AsyncOperationHandle existing) && existing.IsValid())
            {
                if (existing.IsDone)
                {
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

                    callback?.Invoke(existingAsset);
                    return;
                }

                existing.Completed += op =>
                {
                    T existingAsset = op.Status == AsyncOperationStatus.Succeeded ? op.Result as T : null;
                    if (existingAsset != null)
                    {
                        _assetCache[key] = existingAsset;
                    }
                    else
                    {
                        _assetHandleCache.Remove(key);
                        Addressables.Release(op);
                    }

                    callback?.Invoke(existingAsset);
                };

                return;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
            _assetHandleCache[key] = handle;
            handle.Completed += operation =>
            {
                T asset = operation.Status == AsyncOperationStatus.Succeeded ? operation.Result : null;
                if (asset != null)
                {
                    _assetCache[key] = asset;
                }
                else
                {
                    _assetHandleCache.Remove(key);
                    Addressables.Release(operation);
                }

                callback?.Invoke(asset);
            };
        }

        public void LoadAssets<T>(object key, Action<T> perAssetCallback = null, Action<IList<T>> callback = null)
        {
            Type cacheType = typeof(T);
            ListCacheKey cacheKey = ListCacheKey.Create(key, cacheType);
            if (TryGetCachedAsset(cacheKey, out IList<T> cached))
            {
                callback?.Invoke(cached);
                return;
            }

            if (_assetHandleCache.TryGetValue(cacheKey, out AsyncOperationHandle existing) && existing.IsValid())
            {
                if (existing.IsDone)
                {
                    IList<T> existingAssets = existing.Status == AsyncOperationStatus.Succeeded ? existing.Result as IList<T> : null;
                    if (existingAssets != null)
                    {
                        _assetCache[cacheKey] = existingAssets;
                    }
                    else
                    {
                        _assetHandleCache.Remove(cacheKey);
                        Addressables.Release(existing);
                    }

                    callback?.Invoke(existingAssets);
                    return;
                }

                existing.Completed += op =>
                {
                    IList<T> existingAssets = op.Status == AsyncOperationStatus.Succeeded ? op.Result as IList<T> : null;
                    if (existingAssets != null)
                    {
                        _assetCache[cacheKey] = existingAssets;
                    }
                    else
                    {
                        _assetHandleCache.Remove(cacheKey);
                        Addressables.Release(op);
                    }

                    callback?.Invoke(existingAssets);
                };

                return;
            }

            AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(key, perAssetCallback);
            _assetHandleCache[cacheKey] = handle;
            handle.Completed += operation =>
            {
                IList<T> assets = operation.Status == AsyncOperationStatus.Succeeded ? operation.Result : null;
                if (assets != null)
                {
                    _assetCache[cacheKey] = assets;
                }
                else
                {
                    _assetHandleCache.Remove(cacheKey);
                    Addressables.Release(operation);
                }

                callback?.Invoke(assets);
            };
        }

        public void GetDownloadSize(object key, Action<long> callback)
        {
            AsyncOperationHandle<long> handle = Addressables.GetDownloadSizeAsync(key);
            handle.Completed += operation =>
            {
                long size = operation.Status == AsyncOperationStatus.Succeeded ? operation.Result : 0L;
                callback?.Invoke(size);

                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            };
        }

        public void GetDownloadSize(IEnumerable keys, Addressables.MergeMode mergeMode, Action<long> callback)
        {
            AsyncOperationHandle<IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation>> locationsHandle =
                Addressables.LoadResourceLocationsAsync(keys, mergeMode, typeof(object));
            locationsHandle.Completed += locationsOperation =>
            {
                if (locationsOperation.Status != AsyncOperationStatus.Succeeded || locationsOperation.Result == null)
                {
                    callback?.Invoke(0L);
                    if (locationsHandle.IsValid())
                    {
                        Addressables.Release(locationsHandle);
                    }
                    return;
                }

                AsyncOperationHandle<long> sizeHandle = Addressables.GetDownloadSizeAsync(locationsOperation.Result);
                sizeHandle.Completed += sizeOperation =>
                {
                    long size = sizeOperation.Status == AsyncOperationStatus.Succeeded ? sizeOperation.Result : 0L;
                    callback?.Invoke(size);

                    if (sizeHandle.IsValid())
                    {
                        Addressables.Release(sizeHandle);
                    }

                    if (locationsHandle.IsValid())
                    {
                        Addressables.Release(locationsHandle);
                    }
                };
            };
        }

        public void DownloadDependencies(object key, Action<float> progress = null, Action callback = null, bool autoReleaseHandle = true)
        {
            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(key, autoReleaseHandle);
            TrackDownloadHandle(handle, progress);
            handle.Completed += operation =>
            {
                if (operation.Status == AsyncOperationStatus.Succeeded)
                {
                    callback?.Invoke();
                }

                if (!autoReleaseHandle && handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            };
        }

        public void DownloadDependencies(IEnumerable keys, Addressables.MergeMode mergeMode, Action<float> progress = null, Action callback = null, bool autoReleaseHandle = true)
        {
            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(keys, mergeMode, autoReleaseHandle);
            TrackDownloadHandle(handle, progress);
            handle.Completed += operation =>
            {
                if (operation.Status == AsyncOperationStatus.Succeeded)
                {
                    callback?.Invoke();
                }

                if (!autoReleaseHandle && handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            };
        }

        public void CheckForCatalogUpdates(Action<IList<string>> callback, bool autoReleaseHandle = true)
        {
            AsyncOperationHandle<List<string>> handle = Addressables.CheckForCatalogUpdates(autoReleaseHandle);
            handle.Completed += operation =>
            {
                IList<string> catalogs = operation.Status == AsyncOperationStatus.Succeeded ? operation.Result : null;
                callback?.Invoke(catalogs);

                if (!autoReleaseHandle && handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            };
        }

        public void UpdateCatalogs(Action<IList<IResourceLocator>> callback, bool autoReleaseHandle = true)
        {
            AsyncOperationHandle<List<IResourceLocator>> handle = Addressables.UpdateCatalogs(autoReleaseHandle);
            handle.Completed += operation =>
            {
                IList<IResourceLocator> locators = operation.Status == AsyncOperationStatus.Succeeded ? operation.Result : null;
                callback?.Invoke(locators);

                if (!autoReleaseHandle && handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            };
        }

        public bool TryGetCachedAsset<T>(object key, out T asset) where T : class
        {
            asset = null;

            if (_assetCache.TryGetValue(key, out object cached))
            {
                asset = cached as T;
                return asset != null;
            }

            return false;
        }

        public void ReleaseCachedAsset(object key)
        {
            if (key == null)
            {
                return;
            }

            if (_assetHandleCache.TryGetValue(key, out AsyncOperationHandle handle))
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                _assetHandleCache.Remove(key);
            }

            _assetCache.Remove(key);
        }

        public void ClearCache()
        {
            foreach (AsyncOperationHandle handle in _assetHandleCache.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            _assetHandleCache.Clear();
            _assetCache.Clear();
        }

        private void UpdateInitializeProgress()
        {
            if (!_isInitializing || !_initializeHandle.IsValid())
            {
                return;
            }

            float progress = _initializeHandle.PercentComplete;
            if (Mathf.Approximately(progress, _lastInitializeProgress))
            {
                return;
            }

            _lastInitializeProgress = progress;
            OnInitializeProgress?.Invoke(progress);
        }

        private void UpdateDownloadProgress()
        {
            if (_downloadTrackers.Count == 0)
            {
                return;
            }

            float total = 0.0f;
            int count = 0;

            for (int i = _downloadTrackers.Count - 1; i >= 0; i--)
            {
                DownloadTracker tracker = _downloadTrackers[i];
                AsyncOperationHandle handle = tracker.Handle;
                if (!handle.IsValid())
                {
                    _downloadTrackers.RemoveAt(i);
                    continue;
                }

                total += handle.PercentComplete;
                count++;

                float handleProgress = handle.PercentComplete;
                if (!Mathf.Approximately(handleProgress, tracker.LastProgress))
                {
                    tracker.LastProgress = handleProgress;
                    tracker.Progress?.Invoke(handleProgress);
                }

                if (handle.IsDone)
                {
                    _downloadTrackers.RemoveAt(i);
                }
            }

            if (count == 0)
            {
                return;
            }

            float progress = total / count;
            if (Mathf.Approximately(progress, _lastDownloadProgress))
            {
                return;
            }

            _lastDownloadProgress = progress;
            OnDownloadProgress?.Invoke(progress);
        }

        private void TrackDownloadHandle(AsyncOperationHandle handle, Action<float> progress)
        {
            if (!handle.IsValid())
            {
                return;
            }

            _downloadTrackers.Add(new DownloadTracker
            {
                Handle = handle,
                Progress = progress,
                LastProgress = -1.0f,
            });
        }
    }
}

#endif
