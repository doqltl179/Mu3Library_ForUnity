#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mu3Library.Utility;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Mu3Library.Localization
{
    public class LocalizationManager : GenericSingleton<LocalizationManager>
    {
        private bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        public Locale CurrentLocale => LocalizationSettings.SelectedLocale;

        private IEnumerator _initializeCoroutine = null;



        #region Utilitiy
        public void GetStringAsync(string tableName, string key, Action<string> callback = null)
        {
            StartCoroutine(GetStringAsyncCoroutine(tableName, key, callback));
        }

        public string GetString(string tableName, string key)
        {
            LocalizedStringDatabase sdb = LocalizationSettings.StringDatabase;
            StringTable table = sdb != null ? sdb.GetTable(tableName) : null;
            StringTableEntry entry = table != null ? table.GetEntry(key) : null;
            return entry != null ? entry.LocalizedValue : "";
        }

        public void ChangeLocaleToNative()
        {
            SystemLanguage sl = Application.systemLanguage;
            ChangeLocaleWithEnglishName(sl.ToString());
        }

        public void ChangeLocaleWithEnglishName(string englishName)
        {
            Locale locale = LocalizationSettings.AvailableLocales.Locales
                .Where(t => t.Identifier.CultureInfo.EnglishName == englishName)
                .FirstOrDefault();

            ChangeLocale(locale);
        }

        public void ChangeLocale(Locale locale)
        {
            if (locale == null)
            {
                return;
            }

            LocalizationSettings.SelectedLocale = locale;
        }

        public List<Locale> GetAvailableLocales()
        {
            return LocalizationSettings.AvailableLocales.Locales;
        }

        public void RemoveLocaleChangedEvent(Action<Locale> action)
        {
            LocalizationSettings.SelectedLocaleChanged -= action;
        }

        public void AddLocaleChangedEvent(Action<Locale> action)
        {
            LocalizationSettings.SelectedLocaleChanged += action;
        }

        public void Initialize(Action callback = null)
        {
            if (_initializeCoroutine == null)
            {
                _initializeCoroutine = InitializeCoroutine(callback);
                StartCoroutine(_initializeCoroutine);
            }
        }
        #endregion

        private IEnumerator GetStringAsyncCoroutine(string tableName, string key, Action<string> callback = null)
        {
            LocalizedStringDatabase stringDatabase = LocalizationSettings.StringDatabase;
            if(stringDatabase == null)
            {
                callback?.Invoke("");
                yield break;
            }

            AsyncOperationHandle<StringTable> handle = stringDatabase.GetTableAsync(tableName);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                StringTable table = handle.Result;
                StringTableEntry entry = table != null ? table.GetEntry(key) : null;
                string value = entry != null ? entry.LocalizedValue : "";
                callback?.Invoke(value);
            }
            else
            {
                callback?.Invoke("");
            }
        }

        private IEnumerator InitializeCoroutine(Action callback = null)
        {
            AsyncOperationHandle handle = LocalizationSettings.InitializationOperation;
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _isInitialized = true;
                Debug.Log("Localization initialized.");
            }
            else
            {
                Debug.LogError($"Localization initialize failed.\r\n{handle.OperationException?.Message}");
            }

            _initializeCoroutine = null;

            callback?.Invoke();
        }
    }
}
#endif