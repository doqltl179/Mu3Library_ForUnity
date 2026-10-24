#if MU3LIBRARY_LOCALIZATION_SUPPORT && MU3LIBRARY_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mu3Library.Localization.Data;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Mu3Library.Localization
{
    public partial class LocalizationManager
    {
        private bool _isLocaleChanging = false;
        public bool IsLocaleChanging => _isLocaleChanging;

        private CancellationTokenSource _localeChangeCts;

        /// <summary>
        /// The locale the running <see cref="ChangeLocaleAsync"/> waits to see announced, and whether
        /// <br/> the package has announced it since that call selected it.
        /// </summary>
        private Locale _awaitedLocale;
        private bool _isAwaitedLocaleAnnounced = false;



        public async UniTask InitializeAsync()
        {
            if (_isInitialized)
            {
                OnInitializeProgress?.Invoke(1.0f);
                return;
            }

            // BeginInitialize owns acquiring the handle and attaching the completion handler for
            // the callback and the UniTask paths alike, and leaves a running initialization alone.
            BeginInitialize();

            AsyncOperationHandle<LocalizationSettings> handle = _initializeHandle;
            if (_isInitializing && handle.IsValid() && !handle.IsDone)
            {
                try
                {
                    // Mu3Library does not reference UniTask.Addressables, so the handle awaits in this
                    // file bind to UniTask's IEnumerator extension, which waits for IsDone and does not
                    // throw for a failed handle; the status read after each await decides the result.
                    // OnInitializeCompleted logs a failed initialization and reports it through
                    // InitializeError and OnInitializeResult. The catches keep each path's answer the
                    // same should the handle extension ever be the one that binds.
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

        public async UniTask<string> GetStringAsync(string tableName, string key)
        {
            LocalizedStringDatabase stringDatabase = GetStringDatabase();
            if (stringDatabase == null)
            {
                return string.Empty;
            }

            AsyncOperationHandle<StringTable> handle = stringDatabase.GetTableAsync(tableName);
            if (!handle.IsDone)
            {
                try
                {
                    await handle.ToUniTask();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Failed to load the string table. table: {tableName}\r\n{exception.Message}");
                    return string.Empty;
                }
            }

            return GetTableEntryValue(ResolveTable(handle), key);
        }

        public async UniTask<T> GetAssetAsync<T>(string tableName, string key) where T : UnityEngine.Object
        {
            LocalizedAssetDatabase assetDatabase = GetAssetDatabase();
            if (assetDatabase == null)
            {
                return null;
            }

            AsyncOperationHandle<T> handle = assetDatabase.GetLocalizedAssetAsync<T>(tableName, key);
            if (!handle.IsDone)
            {
                try
                {
                    await handle.ToUniTask();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Failed to load the localized asset. table: {tableName}, key: {key}\r\n{exception.Message}");
                    return null;
                }
            }

            return ResolveAsset(handle);
        }

        public UniTask<string> GetStringAsync(EntryData entryData)
        {
            if (entryData == null)
            {
                Debug.LogError("EntryData is null.");
                return UniTask.FromResult(string.Empty);
            }

            return GetStringAsync(entryData.TableName, entryData.Key);
        }

        public async UniTask<Locale> GetSelectedLocaleAsync()
        {
            if (!LocalizationSettings.HasSettings)
            {
                return _defaultLocale;
            }

            // The property builds the handle on demand, so it is read once and that one handle
            // is what gets waited on.
            AsyncOperationHandle<Locale> handle = LocalizationSettings.SelectedLocaleAsync;
            if (!handle.IsDone)
            {
                try
                {
                    await handle.ToUniTask();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Failed to resolve the selected locale.\r\n{exception.Message}");
                    return _defaultLocale;
                }
            }

            return ResolveSelectedLocale(handle);
        }

        public async UniTask ChangeLocaleToNativeAsync()
        {
            await ChangeLocaleAsync(FindNativeLocale());
        }

        public async UniTask ChangeLocaleWithEnglishNameAsync(string englishName)
        {
            await ChangeLocaleAsync(FindLocaleByEnglishName(englishName));
        }

        public async UniTask ChangeLocaleAsync(Locale locale)
        {
            if (locale == null)
            {
                return;
            }

            CancelChangeLocale();

            CancellationTokenSource cts = new();
            _localeChangeCts = cts;
            _isLocaleChanging = true;

            try
            {
                // Selecting another locale makes the package start its initialization again, table
                // preloading included, and announce the change through SelectedLocaleChanged only
                // once that is over, in the same frame or a later one. The selected-locale handle is
                // already complete when the selection returns, so it says nothing about either. A
                // selection the package ignores, such as the locale already selected, restarts
                // nothing and announces nothing.
                AsyncOperationHandle<LocalizationSettings> previousInitialization =
                    LocalizationSettings.HasSettings ? LocalizationSettings.InitializationOperation : default;

                _awaitedLocale = locale;
                _isAwaitedLocaleAnnounced = false;

                if (!SelectLocale(locale))
                {
                    return;
                }

                AsyncOperationHandle<LocalizationSettings> initialization = LocalizationSettings.InitializationOperation;
                if (initialization.Equals(previousInitialization))
                {
                    return;
                }

                if (!_isAwaitedLocaleAnnounced || !initialization.IsDone)
                {
                    await UniTask.WaitUntil(
                        () => _isAwaitedLocaleAnnounced && initialization.IsDone,
                        cancellationToken: cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("Locale change canceled.");
            }
            finally
            {
                // Only the call that still owns the token source settles the state. A superseded
                // call must not report that the newer one it was replaced by has finished.
                if (ReferenceEquals(_localeChangeCts, cts))
                {
                    _localeChangeCts = null;
                    _isLocaleChanging = false;
                    _awaitedLocale = null;
                }

                cts.Dispose();
            }
        }

        /// <summary>
        /// Stops the wait of a running <see cref="ChangeLocaleAsync"/>, which then completes. The locale
        /// <br/> it selected stays selected, and the package still finishes its initialization and
        /// <br/> announces the change through <see cref="OnLocaleChanged"/>.
        /// </summary>
        public void CancelChangeLocale()
        {
            CancellationTokenSource cts = _localeChangeCts;
            if (cts == null)
            {
                return;
            }

            _localeChangeCts = null;
            _isLocaleChanging = false;
            _awaitedLocale = null;

            cts.Cancel();
        }

        partial void TrackLocaleAnnouncement(Locale locale)
        {
            if (_awaitedLocale != null && ReferenceEquals(locale, _awaitedLocale))
            {
                _isAwaitedLocaleAnnounced = true;
            }
        }

        partial void DisposeLocaleChange()
        {
            CancelChangeLocale();

            _isLocaleChanging = false;
        }
    }
}
#endif
