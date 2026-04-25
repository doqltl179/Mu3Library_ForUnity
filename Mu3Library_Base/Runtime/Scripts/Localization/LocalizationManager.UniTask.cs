#if MU3LIBRARY_LOCALIZATION_SUPPORT && MU3LIBRARY_UNITASK_SUPPORT
using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
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

            if (_isInitializing)
            {
                await _initializeHandle.ToUniTask();
                return;
            }

            _isInitializing = true;
            _initializeHandle = LocalizationSettings.InitializationOperation;
            if (_initializeHandle.IsDone)
            {
                OnInitializeCompleted(_initializeHandle);
                return;
            }

            await _initializeHandle.ToUniTask();
            OnInitializeCompleted(_initializeHandle);
        }

        public async UniTask<string> GetStringAsync(string tableName, string key)
        {
            LocalizedStringDatabase stringDatabase = LocalizationSettings.StringDatabase;
            if (stringDatabase == null)
            {
                return "";
            }

            AsyncOperationHandle<StringTable> handle = stringDatabase.GetTableAsync(tableName);
            await handle.ToUniTask();

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                return "";
            }

            StringTable table = handle.Result;
            StringTableEntry entry = table != null ? table.GetEntry(key) : null;
            return entry != null ? entry.LocalizedValue : "";
        }

        public async UniTask ChangeLocaleToNativeAsync()
        {
            SystemLanguage sl = Application.systemLanguage;
            await ChangeLocaleWithEnglishNameAsync(sl.ToString());
        }

        public async UniTask ChangeLocaleWithEnglishNameAsync(string englishName)
        {
            Locale locale = LocalizationSettings.AvailableLocales.Locales
                .Where(t => t.Identifier.CultureInfo.EnglishName == englishName)
                .FirstOrDefault();

            await ChangeLocaleAsync(locale);
        }

        public async UniTask ChangeLocaleAsync(Locale locale)
        {
            if (locale == null)
            {
                return;
            }

            _localeChangeCts?.Cancel();
            _localeChangeCts?.Dispose();
            _localeChangeCts = new CancellationTokenSource();
            CancellationToken token = _localeChangeCts.Token;

            _isLocaleChanging = true;
            try
            {
                LocalizationSettings.SelectedLocale = locale;
                await LocalizationSettings.SelectedLocaleAsync.ToUniTask(cancellationToken: token);

                _currentLocale = locale;
            }
            catch (OperationCanceledException)
            {
                // Cancelled — _currentLocale is not updated
            }
            finally
            {
                _isLocaleChanging = false;
            }
        }

        public void CancelChangeLocale()
        {
            _localeChangeCts?.Cancel();
        }

        public async UniTask<Locale> GetSelectedLocaleAsync()
        {
            AsyncOperationHandle<Locale> handle = LocalizationSettings.SelectedLocaleAsync;
            if (!handle.IsDone)
            {
                await handle.ToUniTask();
            }

            return handle.Status == AsyncOperationStatus.Succeeded
                ? handle.Result
                : _defaultLocale;
        }
    }
}
#endif
