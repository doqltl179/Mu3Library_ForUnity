#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Mu3Library.Localization
{
    public class LocalizationManager : ILocalizationManager
    {
        private bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        private Locale m_defaultLocale;
        private Locale _defaultLocale
        {
            get
            {
                if (m_defaultLocale == null)
                {
                    IReadOnlyList<Locale> locales = LocalizationSettings.AvailableLocales?.Locales;
                    Locale fromSettings = locales?
                        .FirstOrDefault(t => t.Identifier.CultureInfo.TwoLetterISOLanguageName == "en");
                    if (fromSettings == null && locales != null && locales.Count > 0)
                    {
                        fromSettings = locales[0];
                    }

                    m_defaultLocale = fromSettings ?? CreateEnglishFallbackLocale();
                }

                return m_defaultLocale;
            }
        }

        private Locale _currentLocale;
        public Locale CurrentLocale
        {
            get
            {
                if (_currentLocale == null)
                {
                    return _defaultLocale;
                }

                return _currentLocale;
            }
        }

        private bool _isInitializing = false;

        private readonly List<Action> _initializeCallbacks = new();



        #region Utility
        public void Initialize(Action callback = null)
        {
            if (_isInitialized)
            {
                callback?.Invoke();
                return;
            }

            if (callback != null)
            {
                _initializeCallbacks.Add(callback);
            }

            if (_isInitializing)
            {
                return;
            }

            _isInitializing = true;

            AsyncOperationHandle<LocalizationSettings> handle = LocalizationSettings.InitializationOperation;
            if (handle.IsDone)
            {
                OnInitializeCompleted(handle);
                return;
            }

            handle.Completed += OnInitializeCompleted;
        }

        public void GetStringAsync(string tableName, string key, Action<string> callback)
        {
            LocalizedStringDatabase stringDatabase = LocalizationSettings.StringDatabase;
            if (stringDatabase == null)
            {
                callback?.Invoke("");
                return;
            }

            AsyncOperationHandle<StringTable> handle = stringDatabase.GetTableAsync(tableName);
            handle.Completed += operation =>
            {
                if (operation.Status == AsyncOperationStatus.Succeeded)
                {
                    StringTable table = operation.Result;
                    StringTableEntry entry = table != null ? table.GetEntry(key) : null;
                    string value = entry != null ? entry.LocalizedValue : "";
                    callback?.Invoke(value);
                }
                else
                {
                    callback?.Invoke("");
                }
            };
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

            _currentLocale = locale;
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
        #endregion

        private void OnInitializeCompleted(AsyncOperationHandle<LocalizationSettings> handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _isInitialized = true;
                Debug.Log("Localization initialized.");
            }
            else
            {
                Debug.LogError($"Localization initialize failed.\r\n{handle.OperationException?.Message}");
            }

            var settings = handle.Result;
            if(settings != null)
            {
                _currentLocale = settings.GetSelectedLocale();
            }

            _isInitializing = false;

            if (_initializeCallbacks.Count == 0)
            {
                return;
            }

            Action[] callbacks = _initializeCallbacks.ToArray();
            _initializeCallbacks.Clear();

            foreach (Action cb in callbacks)
            {
                cb?.Invoke();
            }
        }

        private Locale CreateEnglishFallbackLocale()
        {
            Locale locale = Locale.CreateLocale("en");
            if (locale != null)
            {
                return locale;
            }

            locale = ScriptableObject.CreateInstance<Locale>();
            locale.Identifier = new LocaleIdentifier("en");
            return locale;
        }
    }
}
#endif
