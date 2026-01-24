#if MU3LIBRARY_LOCALIZATION_SUPPORT && MU3LIBRARY_UNITASK_SUPPORT
using System;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Mu3Library.Localization
{
    public partial class LocalizationManager
    {
        public async UniTask InitializeAsync(Action<float> progress = null)
        {
            if (_isInitialized)
            {
                progress?.Invoke(1.0f);
                return;
            }

            if (progress != null)
            {
                _initializeProgressCallbacks.Add(progress);
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
