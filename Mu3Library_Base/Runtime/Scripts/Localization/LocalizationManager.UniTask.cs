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
                if (!SelectLocale(locale))
                {
                    return;
                }

                await LocalizationSettings.SelectedLocaleAsync.ToUniTask(cancellationToken: cts.Token);
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
                }

                cts.Dispose();
            }
        }

        public void CancelChangeLocale()
        {
            CancellationTokenSource cts = _localeChangeCts;
            if (cts == null)
            {
                return;
            }

            _localeChangeCts = null;
            _isLocaleChanging = false;

            cts.Cancel();
        }

        partial void DisposeLocaleChange()
        {
            CancelChangeLocale();

            _isLocaleChanging = false;
        }
    }
}
#endif
