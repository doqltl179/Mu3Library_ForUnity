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

        // Typed on purpose: the untyped handle's Completed wraps every handler it is given, so a
        // handler added through it can never be taken back off.
        private AsyncOperationHandle<IResourceLocator> _initializeHandle;
        private float _lastInitializeProgress = -1.0f;
        private float _lastDownloadProgress = -1.0f;

        private HashSet<object> _locatorKeys = new();
        private readonly Dictionary<object, object> _assetCache = new();
        private readonly Dictionary<object, object> _cachedAssetKeyMap = new();
        private readonly Dictionary<object, AsyncOperationHandle> _assetHandleCache = new();

        /// <summary>
        /// Every cache key a base key produced: the single-asset entries for each requested type,
        /// the list entries, and the keyed-dictionary entries. Releasing by base key walks this,
        /// so nothing a key loaded is left behind.
        /// </summary>
        private readonly Dictionary<object, HashSet<object>> _cacheKeysByBaseKey = new();

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
                // The handle is only detached, never released: Addressables releases its initialization
                // itself once it completes and hands the running one to every caller without adding a
                // reference, so this manager never owns one. Taking the handler off keeps a completion
                // that lands later out of a manager that has already been torn down.
                _initializeHandle.Completed -= OnInitializeCompleted;
            }

            List<CatalogCall> abandonedCatalogCalls = TakeCatalogCallsNotInAddressables();

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

            // Reported last, so a callback that throws cannot leave the teardown half done.
            foreach (CatalogCall call in abandonedCatalogCalls)
            {
                try
                {
                    call.Abandon();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
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

            // The callback belongs to this initialize call only, so it goes through the
            // one-shot subscription instead of staying on the event for every later one.
            SubscribeOnInitializedOnce(callback);

            BeginInitialize();
        }

        public void InitializeWithResult(Action<bool, string> callback)
        {
            if (_isInitialized)
            {
                callback?.Invoke(true, string.Empty);
                return;
            }

            SubscribeOnInitializeResultOnce(callback);

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

            // Set once the handle is there, so an InitializeAsync that throws does not leave every later
            // initialize call and catalog update waiting for an initialization that never started.
            _initializeHandle = InitializeOperationOverride != null
                ? InitializeOperationOverride()
                : Addressables.InitializeAsync();
            _isInitializing = true;

            if (_initializeHandle.IsDone)
            {
                OnInitializeCompleted(_initializeHandle);
                return;
            }

            _initializeHandle.Completed += OnInitializeCompleted;
        }

        private void OnInitializeCompleted(AsyncOperationHandle<IResourceLocator> handle)
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

            try
            {
                OnInitializeProgress?.Invoke(_lastInitializeProgress);

                OnInitialized?.Invoke();
                OnInitializeResult?.Invoke(isSuccess, errorMessage);
            }
            finally
            {
                // After the events, so their handlers see the initialization finished before the update
                // that waited for it goes on, and even when one of them throws.
                ResumeCatalogCallAwaitingInitialization();
            }
        }

        public void LoadAsset<T>(object key, Action<T> callback = null) where T : class
        {
            // The cache key carries the requested type, so one address loaded as two different
            // types holds two independent entries instead of fighting over a single slot.
            object cacheKey = CreateSingleAssetCacheKey<T>(key);
            if (TryGetCachedAssetInternal(cacheKey, out T cached))
            {
                callback?.Invoke(cached);
                return;
            }

            if (TryGetValidCachedHandle(cacheKey, out AsyncOperationHandle existing))
            {
                if (existing.IsDone)
                {
                    callback?.Invoke(FinalizeCachedLoad<T>(key, cacheKey, existing));
                    return;
                }

                existing.Completed += op => callback?.Invoke(FinalizeCachedLoad<T>(key, cacheKey, op));
                return;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
            RegisterHandleCache(key, cacheKey, handle);
            handle.Completed += operation => callback?.Invoke(FinalizeCachedLoad<T>(key, cacheKey, operation));
        }

        public void LoadAssets<T>(object key, Action<T> perAssetCallback = null, Action<IList<T>> callback = null)
        {
            Type cacheType = typeof(T);
            ListCacheKey cacheKey = ListCacheKey.Create(key, cacheType);
            if (TryGetCachedAssetInternal(cacheKey, out IList<T> cached))
            {
                callback?.Invoke(cached);
                return;
            }

            if (TryGetValidCachedHandle(cacheKey, out AsyncOperationHandle existing))
            {
                if (existing.IsDone)
                {
                    callback?.Invoke(FinalizeCachedLoad<IList<T>>(key, cacheKey, existing));
                    return;
                }

                existing.Completed += op => callback?.Invoke(FinalizeCachedLoad<IList<T>>(key, cacheKey, op));
                return;
            }

            AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(key, perAssetCallback);
            RegisterHandleCache(key, cacheKey, handle);
            handle.Completed += operation => callback?.Invoke(FinalizeCachedLoad<IList<T>>(key, cacheKey, operation));
        }

        public void LoadAssetsWithKeys<T>(object key, Action<Dictionary<string, T>> callback = null)
        {
            ListCacheKey cacheKey = ListCacheKey.Create(key, typeof(AssetsWithKeysCacheMarker<T>));
            if (TryGetCachedAssetInternal(cacheKey, out Dictionary<string, T> cached))
            {
                callback?.Invoke(cached);
                return;
            }

            if (TryGetValidCachedHandle(cacheKey, out AsyncOperationHandle existing))
            {
                if (existing.IsDone)
                {
                    Dictionary<string, T> existingAssets = FinalizeAssetsWithKeysLoad<T>(key, cacheKey, existing);
                    callback?.Invoke(existingAssets);
                    return;
                }

                existing.Completed += operation =>
                {
                    Dictionary<string, T> existingAssets = FinalizeAssetsWithKeysLoad<T>(key, cacheKey, operation);
                    callback?.Invoke(existingAssets);
                };
                return;
            }

            AsyncOperationHandle<Dictionary<string, T>> handle = CreateLoadAssetsWithKeysOperation<T>(key);
            RegisterHandleCache(key, cacheKey, handle);
            handle.Completed += operation =>
            {
                Dictionary<string, T> assets = FinalizeAssetsWithKeysLoad<T>(key, cacheKey, operation);
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
                if (operation.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError($"Addressables dependency download failed.\r\n{operation.OperationException?.Message ?? "Unknown download error."}");
                }

                // The callback reports completion, success or not. A failed download must not
                // leave the caller waiting forever; the failure itself is reported above.
                callback?.Invoke();

                if (!autoReleaseHandle && handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            };
        }

        /// <summary>
        /// Checks the remote catalogs for updates. Catalog calls reach Addressables one at a time, in
        /// <br/> call order: a check made while another catalog call runs, from that call's callback
        /// <br/> included, starts once the earlier handle is settled. The result is read inside the
        /// <br/> completion callback, the last moment the handle is valid when Addressables releases it
        /// <br/> itself. A handle still valid on the next ResourceManager update, because
        /// <br/> <paramref name="autoReleaseHandle"/> is false or Addressables returned a chain operation it
        /// <br/> does not release, is released by this manager then, so every handle is released once.
        /// <br/> Disposing the manager reports a call that has not reached Addressables yet with null.
        /// </summary>
        public void CheckForCatalogUpdates(Action<IList<string>> callback, bool autoReleaseHandle = true)
            => EnqueueCatalogCall(new CatalogCall(
                call => StartCheckForCatalogUpdates(call, callback, autoReleaseHandle),
                () => callback?.Invoke(null)));

        /// <summary>
        /// Updates every catalog that has an update. The bundle cache is left alone; use the
        /// <br/> overload that takes <c>autoCleanBundleCache</c> to remove the bundles the updated
        /// <br/> catalogs no longer reference. The manager initializes first as described there.
        /// </summary>
        public void UpdateCatalogs(Action<IList<IResourceLocator>> callback, bool autoReleaseHandle = true)
            => UpdateCatalogs(callback, autoCleanBundleCache: false, autoReleaseHandle: autoReleaseHandle);

        /// <summary>
        /// Updates every catalog that has an update and, when <paramref name="autoCleanBundleCache"/>
        /// <br/> is true, removes the cached bundles the updated catalogs no longer reference. Addressables
        /// <br/> fails the update when it cannot clean the cache, and the callback then receives null.
        /// <br/> The update reaches Addressables only once this manager is initialized, so the check
        /// <br/> Addressables starts for it never waits inside a chain operation nothing releases. A manager
        /// <br/> that has not initialized yet runs the initialization <see cref="Initialize"/> runs, with the
        /// <br/> same <c>IsInitializing</c> state and the same events, and an update made while one runs
        /// <br/> waits for it. When that initialization fails, the callback receives null and Addressables
        /// <br/> is not called. Later updates fail the same way without another attempt, since Addressables
        /// <br/> does not run a failed initialization again, until an initialize call succeeds. The call
        /// <br/> waits for its turn and its handle is released as in <see cref="CheckForCatalogUpdates"/>.
        /// </summary>
        public void UpdateCatalogs(Action<IList<IResourceLocator>> callback, bool autoCleanBundleCache, bool autoReleaseHandle)
            => EnqueueCatalogCall(new CatalogCall(
                call => StartUpdateCatalogs(call, callback, autoCleanBundleCache, autoReleaseHandle),
                () => callback?.Invoke(null)));

        #region Catalog Calls
        /// <summary>
        /// A catalog check or update the manager accepted. Addressables releases the check handle it
        /// <br/> still holds whenever a new check starts, the one an update starts by itself included, so
        /// <br/> catalog calls that overlap there release one handle twice, and the second destroy throws
        /// <br/> out of the ResourceManager update and stops every later one. The manager therefore hands
        /// <br/> them to Addressables one at a time and starts the next only once the previous handle is
        /// <br/> settled.
        /// </summary>
        private sealed class CatalogCall
        {
            private readonly Action<CatalogCall> _start;
            private readonly Action _abandon;

            public CatalogCall(Action<CatalogCall> start, Action abandon)
            {
                _start = start;
                _abandon = abandon;
            }

            /// <summary>Set once Addressables runs the call, which then reports it through the handle.</summary>
            public bool IsInAddressables { get; set; }

            /// <summary>Hands the call to Addressables, or first to the manager's initialization.</summary>
            public void Start() => _start(this);

            /// <summary>Reports the call as failed without it reaching Addressables.</summary>
            public void Abandon() => _abandon();
        }

        private readonly Queue<CatalogCall> _queuedCatalogCalls = new();
        private CatalogCall _activeCatalogCall;
        private CatalogCall _catalogCallAwaitingInitialization;

        private void EnqueueCatalogCall(CatalogCall call)
        {
            if (_activeCatalogCall != null)
            {
                _queuedCatalogCalls.Enqueue(call);
                return;
            }

            StartCatalogCall(call);
        }

        private void StartCatalogCall(CatalogCall call)
        {
            _activeCatalogCall = call;
            try
            {
                call.Start();
            }
            catch (Exception exception)
            {
                if (_activeCatalogCall != call || call.IsInAddressables)
                {
                    // The call has already reported and ended, or Addressables runs it and reports it
                    // later; what threw was a callback that ran meanwhile, such as an initialization
                    // event handler.
                    throw;
                }

                // A call that throws before it reaches Addressables is reported like a failed one, so its
                // caller is not left waiting and the calls queued behind it still get their turn.
                Debug.LogException(exception);
                if (_catalogCallAwaitingInitialization == call)
                {
                    _catalogCallAwaitingInitialization = null;
                }

                FailCatalogCall(call);
            }
        }

        /// <summary>
        /// Ends the active call and gives the next queued one its turn. Runs once the call's handle is
        /// <br/> settled, or once the call failed before it reached Addressables.
        /// </summary>
        private void EndCatalogCall(CatalogCall call)
        {
            if (_activeCatalogCall != call)
            {
                return;
            }

            _activeCatalogCall = null;
            if (_queuedCatalogCalls.Count > 0)
            {
                StartCatalogCall(_queuedCatalogCalls.Dequeue());
            }
        }

        private void FailCatalogCall(CatalogCall call)
        {
            try
            {
                call.Abandon();
            }
            finally
            {
                EndCatalogCall(call);
            }
        }

        private void ResumeCatalogCallAwaitingInitialization()
        {
            CatalogCall call = _catalogCallAwaitingInitialization;
            if (call == null)
            {
                return;
            }

            _catalogCallAwaitingInitialization = null;
            StartCatalogCall(call);
        }

        /// <summary>
        /// Takes out the catalog calls that have not reached Addressables yet, the one waiting for the
        /// <br/> initialization first, for Dispose to report as failed. A call Addressables is running stays
        /// <br/> active: it completes and releases its handle as before, and a call made later waits for it.
        /// </summary>
        private List<CatalogCall> TakeCatalogCallsNotInAddressables()
        {
            List<CatalogCall> calls = new();
            if (_catalogCallAwaitingInitialization != null)
            {
                calls.Add(_catalogCallAwaitingInitialization);
                _catalogCallAwaitingInitialization = null;
                _activeCatalogCall = null;
            }

            calls.AddRange(_queuedCatalogCalls);
            _queuedCatalogCalls.Clear();
            return calls;
        }

        private void StartCheckForCatalogUpdates(CatalogCall call, Action<IList<string>> callback, bool autoReleaseHandle)
        {
            AsyncOperationHandle<List<string>> handle = Addressables.CheckForCatalogUpdates(autoReleaseHandle);
            LastCatalogHandle = handle;
            handle.Completed += operation =>
            {
                try
                {
                    IList<string> catalogs = operation.Status == AsyncOperationStatus.Succeeded ? operation.Result : null;
                    callback?.Invoke(catalogs);
                }
                finally
                {
                    SettleCatalogHandle(call, handle);
                }
            };
            call.IsInAddressables = true;
        }

        private void StartUpdateCatalogs(CatalogCall call, Action<IList<IResourceLocator>> callback, bool autoCleanBundleCache, bool autoReleaseHandle)
        {
            if (!_isInitialized)
            {
                if (!_isInitializing && !string.IsNullOrEmpty(_initializeError))
                {
                    FailCatalogCall(call);
                    return;
                }

                // OnInitializeCompleted starts the call again, possibly before BeginInitialize returns.
                _catalogCallAwaitingInitialization = call;
                BeginInitialize();
                return;
            }

            AsyncOperationHandle<List<IResourceLocator>> handle =
                Addressables.UpdateCatalogs(autoCleanBundleCache, catalogs: null, autoReleaseHandle: autoReleaseHandle);
            LastCatalogHandle = handle;
            handle.Completed += operation =>
            {
                try
                {
                    IList<IResourceLocator> locators = operation.Status == AsyncOperationStatus.Succeeded ? operation.Result : null;
                    if (locators != null)
                    {
                        RefreshLocatorKeys();
                    }

                    callback?.Invoke(locators);
                }
                finally
                {
                    SettleCatalogHandle(call, handle);
                }
            };
            call.IsInAddressables = true;
        }

        /// <summary>
        /// Settles a catalog handle once its callback has run, then ends the call. With auto-release,
        /// <br/> Addressables releases the catalog operation itself, but when it first has to wait, for its
        /// <br/> initialization or, in an update that knows of no catalog update yet, for the check it
        /// <br/> starts, it returns a chain operation around that one, and nothing releases the chain.
        /// <br/> Every handle is still valid inside the completion callback, so the two cannot be told
        /// <br/> apart there; on the next ResourceManager update an auto-released handle is gone, and one
        /// <br/> that is still valid, a chain or a handle asked for without auto-release, is released.
        /// <br/> Only then does the next call start, so a check it begins finds no earlier check handle
        /// <br/> for Addressables to release.
        /// </summary>
        private void SettleCatalogHandle(CatalogCall call, AsyncOperationHandle handle)
        {
            // A callback added to an operation that has already finished runs on the next ResourceManager
            // update, after Addressables has finished completing the catalog operation. Releasing the
            // chain any earlier, inside its own completion, would release the auto-released operation it
            // wraps a second time when the chain is destroyed.
            AsyncOperationHandle<bool> afterCompletion = Addressables.ResourceManager.CreateCompletedOperation(true, null);
            afterCompletion.Completed += _ =>
            {
                try
                {
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }
                finally
                {
                    EndCatalogCall(call);
                }
            };
            afterCompletion.ReleaseHandleOnCompletion();
        }
        #endregion

        /// <summary>
        /// Rebuilds the known-key set from every locator Addressables currently holds, so
        /// <see cref="IsKeyExist"/> answers for the keys a catalog update brought in as well.
        /// </summary>
        private void RefreshLocatorKeys()
        {
            HashSet<object> keys = new();
            foreach (IResourceLocator locator in Addressables.ResourceLocators)
            {
                if (locator?.Keys == null)
                {
                    continue;
                }

                keys.UnionWith(locator.Keys);
            }

            _locatorKeys = keys;
        }

        #region Diagnostics
        /// <summary>Number of cached asset entries, for diagnostics.</summary>
        public int CachedAssetCount => _assetCache.Count;

        /// <summary>Number of tracked load handles, pending loads included, for diagnostics.</summary>
        public int TrackedHandleCount => _assetHandleCache.Count;

        /// <summary>The base keys the cache currently holds anything for, for diagnostics.</summary>
        public IReadOnlyCollection<object> CachedBaseKeys => _cacheKeysByBaseKey.Keys;

        /// <summary>
        /// The handle Addressables returned for the latest catalog check or update, for tests. It holds
        /// <br/> no reference of its own, so it reads invalid once the operation has been released.
        /// </summary>
        internal AsyncOperationHandle LastCatalogHandle { get; private set; }

        /// <summary>
        /// Starts the initialization in place of <c>Addressables.InitializeAsync()</c>, for tests that need
        /// <br/> one Addressables does not produce on demand, still running or failed. Like the operation it
        /// <br/> stands in for, it must release itself once it completes; this manager never releases it.
        /// </summary>
        internal Func<AsyncOperationHandle<IResourceLocator>> InitializeOperationOverride { get; set; }
        #endregion

        public bool IsKeyExist(object key)
        {
            if (key == null)
            {
                return false;
            }
            else if (_cacheKeysByBaseKey.ContainsKey(key))
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
            return TryGetCachedAssetInternal(CreateSingleAssetCacheKey<T>(key), out asset);
        }

        private bool TryGetCachedAssetInternal<T>(object cacheKey, out T asset) where T : class
        {
            asset = null;

            if (_assetCache.TryGetValue(cacheKey, out object cached))
            {
                asset = cached as T;
                return asset != null;
            }

            return false;
        }

        private static object CreateSingleAssetCacheKey<T>(object key)
            => ListCacheKey.Create(key, typeof(SingleAssetCacheMarker<T>));

        private void RegisterHandleCache(object baseKey, object cacheKey, AsyncOperationHandle handle)
        {
            _assetHandleCache[cacheKey] = handle;
            TrackCacheKey(baseKey, cacheKey);
        }

        private void CacheLoadedAsset(object baseKey, object cacheKey, object asset)
        {
            _assetCache[cacheKey] = asset;

            // The address map answers with what the caller asked to load, so it keeps the
            // base key and never the composite cache key.
            _cachedAssetKeyMap[asset] = baseKey;

            TrackCacheKey(baseKey, cacheKey);
        }

        private void TrackCacheKey(object baseKey, object cacheKey)
        {
            if (!_cacheKeysByBaseKey.TryGetValue(baseKey, out HashSet<object> cacheKeys))
            {
                cacheKeys = new HashSet<object>();
                _cacheKeysByBaseKey.Add(baseKey, cacheKeys);
            }

            cacheKeys.Add(cacheKey);
        }

        private void UntrackCacheKey(object baseKey, object cacheKey)
        {
            if (_cacheKeysByBaseKey.TryGetValue(baseKey, out HashSet<object> cacheKeys))
            {
                cacheKeys.Remove(cacheKey);
                if (cacheKeys.Count == 0)
                {
                    _cacheKeysByBaseKey.Remove(baseKey);
                }
            }
        }

        /// <summary>
        /// Settles a finished single or list load: a hit is cached, a miss takes its handle out
        /// of the cache and releases it. The handle is touched only while the cache still tracks
        /// this very handle, so a release-and-reload that happened meanwhile is left alone.
        /// </summary>
        private T FinalizeCachedLoad<T>(object baseKey, object cacheKey, AsyncOperationHandle handle) where T : class
        {
            if (!handle.IsValid())
            {
                return null;
            }

            T asset = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result as T : null;
            bool isTrackedHandle = _assetHandleCache.TryGetValue(cacheKey, out AsyncOperationHandle cachedHandle) &&
                cachedHandle.Equals(handle);

            if (asset != null)
            {
                if (isTrackedHandle)
                {
                    CacheLoadedAsset(baseKey, cacheKey, asset);
                }
            }
            else if (isTrackedHandle)
            {
                _assetHandleCache.Remove(cacheKey);
                UntrackCacheKey(baseKey, cacheKey);
                Addressables.Release(cachedHandle);
            }

            return asset;
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

            // Everything the key produced goes together: the single-asset entries for each
            // requested type, the list entries, and the keyed-dictionary entries.
            if (!_cacheKeysByBaseKey.TryGetValue(key, out HashSet<object> cacheKeys))
            {
                return;
            }

            _cacheKeysByBaseKey.Remove(key);

            foreach (object cacheKey in cacheKeys)
            {
                if (_assetHandleCache.TryGetValue(cacheKey, out AsyncOperationHandle handle))
                {
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }

                    _assetHandleCache.Remove(cacheKey);
                }

                if (_assetCache.TryGetValue(cacheKey, out object cachedAsset))
                {
                    _cachedAssetKeyMap.Remove(cachedAsset);
                    _assetCache.Remove(cacheKey);
                }
            }
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
            _cacheKeysByBaseKey.Clear();
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

        private Dictionary<string, T> FinalizeAssetsWithKeysLoad<T>(object baseKey, object cacheKey, AsyncOperationHandle handle)
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
                    CacheLoadedAsset(baseKey, cacheKey, assets);
                }
            }
            else
            {
                if (_assetHandleCache.TryGetValue(cacheKey, out AsyncOperationHandle cachedHandle) && cachedHandle.Equals(handle))
                {
                    _assetHandleCache.Remove(cacheKey);
                    UntrackCacheKey(baseKey, cacheKey);
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

        private sealed class SingleAssetCacheMarker<T>
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
