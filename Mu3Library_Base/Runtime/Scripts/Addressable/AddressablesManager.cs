#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using System;
using System.Collections;
using System.Collections.Generic;
using Mu3Library.DI;
using Mu3Library.Foundation.Event;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Mu3Library.Addressable
{
    public partial class AddressablesManager : IAddressablesManager, IAddressablesManagerEventBus, IUpdatable, IDisposable
    {
        private bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        private string _initializeError = string.Empty;
        public string InitializeError => _initializeError;

        private bool _isInitializing = false;
        public bool IsInitializing => _isInitializing;

        private AsyncOperationHandle _initializeHandle;
        private float _lastInitializeProgress = -1.0f;
        private float _lastDownloadProgress = -1.0f;

        private HashSet<object> _locatorKeys = new();
        private readonly Dictionary<object, object> _assetCache = new();
        private readonly Dictionary<object, object> _cachedAssetKeyMap = new();
        private readonly Dictionary<object, AsyncOperationHandle> _assetHandleCache = new();

        private sealed class DownloadTracker
        {
            public AsyncOperationHandle Handle;
            public Action<float> Progress;
            public float LastProgress = -1.0f;
        }
        private readonly List<DownloadTracker> _downloadTrackers = new();
        private readonly SubscribeHandler _subscribeHandler = new();

        public event Action OnInitialized;
        public event Action<bool, string> OnInitializeResult;
        public event Action<float> OnInitializeProgress;
        public event Action<float> OnDownloadProgress;



        public void Dispose()
        {
            _subscribeHandler.Dispose();

            if (_initializeHandle.IsValid())
            {
                Addressables.Release(_initializeHandle);
            }

            ClearCache();

            _downloadTrackers.Clear();
            _locatorKeys.Clear();

            _isInitialized = false;
            _isInitializing = false;
            _lastDownloadProgress = -1.0f;
            _initializeError = string.Empty;

            OnInitialized = null;
            OnInitializeResult = null;
            OnInitializeProgress = null;
            OnDownloadProgress = null;
        }

        public void Update()
        {
            UpdateInitializeProgress();
            UpdateDownloadProgress();
        }

        public ISubscriptionInfo SubscribeOnInitializedOnce(Action callback)
            => SubscribeOnInitializedOnce(callback, null);

        public ISubscriptionInfo SubscribeOnInitializedOnce(Action callback, Action onDisposed)
            => _subscribeHandler.SubscribeOnce(
                handler => OnInitialized += handler,
                handler => OnInitialized -= handler,
                callback,
                onDisposed);

        public ISubscriptionInfo SubscribeOnInitializeResultOnce(Action<bool, string> callback)
            => SubscribeOnInitializeResultOnce(callback, null);

        public ISubscriptionInfo SubscribeOnInitializeResultOnce(Action<bool, string> callback, Action onDisposed)
            => _subscribeHandler.SubscribeOnce(
                handler => OnInitializeResult += handler,
                handler => OnInitializeResult -= handler,
                callback,
                onDisposed);

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

            BeginInitialize();
        }

        public void InitializeWithResult(Action<bool, string> callback)
        {
            if (_isInitialized)
            {
                callback?.Invoke(true, string.Empty);
                return;
            }

            if (callback != null)
            {
                OnInitializeResult += callback;
            }

            BeginInitialize();
        }

        private void BeginInitialize()
        {
            if (_isInitialized)
            {
                return;
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
            bool isSuccess = false;
            string errorMessage = string.Empty;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _isInitialized = true;
                isSuccess = true;
                errorMessage = string.Empty;

                if (handle.Result is IResourceLocator locator)
                {
                    _locatorKeys = new HashSet<object>(locator.Keys);
                }

                Debug.Log("Addressables initialized.");
            }
            else
            {
                _isInitialized = false;
                isSuccess = false;
                errorMessage = handle.OperationException?.Message ?? "Unknown initialization error.";
                Debug.LogError($"Addressables initialize failed.\r\n{errorMessage}");
            }

            _isInitializing = false;
            _lastInitializeProgress = handle.PercentComplete;
            _initializeError = errorMessage;

            OnInitializeProgress?.Invoke(_lastInitializeProgress);

            OnInitialized?.Invoke();
            OnInitializeResult?.Invoke(isSuccess, errorMessage);
        }

        public void LoadAsset<T>(object key, Action<T> callback = null) where T : class
        {
            if (TryGetCachedAsset(key, out T cached))
            {
                callback?.Invoke(cached);
                return;
            }

            if (TryGetValidCachedHandle(key, out AsyncOperationHandle existing))
            {
                if (existing.IsDone)
                {
                    T existingAsset = existing.Status == AsyncOperationStatus.Succeeded ? existing.Result as T : null;
                    if (existingAsset != null)
                    {
                        CacheLoadedAsset(key, existingAsset);
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
                        CacheLoadedAsset(key, existingAsset);
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
                    CacheLoadedAsset(key, asset);
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

            if (TryGetValidCachedHandle(cacheKey, out AsyncOperationHandle existing))
            {
                if (existing.IsDone)
                {
                    IList<T> existingAssets = existing.Status == AsyncOperationStatus.Succeeded ? existing.Result as IList<T> : null;
                    if (existingAssets != null)
                    {
                        CacheLoadedAsset(cacheKey, existingAssets);
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
                        CacheLoadedAsset(cacheKey, existingAssets);
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
                    CacheLoadedAsset(cacheKey, assets);
                }
                else
                {
                    _assetHandleCache.Remove(cacheKey);
                    Addressables.Release(operation);
                }

                callback?.Invoke(assets);
            };
        }

        public void LoadAssetsWithKeys<T>(object key, Action<Dictionary<string, T>> callback = null)
        {
            ListCacheKey cacheKey = ListCacheKey.Create(key, typeof(AssetsWithKeysCacheMarker<T>));
            if (TryGetCachedAsset(cacheKey, out Dictionary<string, T> cached))
            {
                callback?.Invoke(cached);
                return;
            }

            if (TryGetValidCachedHandle(cacheKey, out AsyncOperationHandle existing))
            {
                if (existing.IsDone)
                {
                    Dictionary<string, T> existingAssets = FinalizeAssetsWithKeysLoad<T>(cacheKey, existing);
                    callback?.Invoke(existingAssets);
                    return;
                }

                existing.Completed += operation =>
                {
                    Dictionary<string, T> existingAssets = FinalizeAssetsWithKeysLoad<T>(cacheKey, operation);
                    callback?.Invoke(existingAssets);
                };
                return;
            }

            AsyncOperationHandle<Dictionary<string, T>> handle = CreateLoadAssetsWithKeysOperation<T>(key);
            _assetHandleCache[cacheKey] = handle;
            handle.Completed += operation =>
            {
                Dictionary<string, T> assets = FinalizeAssetsWithKeysLoad<T>(cacheKey, operation);
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
            TrackAndCompleteDownload(handle, progress, callback, autoReleaseHandle);
        }

        public void DownloadDependencies(IEnumerable keys, Addressables.MergeMode mergeMode, Action<float> progress = null, Action callback = null, bool autoReleaseHandle = true)
        {
            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(keys, mergeMode, autoReleaseHandle);
            TrackAndCompleteDownload(handle, progress, callback, autoReleaseHandle);
        }

        private void TrackAndCompleteDownload(AsyncOperationHandle handle, Action<float> progress, Action callback, bool autoReleaseHandle)
        {
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

        public bool IsKeyExist(object key)
        {
            if (key == null)
            {
                return false;
            }
            else if (_assetCache.ContainsKey(key))
            {
                return true;
            }

            return _locatorKeys.Contains(key);
        }

        public string GetCachedAddress(object asset)
            => GetCachedAddress<string>(asset);

        public T GetCachedAddress<T>(object asset) where T : class
        {
            if (asset == null)
            {
                return null;
            }

            if (_cachedAssetKeyMap.TryGetValue(asset, out object key))
            {
                return key as T;
            }

            return null;
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

        private void CacheLoadedAsset(object cacheKey, object asset)
        {
            _assetCache[cacheKey] = asset;
            _cachedAssetKeyMap[asset] = cacheKey;
        }

        private bool TryGetValidCachedHandle(object key, out AsyncOperationHandle handle)
        {
            return _assetHandleCache.TryGetValue(key, out handle) && handle.IsValid();
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

            if (_assetCache.TryGetValue(key, out object cachedAsset))
            {
                _cachedAssetKeyMap.Remove(cachedAsset);
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
            _cachedAssetKeyMap.Clear();
        }

        private AsyncOperationHandle<Dictionary<string, T>> CreateLoadAssetsWithKeysOperation<T>(object key)
        {
            AsyncOperationHandle<IList<IResourceLocation>> locationsHandle =
                Addressables.LoadResourceLocationsAsync(key, typeof(T));

            AsyncOperationHandle<Dictionary<string, T>> operation =
                Addressables.ResourceManager.CreateChainOperation<Dictionary<string, T>, IList<IResourceLocation>>(
                    locationsHandle,
                    locationsOperation =>
                    {
                        if (locationsOperation.Status != AsyncOperationStatus.Succeeded || locationsOperation.Result == null)
                        {
                            return CreateFailedLoadAssetsWithKeysOperation<T>("Failed to resolve Addressables resource locations.");
                        }

                        IList<IResourceLocation> locations = locationsOperation.Result;
                        if (!TryValidatePrimaryKeys(locations, out string errorMessage))
                        {
                            return CreateFailedLoadAssetsWithKeysOperation<T>(errorMessage);
                        }

                        if (locations.Count == 0)
                        {
                            return Addressables.ResourceManager.CreateCompletedOperation(new Dictionary<string, T>(), null);
                        }

                        List<AsyncOperationHandle> loadOperations = new(locations.Count);
                        Dictionary<string, AsyncOperationHandle<T>> operationsByKey = new(locations.Count);
                        foreach (IResourceLocation location in locations)
                        {
                            string primaryKey = location.PrimaryKey;
                            AsyncOperationHandle<T> loadHandle = Addressables.LoadAssetAsync<T>(location);
                            loadOperations.Add(loadHandle);
                            operationsByKey.Add(primaryKey, loadHandle);
                        }

                        AsyncOperationHandle<IList<AsyncOperationHandle>> groupHandle =
                            Addressables.ResourceManager.CreateGenericGroupOperation(loadOperations, true);

                        AsyncOperationHandle<Dictionary<string, T>> groupOperationHandle =
                            Addressables.ResourceManager.CreateChainOperation<Dictionary<string, T>, IList<AsyncOperationHandle>>(
                                groupHandle,
                                groupOperation =>
                                {
                                    if (groupOperation.Status != AsyncOperationStatus.Succeeded)
                                    {
                                        return CreateFailedLoadAssetsWithKeysOperation<T>("Failed to load one or more Addressables assets.");
                                    }

                                    Dictionary<string, T> assets = new(operationsByKey.Count);
                                    foreach (KeyValuePair<string, AsyncOperationHandle<T>> operationByKey in operationsByKey)
                                    {
                                        assets.Add(operationByKey.Key, operationByKey.Value.Result);
                                    }

                                    return Addressables.ResourceManager.CreateCompletedOperation(assets, null);
                                });

                        Addressables.Release(groupHandle);
                        return groupOperationHandle;
                    });

            Addressables.Release(locationsHandle);
            return operation;
        }

        private Dictionary<string, T> FinalizeAssetsWithKeysLoad<T>(object cacheKey, AsyncOperationHandle handle)
        {
            if (!handle.IsValid())
            {
                return null;
            }

            Dictionary<string, T> assets = handle.Status == AsyncOperationStatus.Succeeded
                ? handle.Result as Dictionary<string, T>
                : null;
            if (assets != null)
            {
                if (_assetHandleCache.TryGetValue(cacheKey, out AsyncOperationHandle cachedHandle) && cachedHandle.Equals(handle))
                {
                    CacheLoadedAsset(cacheKey, assets);
                }
            }
            else
            {
                if (_assetHandleCache.TryGetValue(cacheKey, out AsyncOperationHandle cachedHandle) && cachedHandle.Equals(handle))
                {
                    _assetHandleCache.Remove(cacheKey);
                    Addressables.Release(cachedHandle);
                }
            }

            return assets;
        }

        private AsyncOperationHandle<Dictionary<string, T>> CreateFailedLoadAssetsWithKeysOperation<T>(string errorMessage)
        {
            return Addressables.ResourceManager.CreateCompletedOperation<Dictionary<string, T>>(null, errorMessage);
        }

        private static bool TryValidatePrimaryKeys(IList<IResourceLocation> locations, out string errorMessage)
        {
            HashSet<string> primaryKeys = new();
            foreach (IResourceLocation location in locations)
            {
                if (location == null || string.IsNullOrEmpty(location.PrimaryKey))
                {
                    errorMessage = "LoadAssetsWithKeys requires every resource location to have a non-empty PrimaryKey.";
                    return false;
                }

                if (!primaryKeys.Add(location.PrimaryKey))
                {
                    errorMessage = "LoadAssetsWithKeys requires unique resource location PrimaryKey values.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        private sealed class AssetsWithKeysCacheMarker<T>
        {
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

                float handleProgress = handle.PercentComplete;
                total += handleProgress;
                count++;

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
